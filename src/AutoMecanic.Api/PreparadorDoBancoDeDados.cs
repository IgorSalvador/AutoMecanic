using AutoMecanic.Infrastructure.Persistencia;
using AutoMecanic.Infrastructure.Persistencia.Seed;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Api;

/// <summary>
/// Aplica as migrações pendentes e executa a carga inicial de dados.
/// <para>
/// É usado em dois momentos: na inicialização da API, quando
/// <c>BancoDeDados:MigrarNaInicializacao</c> está ligado (o padrão do docker-compose, em que
/// há uma única instância), e no modo <c>--migrar</c>, executado pelo Job do Kubernetes antes
/// de o Deployment subir as réplicas.
/// </para>
/// </summary>
internal static class PreparadorDoBancoDeDados
{
    /// <summary>
    /// Migra o esquema e, se configurado, semeia os dados iniciais.
    /// </summary>
    /// <param name="servicos">Provedor de serviços da aplicação já construída.</param>
    /// <param name="configuracao">Configuração da aplicação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public static async Task ExecutarAsync(
        IServiceProvider servicos,
        IConfiguration configuracao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentNullException.ThrowIfNull(configuracao);

        using var escopo = servicos.CreateScope();
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(PreparadorDoBancoDeDados));

        try
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<AutoMecanicDbContext>();

            logger.LogInformation("Aplicando migrações pendentes...");
            await contexto.Database.MigrateAsync(cancellationToken);

            if (!configuracao.GetValue("BancoDeDados:SemearDados", true))
            {
                logger.LogInformation("Banco de dados migrado. Carga inicial desligada por configuração.");
                return;
            }

            var senhaDoAdministrador = configuracao["Seed:SenhaDoAdministrador"];

            if (string.IsNullOrWhiteSpace(senhaDoAdministrador))
            {
                logger.LogWarning(
                    "Seed ignorado: defina Seed__SenhaDoAdministrador para criar o usuário administrador inicial.");
                return;
            }

            var semeador = escopo.ServiceProvider.GetRequiredService<SemeadorDeDados>();

            await semeador.ExecutarAsync(
                senhaDoAdministrador,
                configuracao.GetValue("Seed:IncluirDadosDeDemonstracao", false),
                cancellationToken);

            logger.LogInformation("Banco de dados pronto.");
        }
        catch (Exception excecao)
        {
            logger.LogCritical(excecao, "Falha ao preparar o banco de dados.");
            throw;
        }
    }
}
