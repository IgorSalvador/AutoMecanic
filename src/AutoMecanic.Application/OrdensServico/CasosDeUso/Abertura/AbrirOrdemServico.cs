using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Abre uma Ordem de Serviço para cliente e veículo já cadastrados, informados pela identidade.
/// </summary>
public sealed class AbrirOrdemServico(
    IRepositorioDeClientes repositorioDeClientes,
    IRepositorioDeVeiculos repositorioDeVeiculos,
    AberturaDeOrdemServico abertura,
    IUnitOfWork unitOfWork)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        AbrirOrdemServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var cliente = await repositorioDeClientes.ObterPorIdAsync(requisicao.ClienteId, ct)
                ?? throw new RecursoNaoEncontradoException("Cliente", requisicao.ClienteId);

            var veiculo = await repositorioDeVeiculos.ObterPorIdAsync(requisicao.VeiculoId, ct)
                ?? throw new RecursoNaoEncontradoException("Veículo", requisicao.VeiculoId);

            var ordem = await abertura.AbrirAsync(cliente, veiculo, requisicao.DescricaoProblema, requisicao.QuilometragemEntrada, ct);

            await unitOfWork.SalvarAlteracoesAsync(ct);

            return OrdemServicoResponse.De(ordem, cliente.Nome, cliente.Documento.Formatado, veiculo.Descricao);
        }, cancellationToken);
    }
}
