using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;

/// <summary>
/// O que todo caso de uso da Ordem de Serviço faz antes e depois da regra de negócio:
/// carregar o agregado inteiro, persistir a alteração e projetar a resposta com o nome do
/// cliente e a descrição do veículo.
/// <para>
/// Concentrar esse roteiro aqui mantém cada caso de uso reduzido à sua única decisão — qual
/// comportamento do domínio chamar e com quais dados.
/// </para>
/// </summary>
public sealed class OperacaoSobreOrdemServico(
    IRepositorioDeOrdensServico repositorio,
    IRepositorioDeClientes repositorioDeClientes,
    IRepositorioDeVeiculos repositorioDeVeiculos,
    IUnitOfWork unitOfWork)
{
    /// <summary>Carrega a OS com itens, orçamento e histórico, ou falha com 404.</summary>
    public async Task<OrdemServico> ExigirAsync(Guid id, CancellationToken cancellationToken) =>
        await repositorio.ObterCompletaPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ordem de Serviço", id);

    /// <summary>Marca a OS como alterada e grava, despachando os eventos de domínio.</summary>
    public async Task PersistirAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        repositorio.Atualizar(ordem);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    /// <summary>
    /// Carrega a OS, aplica um comportamento de domínio, persiste e devolve a projeção. É o
    /// caminho completo dos casos de uso que tocam apenas o agregado Ordem de Serviço.
    /// </summary>
    public async Task<OrdemServicoResponse> AplicarAsync(
        Guid id,
        Action<OrdemServico> comportamento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comportamento);

        var ordem = await ExigirAsync(id, cancellationToken);

        comportamento(ordem);

        await PersistirAsync(ordem, cancellationToken);

        return await ProjetarAsync(ordem, cancellationToken);
    }

    /// <summary>Enriquece a resposta com nome e documento do cliente e descrição do veículo.</summary>
    public async Task<OrdemServicoResponse> ProjetarAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        var cliente = await repositorioDeClientes.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
        var veiculo = await repositorioDeVeiculos.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return OrdemServicoResponse.De(
            ordem,
            cliente?.Nome,
            cliente?.Documento.Formatado,
            veiculo?.Descricao);
    }
}
