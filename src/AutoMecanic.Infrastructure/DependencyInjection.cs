using AutoMecanic.Application.Abstractions;
using AutoMecanic.Infrastructure.Persistencia;
using AutoMecanic.Infrastructure.Persistencia.Repositorios;
using AutoMecanic.Infrastructure.Seguranca;
using AutoMecanic.Infrastructure.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoMecanic.Infrastructure;

/// <summary>Registro da camada de Infraestrutura no contêiner de injeção de dependência.</summary>
public static class DependencyInjection
{
    /// <summary>Nome da cadeia de conexão esperada na configuração.</summary>
    public const string NomeDaConexao = "PostgreSQL";

    /// <summary>
    /// Registra o contexto de persistência, os repositórios e os serviços técnicos
    /// (hash de senha, emissão de token, relógio).
    /// </summary>
    public static IServiceCollection AdicionarCamadaDeInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var cadeiaDeConexao = configuration.GetConnectionString(NomeDaConexao)
            ?? throw new InvalidOperationException(
                $"A cadeia de conexão '{NomeDaConexao}' não foi configurada. "
                + "Defina ConnectionStrings__PostgreSQL nas variáveis de ambiente.");

        services.AddDbContext<AutoMecanicDbContext>(opcoes =>
        {
            opcoes.UseNpgsql(cadeiaDeConexao, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__historico_migracoes", AutoMecanicDbContext.Esquema);

                // Repete falhas transitórias (queda momentânea de rede, reinício do banco)
                // antes de devolver erro ao cliente da API.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            // Consultas não rastreadas por padrão: a maioria das operações é leitura para
            // projeção em DTO. Os casos de uso que alteram estado pedem rastreamento
            // explicitamente ao carregar o agregado pelo repositório.
            opcoes.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DespachanteDeEventosDeDominio>();
        services.AddScoped<IGeradorDeNumeroDeOrdemServico, GeradorDeNumeroDeOrdemServico>();

        services.AddScoped<IRepositorioDeClientes, RepositorioDeClientes>();
        services.AddScoped<IRepositorioDeVeiculos, RepositorioDeVeiculos>();
        services.AddScoped<IRepositorioDeServicos, RepositorioDeServicos>();
        services.AddScoped<IRepositorioDePecas, RepositorioDePecas>();
        services.AddScoped<IRepositorioDeMovimentosDeEstoque, RepositorioDeMovimentosDeEstoque>();
        services.AddScoped<IRepositorioDeOrdensServico, RepositorioDeOrdensServico>();
        services.AddScoped<IRepositorioDeUsuarios, RepositorioDeUsuarios>();

        services.AddSingleton<IProvedorDeDataHora, ProvedorDeDataHora>();
        services.AddSingleton<IServicoDeHashDeSenha, ServicoDeHashDeSenhaBCrypt>();
        services.AddScoped<IGeradorDeToken, GeradorDeTokenJwt>();

        // A configuração de JWT é validada na inicialização: sem chave válida, a aplicação
        // falha ao subir em vez de aceitar tokens forjados em produção.
        services.AddOptions<OpcoesDeJwt>()
            .Bind(configuration.GetSection(OpcoesDeJwt.SecaoDeConfiguracao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
