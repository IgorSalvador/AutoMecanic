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

            peca.GarantirPecaAtiva();

            // A ordem importa: reservar primeiro faz a operação inteira falhar por falta de
            // saldo antes de qualquer alteração na OS, mantendo os dois agregados coerentes.
            peca.Reservar(requisicao.Quantidade, ordem.Id);

            var item = ordem.AdicionarPeca(
                peca.Id,
                peca.Codigo,
                peca.Nome,
                peca.PrecoUnitario.Valor,
                requisicao.Quantidade);

            ordem.ConfirmarReservaDePeca(item.Id);

            repositorioDePecas.Atualizar(peca);
            await operacao.PersistirAsync(ordem, ct);

            logger.LogInformation("Peça {Codigo} x{Quantidade} reservada para a OS {Numero}.",
                peca.Codigo, requisicao.Quantidade, ordem.Numero.Valor);

            return await operacao.ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }
}
