using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoMecanic.Infrastructure.Notificacoes;

/// <summary>
/// Processo em segundo plano que esvazia a caixa de saída periodicamente. Cada rodada usa um
/// escopo próprio, com seu próprio <c>DbContext</c>, independente das requisições HTTP.
/// <para>
/// Ao receber o sinal de encerramento (o Kubernetes removendo a réplica), termina a rodada em
/// curso ou a desfaz: nenhuma mensagem fica marcada como enviada sem ter sido.
/// </para>
/// </summary>
public sealed class TrabalhadorDaCaixaDeSaida(
    IServiceScopeFactory fabricaDeEscopos,
    IOptions<OpcoesDaCaixaDeSaida> opcoes,
    TimeProvider relogio,
    ILogger<TrabalhadorDaCaixaDeSaida> logger) : BackgroundService
{
    private readonly TimeSpan _intervalo = TimeSpan.FromSeconds(Math.Max(1, opcoes.Value.IntervaloEmSegundos));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Caixa de saída ativa: verificação a cada {Intervalo}.", _intervalo);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var escopo = fabricaDeEscopos.CreateScope();

                await escopo.ServiceProvider
                    .GetRequiredService<ProcessadorDaCaixaDeSaida>()
                    .ProcessarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excecao)
            {
                // Banco indisponível, por exemplo: registra e tenta de novo na próxima rodada.
                logger.LogError(excecao, "Falha ao processar a caixa de saída.");
            }

            try
            {
                await Task.Delay(_intervalo, relogio, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
