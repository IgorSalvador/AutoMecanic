using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;

/// <summary>
/// A costura entre a Ordem de Serviço e o Estoque: baixar as peças reservadas quando o
/// orçamento é aprovado e devolvê-las quando a OS é encerrada sem execução.
/// <para>
/// É chamada dentro da transação do caso de uso, de modo que o saldo da peça e a situação do
/// item na OS mudam juntos ou não mudam.
/// </para>
/// </summary>
public sealed class ReservasDaOrdemServico(IRepositorioDePecas repositorioDePecas)
{
    /// <summary>Baixa do estoque todas as peças reservadas para a OS.</summary>
    public async Task ConsumirAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        var reservados = ordem.ItensPeca.Where(item => item.Reservada).ToList();

        if (reservados.Count == 0)
        {
            return;
        }

        var pecas = await CarregarPecasAsync(reservados.Select(item => item.PecaId), cancellationToken);

        foreach (var item in reservados)
        {
            if (!pecas.TryGetValue(item.PecaId, out var peca))
            {
                throw new RecursoNaoEncontradoException("Peça", item.PecaId);
            }

            peca.ConsumirReserva(item.Quantidade, ordem.Id);
            ordem.ConfirmarConsumoDePeca(item.Id);

            repositorioDePecas.Atualizar(peca);
        }
    }

    /// <summary>
    /// Devolve ao estoque todas as reservas ainda pendentes da OS. Uma peça que tenha deixado
    /// de existir é ignorada: não há reserva a devolver para ela.
    /// </summary>
    public async Task LiberarAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        var reservados = ordem.ItensPeca.Where(item => item.Reservada).ToList();

        if (reservados.Count == 0)
        {
            return;
        }

        var pecas = await CarregarPecasAsync(reservados.Select(item => item.PecaId), cancellationToken);

        foreach (var item in reservados)
        {
            if (!pecas.TryGetValue(item.PecaId, out var peca))
            {
                continue;
            }

            peca.LiberarReserva(item.Quantidade, ordem.Id);
            ordem.ConfirmarLiberacaoDeReservaDePeca(item.Id);

            repositorioDePecas.Atualizar(peca);
        }
    }

    private async Task<Dictionary<Guid, Domain.Estoque.Peca>> CarregarPecasAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken) =>
        (await repositorioDePecas.ObterPorIdsAsync([.. ids], cancellationToken)).ToDictionary(peca => peca.Id);
}
