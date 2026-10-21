using System.Reflection;
using AutoMecanic.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace AutoMecanic.ArchitectureTests;

/// <summary>Convenções da camada de entrega (API) e da aplicação.</summary>
public sealed class ConvencoesDaApiTests
{
    [Fact]
    public void Controladores_TerminamEmControllerEHerdamDeControllerBase()
    {
        var resultado = Types.InAssembly(Camadas.Api)
            .That()
            .ResideInNamespace($"{Camadas.NamespaceDaApi}.Controllers")
            .And()
            .AreClasses()
            .Should()
            .HaveNameEndingWith("Controller", StringComparison.Ordinal)
            .And()
            .Inherit(typeof(ControllerBase))
            .GetResult();

        resultado.IsSuccessful.ShouldBeTrue(
            $"Fora do padrão: {string.Join(", ", resultado.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Controladores_DeclaramAutorizacaoExplicitamente()
    {
        // A política de fallback é nula: um controlador sem atributo seria público por
        // omissão. Exigir a declaração torna a decisão visível em cada classe.
        var semDeclaracao = Camadas.Api.GetTypes()
            .Where(tipo => tipo.IsClass && !tipo.IsAbstract && typeof(ControllerBase).IsAssignableFrom(tipo))
            .Where(tipo => tipo.GetCustomAttribute<AuthorizeAttribute>() is null
                           && tipo.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Select(tipo => tipo.Name)
            .ToList();

        semDeclaracao.ShouldBeEmpty($"Controladores sem [Authorize] ou [AllowAnonymous]: {string.Join(", ", semDeclaracao)}");
    }

    [Fact]
    public void ImplementacoesDeRepositorio_FicamNaInfraestrutura()
    {
        var tiposComRepositorio = new[] { Camadas.Dominio, Camadas.Aplicacao, Camadas.Api }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(tipo => tipo is { IsClass: true, IsAbstract: false }
                           && tipo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepositorio<>)))
            .Select(tipo => tipo.FullName)
            .ToList();

        tiposComRepositorio.ShouldBeEmpty();
    }

    [Fact]
    public void ManipuladoresDeEventos_FicamNaAplicacao()
    {
        var foraDaAplicacao = new[] { Camadas.Dominio, Camadas.Infraestrutura, Camadas.Api }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(tipo => tipo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)))
            .Select(tipo => tipo.FullName)
            .ToList();

        foraDaAplicacao.ShouldBeEmpty();
    }
}
