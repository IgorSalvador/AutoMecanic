using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Troca o responsável técnico pela OS.</summary>
public sealed class AtribuirResponsavel(OperacaoSobreOrdemServico operacao)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        AtribuirResponsavelRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await operacao.AplicarAsync(id, ordem => ordem.AtribuirResponsavel(requisicao.ResponsavelId), cancellationToken);
    }
}
