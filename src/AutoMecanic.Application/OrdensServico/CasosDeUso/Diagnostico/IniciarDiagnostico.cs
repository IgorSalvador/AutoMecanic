using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Move a OS de Recebida para Em diagnóstico, registrando quem assumiu a avaliação.</summary>
public sealed class IniciarDiagnostico(OperacaoSobreOrdemServico operacao, IUsuarioAtual usuarioAtual)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await operacao.AplicarAsync(id, ordem => ordem.IniciarDiagnostico(usuarioAtual.Id), cancellationToken);
    }
}
