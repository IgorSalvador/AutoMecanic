using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Registra o laudo técnico do mecânico, durante o diagnóstico ou a execução.</summary>
public sealed class RegistrarDiagnostico(OperacaoSobreOrdemServico operacao)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        RegistrarDiagnosticoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await operacao.AplicarAsync(id, ordem => ordem.RegistrarDiagnostico(requisicao.Diagnostico), cancellationToken);
    }
}
