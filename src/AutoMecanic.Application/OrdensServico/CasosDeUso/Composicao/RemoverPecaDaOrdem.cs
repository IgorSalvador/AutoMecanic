using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Retira uma peça da OS, devolvendo a reserva ao estoque na mesma transação.</summary>
public sealed class RemoverPecaDaOrdem(
    OperacaoSobreOrdemServico operacao,
    IRepositorioDePecas repositorioDePecas,
    IUnitOfWork unitOfWork)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await operacao.ExigirAsync(id, ct);

            var item = ordem.ItensPeca.FirstOrDefault(i => i.Id == itemId)
                ?? throw new RecursoNaoEncontradoException("Item de peça", itemId);

            if (item.Reservada)
            {
                var peca = await repositorioDePecas.ObterPorIdAsync(item.PecaId, ct)
                    ?? throw new RecursoNaoEncontradoException("Peça", item.PecaId);

                peca.LiberarReserva(item.Quantidade, ordem.Id);
                repositorioDePecas.Atualizar(peca);
            }

            ordem.RemoverPeca(itemId);

            await operacao.PersistirAsync(ordem, ct);

            return await operacao.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
