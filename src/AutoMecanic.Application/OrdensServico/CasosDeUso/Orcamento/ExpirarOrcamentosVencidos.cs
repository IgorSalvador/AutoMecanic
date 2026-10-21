using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Rotina de manutenção: expira os orçamentos que passaram da validade sem resposta do
/// cliente, cancela as respectivas OS e devolve as peças reservadas ao estoque.
/// </summary>
public sealed class ExpirarOrcamentosVencidos(
    IRepositorioDeOrdensServico repositorio,
    ReservasDaOrdemServico reservas,
    IProvedorDeDataHora relogio,
    IUnitOfWork unitOfWork,
    ILogger<ExpirarOrcamentosVencidos> logger)
{
    /// <returns>Quantidade de orçamentos expirados.</returns>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var agora = relogio.Agora;
        var candidatas = await repositorio.ListarComOrcamentoVencidoAsync(agora, cancellationToken);

        if (candidatas.Count == 0)
        {
            return 0;
        }

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var expiradas = 0;

            foreach (var ordem in candidatas)
            {
                var statusAnterior = ordem.Status;

                ordem.ExpirarOrcamento(agora);

                if (ordem.Status == statusAnterior)
                {
                    continue;
                }

                await reservas.LiberarAsync(ordem, ct);

                repositorio.Atualizar(ordem);
                expiradas++;
            }

            if (expiradas > 0)
            {
                await unitOfWork.SalvarAlteracoesAsync(ct);
                logger.LogInformation("{Total} orçamento(s) expirado(s) por decurso de prazo.", expiradas);
            }

            return expiradas;
        }, cancellationToken);
    }
}
