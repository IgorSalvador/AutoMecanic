using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoMecanic.Infrastructure.Persistencia;

/// <summary>
/// Constrói o <see cref="AutoMecanicDbContext"/> para as ferramentas de linha de comando do
/// EF Core (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>).
/// <para>
/// Sem esta fábrica, as ferramentas precisariam inicializar a API inteira — e portanto exigir
/// que segredos como a chave JWT estivessem disponíveis só para gerar uma migração. A cadeia
/// de conexão usada aqui só descreve o esquema; nenhuma credencial de produção fica no código.
/// </para>
/// </summary>
public sealed class FabricaDeContextoEmTempoDeDesign : IDesignTimeDbContextFactory<AutoMecanicDbContext>
{
    private const string CadeiaPadraoDeDesenvolvimento =
        "Host=localhost;Port=5432;Database=automecanic;Username=automecanic;Password=automecanic";

    public AutoMecanicDbContext CreateDbContext(string[] args)
    {
        // Permite apontar para outro banco sem editar código:
        //   $env:ConnectionStrings__PostgreSQL="..."; dotnet ef migrations add Nome
        var cadeiaDeConexao = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL")
            ?? CadeiaPadraoDeDesenvolvimento;

        var opcoes = new DbContextOptionsBuilder<AutoMecanicDbContext>()
            .UseNpgsql(cadeiaDeConexao, npgsql =>
                npgsql.MigrationsHistoryTable("__historico_migracoes", AutoMecanicDbContext.Esquema))
            .Options;

        return new AutoMecanicDbContext(opcoes);
    }
}
