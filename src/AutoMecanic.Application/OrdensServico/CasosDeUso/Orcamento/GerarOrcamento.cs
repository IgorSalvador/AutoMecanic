using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Gera, ou regera, o orçamento a partir dos itens. O valor é sempre calculado, nunca informado.</summary>
public sealed class GerarOrcamento(OperacaoSobreOrdemServico operacao)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        GerarOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await operacao.AplicarAsync(id, ordem => ordem.GerarOrcamento(requisicao.PercentualDesconto), cancellationToken);
    }
}
