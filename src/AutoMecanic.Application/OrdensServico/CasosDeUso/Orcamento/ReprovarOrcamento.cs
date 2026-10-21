using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Registra a recusa do cliente. A OS é cancelada e as reservas de peça voltam ao estoque, na
/// mesma transação.
/// </summary>
public sealed class ReprovarOrcamento(
    OperacaoSobreOrdemServico operacao,
    ReservasDaOrdemServico reservas,
    IUnitOfWork unitOfWork,
    ILogger<ReprovarOrcamento> logger)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        ReprovarOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await operacao.ExigirAsync(id, ct);

            ordem.ReprovarOrcamento(requisicao.Motivo);

            await reservas.LiberarAsync(ordem, ct);
            await operacao.PersistirAsync(ordem, ct);

            logger.LogInformation("Orçamento da OS {Numero} reprovado pelo cliente.", ordem.Numero.Valor);

            return await operacao.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
