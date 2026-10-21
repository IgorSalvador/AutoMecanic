using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Inclui um serviço do catálogo na OS. Preço e tempo estimado são copiados neste momento e
/// congelados no item: um reajuste posterior no catálogo não altera esta OS.
/// </summary>
public sealed class AdicionarServicoNaOrdem(
    OperacaoSobreOrdemServico operacao,
    IRepositorioDeServicos repositorioDeServicos)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        AdicionarServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var ordem = await operacao.ExigirAsync(id, cancellationToken);

        var servico = await repositorioDeServicos.ObterPorIdAsync(requisicao.ServicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Serviço", requisicao.ServicoId);

        servico.GarantirServicoAtivo();

        ordem.AdicionarServico(
            servico.Id,
            servico.Nome,
            servico.Preco.Valor,
            requisicao.Quantidade,
            servico.TempoEstimadoEmMinutos);

        await operacao.PersistirAsync(ordem, cancellationToken);

        return await operacao.ProjetarAsync(ordem, cancellationToken);
    }
}
