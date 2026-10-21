using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Registra a aprovação do cliente. A OS passa a Em execução, e é essa transição que autoriza
/// a baixa das peças reservadas — na mesma transação.
/// </summary>
public sealed class AprovarOrcamento(
    OperacaoSobreOrdemServico operacao,
    DecisaoDoOrcamento decisao,
    IUnitOfWork unitOfWork,
    ILogger<AprovarOrcamento> logger)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await operacao.ExigirAsync(id, ct);

            await decisao.AprovarAsync(ordem, ct);
            await operacao.PersistirAsync(ordem, ct);

            logger.LogInformation("Orçamento da OS {Numero} aprovado. Execução iniciada.", ordem.Numero.Valor);

            return await operacao.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
