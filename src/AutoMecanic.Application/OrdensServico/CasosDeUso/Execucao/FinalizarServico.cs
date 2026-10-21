using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Conclui os serviços: a OS passa a Finalizada e a duração real da execução é registrada.</summary>
public sealed class FinalizarServico(OperacaoSobreOrdemServico operacao, IUsuarioAtual usuarioAtual)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        FinalizarServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await operacao.AplicarAsync(id, ordem => ordem.FinalizarServico(requisicao.Observacao, usuarioAtual.Id), cancellationToken);
    }
}
