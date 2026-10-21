using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Altera a quantidade de um serviço já incluído, enquanto os itens ainda podem mudar.</summary>
public sealed class AlterarQuantidadeDeServico(OperacaoSobreOrdemServico operacao)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        Guid itemId,
        AlterarQuantidadeRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await operacao.AplicarAsync(id, ordem => ordem.AlterarQuantidadeDeServico(itemId, requisicao.Quantidade), cancellationToken);
    }
}
