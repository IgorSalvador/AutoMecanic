using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Retira um serviço da OS, enquanto os itens ainda podem mudar.</summary>
public sealed class RemoverServicoDaOrdem(OperacaoSobreOrdemServico operacao)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return await operacao.AplicarAsync(id, ordem => ordem.RemoverServico(itemId), cancellationToken);
    }
}
