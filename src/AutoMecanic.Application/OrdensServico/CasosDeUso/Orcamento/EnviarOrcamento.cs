using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Envia o orçamento ao cliente: a OS passa a Aguardando aprovação e os itens ficam congelados.</summary>
public sealed class EnviarOrcamento(OperacaoSobreOrdemServico operacao)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        EnviarOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await operacao.AplicarAsync(id, ordem => ordem.EnviarOrcamentoParaAprovacao(requisicao.ValidadeEmDias), cancellationToken);
    }
}
