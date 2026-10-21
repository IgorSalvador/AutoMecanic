using AutoMecanic.Api;
using AutoMecanic.Infrastructure.Persistencia;
using AutoMecanic.IntegrationTests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoMecanic.IntegrationTests.Implantacao;

/// <summary>
/// O modo <c>--migrar</c> é o comando do Job de migração no Kubernetes. O Job pode ser
/// reexecutado a cada deploy e a cada nova tentativa do orquestrador, então preparar o banco
/// precisa ser idempotente: rodar de novo não pode falhar nem duplicar dados.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ModoDeMigracaoTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task PrepararBanco_ExecutadoDeNovoSobreBancoPronto_EhIdempotente()
    {
        var configuracao = ambiente.Services.GetRequiredService<IConfiguration>();

        var usuariosAntes = await ContarUsuariosAsync();

        await PreparadorDoBancoDeDados.ExecutarAsync(ambiente.Services, configuracao);
        await PreparadorDoBancoDeDados.ExecutarAsync(ambiente.Services, configuracao);

        (await ContarUsuariosAsync()).ShouldBe(usuariosAntes);

        using var escopo = ambiente.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AutoMecanicDbContext>();

        (await contexto.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    private async Task<int> ContarUsuariosAsync()
    {
        using var escopo = ambiente.Services.CreateScope();

        return await escopo.ServiceProvider.GetRequiredService<AutoMecanicDbContext>().Usuarios.CountAsync();
    }
}

/// <summary>Leitura do modo de execução a partir da linha de comando.</summary>
public sealed class ArgumentosDeExecucaoTests
{
    [Fact]
    public void Interpretar_SemArgumentos_SobeOServidor()
    {
        var (modo, restantes) = ArgumentosDeExecucao.Interpretar([]);

        modo.ShouldBe(ModoDeExecucao.Servidor);
        restantes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--migrar")]
    [InlineData("--MIGRAR")]
    public void Interpretar_ComMigrar_AtivaOModoERemoveOArgumento(string argumento)
    {
        var (modo, restantes) = ArgumentosDeExecucao.Interpretar([argumento, "--urls=http://+:8080"]);

        modo.ShouldBe(ModoDeExecucao.Migrar);

        // O argumento de modo não pode chegar ao provedor de configuração; os demais, sim.
        restantes.ShouldBe(["--urls=http://+:8080"]);
    }
}
