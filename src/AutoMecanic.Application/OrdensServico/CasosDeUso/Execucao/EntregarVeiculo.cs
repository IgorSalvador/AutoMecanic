using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Registra a entrega do veículo ao cliente. Estado terminal de sucesso.</summary>
public sealed class EntregarVeiculo(OperacaoSobreOrdemServico operacao, IUsuarioAtual usuarioAtual)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        EntregarVeiculoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await operacao.AplicarAsync(id, ordem => ordem.EntregarVeiculo(requisicao.Observacao, usuarioAtual.Id), cancellationToken);
    }
}
