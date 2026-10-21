using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Informa a situação atual de uma Ordem de Serviço (Recebida, Em diagnóstico, Aguardando
/// aprovação, Em execução, Finalizada, Entregue ou Cancelada), sem itens nem orçamento.
/// </summary>
public sealed class ConsultarStatusDaOrdemServico(IRepositorioDeOrdensServico repositorio)
{
    public async Task<StatusDaOrdemServicoResponse> ExecutarAsync(Guid id, CancellationToken cancellationToken = default) =>
        await repositorio.ObterStatusAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ordem de Serviço", id);
}
