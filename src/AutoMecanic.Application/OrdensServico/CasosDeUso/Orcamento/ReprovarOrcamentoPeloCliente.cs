using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// O próprio cliente recusa o orçamento recebido, sem autenticação, informando o número da OS
/// e o CPF/CNPJ do titular. A OS é cancelada e as reservas de peça voltam ao estoque.
/// </summary>
public sealed class ReprovarOrcamentoPeloCliente(
    AcessoDoTitular acesso,
    DecisaoDoOrcamento decisao,
    IRepositorioDeOrdensServico repositorio,
    IUnitOfWork unitOfWork,
    ILogger<ReprovarOrcamentoPeloCliente> logger)
{
    public async Task<AcompanhamentoResponse> ExecutarAsync(
        RecusaDoOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await acesso.LocalizarAsync(requisicao.Numero, requisicao.Documento, ct);

            await decisao.ReprovarAsync(ordem, requisicao.Motivo, ct);

            repositorio.Atualizar(ordem);
            await unitOfWork.SalvarAlteracoesAsync(ct);

            logger.LogInformation("Orçamento da OS {Numero} recusado pelo próprio cliente.", ordem.Numero.Valor);

            return await acesso.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
