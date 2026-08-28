using System.Reflection;
using AutoMecanic.Application.Clientes;
using AutoMecanic.Application.Estoque;
using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Indicadores;
using AutoMecanic.Application.OrdensServico;
using AutoMecanic.Application.Servicos;
using AutoMecanic.Application.Veiculos;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AutoMecanic.Application;

/// <summary>Registro da camada de Aplicação no contêiner de injeção de dependência.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra os serviços de aplicação (casos de uso), os validadores de requisição e os
    /// manipuladores de eventos de domínio.
    /// </summary>
    public static IServiceCollection AdicionarCamadaDeAplicacao(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = Assembly.GetExecutingAssembly();

        services.AddScoped<IServicoDeClientes, ServicoDeClientes>();
        services.AddScoped<IServicoDeVeiculos, ServicoDeVeiculos>();
        services.AddScoped<IServicoDeCatalogo, ServicoDeCatalogo>();
        services.AddScoped<IServicoDeEstoque, ServicoDeEstoque>();
        services.AddScoped<IServicoDeOrdensServico, ServicoDeOrdensServico>();
        services.AddScoped<IServicoDeAutenticacao, ServicoDeAutenticacao>();
        services.AddScoped<IServicoDeUsuarios, ServicoDeUsuarios>();
        services.AddScoped<IServicoDeIndicadores, ServicoDeIndicadores>();

        // Validadores de requisição (FluentValidation), descobertos por convenção.
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Singleton, includeInternalTypes: false);

        // Manipuladores de eventos de domínio: qualquer classe que implemente
        // IDomainEventHandler<T> neste assembly é registrada automaticamente.
        services.AdicionarManipuladoresDeEventos(assembly);

        return services;
    }

    private static void AdicionarManipuladoresDeEventos(this IServiceCollection services, Assembly assembly)
    {
        var tipoDoContrato = typeof(Abstractions.IDomainEventHandler<>);

        var registros = assembly
            .GetTypes()
            .Where(tipo => tipo is { IsAbstract: false, IsInterface: false })
            .SelectMany(
                tipo => tipo.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == tipoDoContrato),
                (implementacao, contrato) => (contrato, implementacao));

        foreach (var (contrato, implementacao) in registros)
        {
            services.AddScoped(contrato, implementacao);
        }
    }
}
