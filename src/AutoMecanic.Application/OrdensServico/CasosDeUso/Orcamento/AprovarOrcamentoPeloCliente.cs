using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// O próprio cliente aprova o orçamento recebido, sem autenticação, informando o número da OS
/// e o CPF/CNPJ do titular. Os efeitos são os mesmos da aprovação registrada pelo atendente:
/// a OS entra em execução e as peças reservadas são baixadas, na mesma transação.
/// </summary>
public sealed class AprovarOrcamentoPeloCliente(
    AcessoDoTitular acesso,
    DecisaoDoOrcamento decisao,
    IRepositorioDeOrdensServico repositorio,
    IUnitOfWork unitOfWork,
    ILogger<AprovarOrcamentoPeloCliente> logger)
{
    public async Task<AcompanhamentoResponse> ExecutarAsync(
        RespostaAoOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await acesso.LocalizarAsync(requisicao.Numero, requisicao.Documento, ct);

            await decisao.AprovarAsync(ordem, ct);

            repositorio.Atualizar(ordem);
            await unitOfWork.SalvarAlteracoesAsync(ct);

            logger.LogInformation("Orçamento da OS {Numero} aprovado pelo próprio cliente.", ordem.Numero.Valor);

            return await acesso.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
