using NetArchTest.Rules;

namespace AutoMecanic.ArchitectureTests;

/// <summary>
/// A <b>regra de dependência</b> da Clean Architecture, verificada sobre o código compilado:
/// cada camada só conhece as que estão mais para dentro.
/// <code>
/// Api ──▶ Application ──▶ Domain ◀── Infrastructure
///                  ▲                      │
///                  └──── portas ──────────┘
/// </code>
/// <para>
/// A referência entre projetos já impede boa parte das violações, mas não todas: o projeto
/// da API referencia a Infraestrutura (é a raiz de composição), e nada impediria um
/// controlador de usar o <c>DbContext</c> diretamente. Estes testes fecham essa brecha.
/// </para>
/// </summary>
public sealed class RegraDeDependenciaTests
{
    [Fact]
    public void Dominio_NaoDependeDeNenhumaOutraCamada()
    {
        var resultado = Types.InAssembly(Camadas.Dominio)
            .ShouldNot()
            .HaveDependencyOnAny(
                Camadas.NamespaceDaAplicacao,
                Camadas.NamespaceDaInfraestrutura,
                Camadas.NamespaceDaApi)
            .GetResult();

        Verificar(resultado);
    }

    [Fact]
    public void Dominio_NaoDependeDeFrameworks()
    {
        var resultado = Types.InAssembly(Camadas.Dominio)
            .ShouldNot()
            .HaveDependencyOnAny([.. Camadas.FrameworksExternos, "FluentValidation", "Microsoft.Extensions"])
            .GetResult();

        Verificar(resultado);
    }

    [Fact]
    public void Aplicacao_NaoDependeDeInfraestruturaNemDaApi()
    {
        var resultado = Types.InAssembly(Camadas.Aplicacao)
            .ShouldNot()
            .HaveDependencyOnAny(Camadas.NamespaceDaInfraestrutura, Camadas.NamespaceDaApi)
            .GetResult();

        Verificar(resultado);
    }

    [Fact]
    public void Aplicacao_NaoDependeDeFrameworksDePersistenciaNemDeEntrega()
    {
        var resultado = Types.InAssembly(Camadas.Aplicacao)
            .ShouldNot()
            .HaveDependencyOnAny(Camadas.FrameworksExternos)
            .GetResult();

        Verificar(resultado);
    }

    [Fact]
    public void Infraestrutura_NaoDependeDaApi()
    {
        var resultado = Types.InAssembly(Camadas.Infraestrutura)
            .ShouldNot()
            .HaveDependencyOn(Camadas.NamespaceDaApi)
            .GetResult();

        Verificar(resultado);
    }

    [Fact]
    public void Controladores_NaoAcessamInfraestruturaDiretamente()
    {
        // O controlador fala com casos de uso. Repositório, DbContext ou serviço técnico
        // chegando a ele significa regra de negócio escapando para a borda.
        var resultado = Types.InAssembly(Camadas.Api)
            .That()
            .ResideInNamespace($"{Camadas.NamespaceDaApi}.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(Camadas.NamespaceDaInfraestrutura, "Microsoft.EntityFrameworkCore")
            .GetResult();

        Verificar(resultado);
    }

    [Fact]
    public void Controladores_NaoUsamRepositorios()
    {
        var resultado = Types.InAssembly(Camadas.Api)
            .That()
            .ResideInNamespace($"{Camadas.NamespaceDaApi}.Controllers")
            .ShouldNot()
            .HaveDependencyOnAny(Camadas.PortasDePersistencia)
            .GetResult();

        Verificar(resultado);
    }

    [Fact]
    public void Sanidade_AFerramentaDetectaDependenciasReais()
    {
        // Controle positivo: se a análise deixasse de enxergar dependências (montagem errada,
        // prefixo mal escrito), todas as regras acima passariam sem verificar nada.
        Types.InAssembly(Camadas.Infraestrutura)
            .That().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetTypes().ShouldNotBeEmpty("a infraestrutura usa EF Core");

        Types.InAssembly(Camadas.Api)
            .That().ResideInNamespace($"{Camadas.NamespaceDaApi}.Controllers")
            .And().HaveDependencyOn(Camadas.NamespaceDaAplicacao)
            .GetTypes().ShouldNotBeEmpty("os controladores dependem da aplicação");

        Camadas.PortasDePersistencia.Length.ShouldBeGreaterThan(5, "todas as portas de repositório devem ser encontradas");

        Types.InAssembly(Camadas.Aplicacao)
            .That().HaveDependencyOnAny(Camadas.PortasDePersistencia)
            .GetTypes().ShouldNotBeEmpty("os casos de uso usam os repositórios; a lista precisa casar com os tipos reais");
    }

    private static void Verificar(TestResult resultado) =>
        resultado.IsSuccessful.ShouldBeTrue(
            $"Tipos que violam a regra: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
}
