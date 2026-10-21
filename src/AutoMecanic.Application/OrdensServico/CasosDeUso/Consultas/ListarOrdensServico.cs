using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Fila de trabalho da oficina: Em execução, depois Aguardando aprovação, Em diagnóstico e
/// Recebida; dentro de cada situação, das mais antigas para as mais novas. Finalizadas,
/// entregues e canceladas saem da listagem — a exclusão é lógica, nada é apagado — e voltam
/// quando o filtro pede as encerradas ou uma situação específica.
/// </summary>
public sealed class ListarOrdensServico(IRepositorioDeOrdensServico repositorio)
{
    public async Task<ResultadoPaginado<OrdemServicoResumoResponse>> ExecutarAsync(
        FiltroDeOrdensServico filtro,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default) =>
        await repositorio.ListarAsync(filtro, paginacao, cancellationToken);
}
