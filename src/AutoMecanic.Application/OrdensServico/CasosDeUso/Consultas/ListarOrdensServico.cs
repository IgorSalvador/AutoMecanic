using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Listagem paginada de Ordens de Serviço, com filtros por situação, cliente, veículo e período.</summary>
public sealed class ListarOrdensServico(IRepositorioDeOrdensServico repositorio)
{
    public async Task<ResultadoPaginado<OrdemServicoResumoResponse>> ExecutarAsync(
        StatusOrdemServico? status,
        Guid? clienteId,
        Guid? veiculoId,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorio.ListarAsync(status, clienteId, veiculoId, de, ate, paginacao, cancellationToken);

        return ResultadoPaginado<OrdemServicoResumoResponse>.Criar(
            [.. pagina.Itens.Select(OrdemServicoResumoResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }
}
