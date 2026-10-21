using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Inclui uma peça na OS e a reserva no estoque, na mesma transação. A peça passa a contar
/// como comprometida — sai do saldo disponível — sem sair ainda da prateleira.
/// </summary>
public sealed class AdicionarPecaNaOrdem(
    OperacaoSobreOrdemServico operacao,
    ComposicaoDaOrdemServico composicao,
    IRepositorioDePecas repositorioDePecas,
    IUnitOfWork unitOfWork,
    ILogger<AdicionarPecaNaOrdem> logger)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        Guid id,
        AdicionarPecaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await operacao.ExigirAsync(id, ct);

            var peca = await repositorioDePecas.ObterPorIdAsync(requisicao.PecaId, ct)
                ?? throw new RecursoNaoEncontradoException("Peça", requisicao.PecaId);

            composicao.IncluirPeca(ordem, peca, requisicao.Quantidade);

            await operacao.PersistirAsync(ordem, ct);

            logger.LogInformation("Peça {Codigo} x{Quantidade} reservada para a OS {Numero}.",
                peca.Codigo, requisicao.Quantidade, ordem.Numero.Valor);

            return await operacao.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
