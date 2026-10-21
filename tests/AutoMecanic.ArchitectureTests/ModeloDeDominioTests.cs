using System.Reflection;
using AutoMecanic.Domain.Abstractions;
using NetArchTest.Rules;

namespace AutoMecanic.ArchitectureTests;

/// <summary>
/// Convenções do modelo de domínio que a revisão de código tende a deixar passar: estado
/// alterado só por comportamento, objetos de valor imutáveis e eventos que não mudam depois
/// de emitidos.
/// </summary>
public sealed class ModeloDeDominioTests
{
    [Fact]
    public void EntidadesEObjetosDeValor_NaoExpoemSetterPublico()
    {
        // Um setter público permitiria mudar o status de uma OS sem passar pela máquina de
        // estados — exatamente o que o requisito proíbe.
        var violacoes = Camadas.Dominio.GetTypes()
            .Where(tipo => typeof(Entity).IsAssignableFrom(tipo) || typeof(ValueObject).IsAssignableFrom(tipo))
            .SelectMany(tipo => tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(propriedade => propriedade.SetMethod is { IsPublic: true })
                .Select(propriedade => $"{tipo.Name}.{propriedade.Name}"))
            .ToList();

        violacoes.ShouldBeEmpty($"Propriedades com setter público: {string.Join(", ", violacoes)}");
    }

    [Fact]
    public void AgregadosEObjetosDeValor_SaoSelados()
    {
        // Herança aberta no domínio deixaria uma subclasse contornar invariantes.
        var resultado = Types.InAssembly(Camadas.Dominio)
            .That()
            .Inherit(typeof(AggregateRoot))
            .Or()
            .Inherit(typeof(ValueObject))
            .Should()
            .BeSealed()
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(
            $"Tipos não selados: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void EventosDeDominio_SaoSeladosEFicamNoNamespaceEvents()
    {
        var eventos = Camadas.Dominio.GetTypes()
            .Where(tipo => typeof(IDomainEvent).IsAssignableFrom(tipo) && tipo is { IsAbstract: false, IsInterface: false })
            .ToList();

        eventos.ShouldNotBeEmpty();

        var foraDoPadrao = eventos
            .Where(tipo => !tipo.IsSealed || tipo.Namespace?.EndsWith(".Events", StringComparison.Ordinal) != true)
            .Select(tipo => tipo.FullName)
            .ToList();

        foraDoPadrao.ShouldBeEmpty($"Eventos fora do padrão: {string.Join(", ", foraDoPadrao)}");
    }
}
