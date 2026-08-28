using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Estoque.Events;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.Estoque.Handlers;

/// <summary>
/// Transforma cada <see cref="EstoqueMovimentado"/> em um lançamento no razão de estoque.
/// <para>
/// Roda dentro da mesma transação que alterou o saldo, o que garante a invariante mais
/// importante do controle de estoque: <b>não existe saldo sem lançamento correspondente</b>.
/// Se a gravação do razão falhar, a alteração de saldo é desfeita junto.
/// </para>
/// </summary>
public sealed class RegistrarMovimentoNoRazaoHandler(
    IRepositorioDeMovimentosDeEstoque repositorio,
    ILogger<RegistrarMovimentoNoRazaoHandler> logger) : IDomainEventHandler<EstoqueMovimentado>
{
    public async Task TratarAsync(EstoqueMovimentado evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var movimento = MovimentoEstoque.Registrar(
            evento.PecaId,
            evento.Tipo,
            evento.Quantidade,
            evento.SaldoAnterior,
            evento.SaldoAtual,
            evento.Motivo,
            evento.OrdemServicoId,
            evento.OcorridoEm);

        await repositorio.AdicionarAsync(movimento, cancellationToken);

        logger.LogDebug(
            "Razão de estoque: {Tipo} de {Quantidade} na peça {PecaId} ({SaldoAnterior} -> {SaldoAtual}).",
            evento.Tipo, evento.Quantidade, evento.PecaId, evento.SaldoAnterior, evento.SaldoAtual);
    }
}

/// <summary>
/// Registra em log o cruzamento do ponto de ressuprimento.
/// <para>
/// Em produção este é o ponto de extensão natural para notificar o setor de compras
/// (e-mail, fila de mensagens). Mantido como log no MVP para não introduzir dependência
/// externa dentro da transação.
/// </para>
/// </summary>
public sealed class AlertarEstoqueMinimoHandler(ILogger<AlertarEstoqueMinimoHandler> logger)
    : IDomainEventHandler<EstoqueAtingiuNivelMinimo>
{
    public Task TratarAsync(EstoqueAtingiuNivelMinimo evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        logger.LogWarning(
            "Ressuprimento necessário: peça {Codigo} ({Nome}) com {Disponivel} disponível(is), mínimo {Minimo}.",
            evento.Codigo, evento.Nome, evento.SaldoDisponivel, evento.EstoqueMinimo);

        return Task.CompletedTask;
    }
}
