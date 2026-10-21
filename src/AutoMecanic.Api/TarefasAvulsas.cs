using AutoMecanic.Application.OrdensServico.CasosDeUso;

namespace AutoMecanic.Api;

/// <summary>
/// Executa os modos de tarefa avulsa (Jobs e CronJobs do Kubernetes) e traduz o resultado em
/// código de saída: 0 em sucesso, 1 em falha. É o código de saída que o orquestrador lê para
/// decidir se tenta de novo e se marca a execução como falha.
/// </summary>
internal static class TarefasAvulsas
{
    public static async Task<int> ExecutarAsync(
        ModoDeExecucao modo,
        IServiceProvider servicos,
        IConfiguration configuracao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentNullException.ThrowIfNull(configuracao);

        var logger = servicos.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TarefasAvulsas));

        try
        {
            switch (modo)
            {
                case ModoDeExecucao.Migrar:
                    await PreparadorDoBancoDeDados.ExecutarAsync(servicos, configuracao, cancellationToken);
                    break;

                case ModoDeExecucao.ExpirarOrcamentos:
                    await ExpirarOrcamentosAsync(servicos, logger, cancellationToken);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo não é uma tarefa avulsa.");
            }

            return 0;
        }
        catch (Exception excecao)
        {
            logger.LogCritical(excecao, "A tarefa {Modo} falhou.", modo);
            return 1;
        }
    }

    private static async Task ExpirarOrcamentosAsync(
        IServiceProvider servicos,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var escopo = servicos.CreateScope();

        var expirados = await escopo.ServiceProvider
            .GetRequiredService<ExpirarOrcamentosVencidos>()
            .ExecutarAsync(cancellationToken);

        logger.LogInformation("Rotina de expiração concluída: {Total} orçamento(s) expirado(s).", expirados);
    }
}
