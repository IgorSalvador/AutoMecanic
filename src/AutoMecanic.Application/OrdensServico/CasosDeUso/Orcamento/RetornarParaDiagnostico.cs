using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Devolve a OS ao diagnóstico para revisar o orçamento a pedido do cliente.</summary>
public sealed class RetornarParaDiagnostico(OperacaoSobreOrdemServico operacao)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        string? motivo,
        CancellationToken cancellationToken = default)
    {
        return await operacao.AplicarAsync(id, ordem => ordem.RetornarParaDiagnostico(motivo), cancellationToken);
    }
}
