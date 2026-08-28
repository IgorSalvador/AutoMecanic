using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Infrastructure.Persistencia;

/// <summary>
/// Implementação da Unidade de Trabalho sobre o <see cref="AutoMecanicDbContext"/>.
/// </summary>
public sealed class UnitOfWork(
    AutoMecanicDbContext contexto,
    IDomainEventDispatcher despachante,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    /// <summary>
    /// Limite de rodadas de despacho de eventos. Um manipulador pode alterar agregados e
    /// gerar novos eventos; o laço trata essa cascata, e o limite impede que um ciclo entre
    /// manipuladores trave a requisição indefinidamente.
    /// </summary>
    private const int MaximoDeRodadasDeEventos = 10;

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        await DespacharEventosPendentesAsync(cancellationToken);

        return await contexto.SaveChangesAsync(cancellationToken);
    }

    public async Task<TResultado> ExecutarEmTransacaoAsync<TResultado>(
        Func<CancellationToken, Task<TResultado>> operacao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        // Chamadas aninhadas participam da transação já aberta em vez de abrir outra.
        if (contexto.Database.CurrentTransaction is not null)
        {
            return await operacao(cancellationToken);
        }

        // A estratégia de execução do Npgsql pode reexecutar o bloco em falhas transitórias;
        // por isso a transação precisa ser aberta dentro dela, e não em volta.
        var estrategia = contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async () =>
        {
            await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var resultado = await operacao(cancellationToken);

                await transacao.CommitAsync(cancellationToken);

                return resultado;
            }
            catch
            {
                await transacao.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <summary>
    /// Coleta e despacha, antes do commit, os eventos acumulados nos agregados rastreados.
    /// <para>
    /// Despachar <b>antes</b> de <c>SaveChanges</c> é uma decisão de projeto: assim, as
    /// entidades que os manipuladores criarem (como os lançamentos do razão de estoque)
    /// entram na mesma gravação e, portanto, na mesma transação.
    /// </para>
    /// </summary>
    private async Task DespacharEventosPendentesAsync(CancellationToken cancellationToken)
    {
        for (var rodada = 0; rodada < MaximoDeRodadasDeEventos; rodada++)
        {
            var agregados = contexto.ChangeTracker
                .Entries<AggregateRoot>()
                .Where(entrada => entrada.Entity.EventosDeDominio.Count > 0)
                .Select(entrada => entrada.Entity)
                .ToList();

            if (agregados.Count == 0)
            {
                return;
            }

            var eventos = agregados.SelectMany(a => a.EventosDeDominio).ToList();

            // Os eventos são limpos antes do despacho para que um manipulador que altere o
            // mesmo agregado não reprocesse o que já está sendo tratado nesta rodada.
            foreach (var agregado in agregados)
            {
                agregado.LimparEventos();
            }

            await despachante.DespacharAsync(eventos, cancellationToken);
        }

        logger.LogWarning(
            "Limite de {Limite} rodadas de despacho de eventos atingido. Possível ciclo entre manipuladores.",
            MaximoDeRodadasDeEventos);
    }
}
