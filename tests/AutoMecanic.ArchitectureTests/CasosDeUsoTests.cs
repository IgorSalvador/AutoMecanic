using System.Reflection;
using NetArchTest.Rules;

namespace AutoMecanic.ArchitectureTests;

/// <summary>
/// Forma dos casos de uso da Ordem de Serviço: uma classe por operação, coesa e independente
/// das demais. Os colaboradores compartilhados ficam no namespace <c>Apoio</c>.
/// </summary>
public sealed class CasosDeUsoTests
{
    private const string NamespaceDosCasosDeUso = "AutoMecanic.Application.OrdensServico.CasosDeUso";

    private const string NamespaceDeApoio = NamespaceDosCasosDeUso + ".Apoio";

    private static readonly Type[] CasosDeUso = Camadas.Aplicacao.GetTypes()
        .Where(tipo => tipo is { IsClass: true, IsPublic: true } && tipo.Namespace == NamespaceDosCasosDeUso)
        .ToArray();

    [Fact]
    public void CasosDeUso_ExistemEFicamNoNamespaceEsperado() =>
        CasosDeUso.Length.ShouldBeGreaterThan(15, "os casos de uso da OS devem ser encontrados pela convenção");

    [Fact]
    public void CasosDeUso_SaoSelados()
    {
        var resultado = Types.InAssembly(Camadas.Aplicacao)
            .That().ResideInNamespace(NamespaceDosCasosDeUso)
            .And().AreClasses()
            .Should().BeSealed()
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(
            $"Não selados: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void CasosDeUso_ExpoemApenasOperacoesAssincronas()
    {
        // Um caso de uso é uma operação: sua superfície pública são os métodos que a executam.
        var foraDoPadrao = CasosDeUso
            .SelectMany(tipo => tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(metodo => !metodo.Name.EndsWith("Async", StringComparison.Ordinal))
                .Select(metodo => $"{tipo.Name}.{metodo.Name}"))
            .ToList();

        foraDoPadrao.ShouldBeEmpty($"Métodos públicos fora do padrão: {string.Join(", ", foraDoPadrao)}");
    }

    [Fact]
    public void CasosDeUso_NaoDependemUnsDosOutros()
    {
        // O que dois casos de uso compartilham vai para o Apoio. Um caso de uso chamando outro
        // esconderia uma segunda transação dentro da primeira e acoplaria operações distintas.
        var nomes = CasosDeUso.Select(tipo => tipo.FullName!).ToArray();

        var violacoes = CasosDeUso
            .Where(tipo => Types.InAssembly(Camadas.Aplicacao)
                .That().HaveName(tipo.Name).And().ResideInNamespace(NamespaceDosCasosDeUso)
                .And().HaveDependencyOnAny([.. nomes.Where(nome => nome != tipo.FullName)])
                .GetTypes().Any())
            .Select(tipo => tipo.Name)
            .ToList();

        violacoes.ShouldBeEmpty($"Casos de uso que dependem de outros: {string.Join(", ", violacoes)}");
    }

    [Fact]
    public void Sanidade_DependenciaDeCasoDeUsoEhDetectada() =>
        // Controle positivo da regra acima: o semeador da infraestrutura usa os casos de uso, e
        // a análise precisa enxergar isso; do contrário, a regra passaria sem verificar nada.
        Types.InAssembly(Camadas.Infraestrutura)
            .That().HaveDependencyOnAny([.. CasosDeUso.Select(tipo => tipo.FullName!)])
            .GetTypes().ShouldNotBeEmpty();

    [Fact]
    public void Apoio_NaoDependeDosCasosDeUso()
    {
        var resultado = Types.InAssembly(Camadas.Aplicacao)
            .That().ResideInNamespace(NamespaceDeApoio)
            .ShouldNot().HaveDependencyOnAny([.. CasosDeUso.Select(tipo => tipo.FullName!)])
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(
            $"Apoio dependendo de casos de uso: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void CasosDeUso_TemPoucosMetodos()
    {
        // Indicador de coesão: a contagem de métodos declarados. O antigo serviço de Ordens de
        // Serviço reunia 26; um caso de uso que passe de 8 provavelmente faz coisas demais.
        var grandes = CasosDeUso
            .Select(tipo => (tipo.Name, Metodos: tipo.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Count(metodo => !metodo.IsSpecialName && !metodo.Name.Contains('<'))))
            .Where(item => item.Metodos > 8)
            .Select(item => $"{item.Name} ({item.Metodos})")
            .ToList();

        grandes.ShouldBeEmpty($"Casos de uso grandes demais: {string.Join(", ", grandes)}");
    }
}
