using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Cancela a OS antes da execução, devolvendo as reservas de peça ao estoque na mesma
/// transação.
/// </summary>
public sealed class CancelarOrdemServico(
    OperacaoSobreOrdemServico operacao,
    ReservasDaOrdemServico reservas,
    IUsuarioAtual usuarioAtual,
    IUnitOfWork unitOfWork)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        CancelarOrdemServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await operacao.ExigirAsync(id, ct);

            ordem.Cancelar(requisicao.Motivo, usuarioAtual.Id);

            await reservas.LiberarAsync(ordem, ct);
            await operacao.PersistirAsync(ordem, ct);

            return await operacao.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
