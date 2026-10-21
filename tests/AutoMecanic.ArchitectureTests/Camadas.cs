using System.Reflection;
using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Infrastructure.Persistencia;

namespace AutoMecanic.ArchitectureTests;

/// <summary>
/// Referências às quatro montagens da solução, obtidas por um tipo conhecido de cada uma.
/// Ancorar em tipos, e não em nomes de arquivo, faz um renomeio quebrar a compilação em vez
/// de silenciosamente esvaziar a regra.
/// </summary>
internal static class Camadas
{
    public static readonly Assembly Dominio = typeof(Entity).Assembly;

    public static readonly Assembly Aplicacao = typeof(IUnitOfWork).Assembly;

    public static readonly Assembly Infraestrutura = typeof(AutoMecanicDbContext).Assembly;

    public static readonly Assembly Api = typeof(PoliticasDeAutorizacao).Assembly;

    public const string NamespaceDoDominio = "AutoMecanic.Domain";

    public const string NamespaceDaAplicacao = "AutoMecanic.Application";

    public const string NamespaceDaInfraestrutura = "AutoMecanic.Infrastructure";

    public const string NamespaceDaApi = "AutoMecanic.Api";

    /// <summary>
    /// Nomes completos das portas de persistência: <see cref="IRepositorio{TAgregado}"/>, cada
    /// repositório específico e a Unidade de Trabalho. Listados por reflexão porque o
    /// NetArchTest compara nomes de tipo por igualdade, e não por prefixo.
    /// </summary>
    public static readonly string[] PortasDePersistencia =
    [
        .. Aplicacao.GetTypes()
            .Where(tipo => tipo.IsInterface
                           && (tipo == typeof(IRepositorio<>)
                               || tipo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepositorio<>))))
            .Select(tipo => tipo.FullName!),
        typeof(IUnitOfWork).FullName!
    ];

    /// <summary>Frameworks de entrega e persistência, que só podem aparecer nas camadas externas.</summary>
    public static readonly string[] FrameworksExternos =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore",
        "BCrypt",
        "System.IdentityModel.Tokens.Jwt",
        "Microsoft.IdentityModel"
    ];
}
