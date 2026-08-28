using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AutoMecanic.Api.Configuracao;
using AutoMecanic.Api.Filtros;
using AutoMecanic.Api.Middlewares;
using AutoMecanic.Api.Servicos;
using AutoMecanic.Application;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Infrastructure;
using AutoMecanic.Infrastructure.Persistencia;
using AutoMecanic.Infrastructure.Persistencia.Seed;
using AutoMecanic.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Registro estruturado. Console em JSON facilita a coleta por agregadores de log
// em contêiner, sem depender de arquivo em disco.
// ---------------------------------------------------------------------------
builder.Host.UseSerilog((contexto, configuracao) => configuracao
    .ReadFrom.Configuration(contexto.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Aplicacao", "AutoMecanic.Api")
    .WriteTo.Console());

// ---------------------------------------------------------------------------
// Camadas da aplicação
// ---------------------------------------------------------------------------
builder.Services.AdicionarCamadaDeAplicacao();
builder.Services.AdicionarCamadaDeInfraestrutura(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtual>();
builder.Services.AddScoped<SemeadorDeDados>();

// ---------------------------------------------------------------------------
// MVC e serialização
// ---------------------------------------------------------------------------
builder.Services
    .AddControllers(opcoes =>
    {
        // Validação automática de todo contrato de entrada que tenha validador registrado.
        opcoes.Filters.Add<FiltroDeValidacao>();

        // Sem isto, uma requisição com Accept incompatível receberia JSON assim mesmo.
        opcoes.ReturnHttpNotAcceptable = true;
    })
    .AddJsonOptions(opcoes =>
    {
        // Enumerações serializadas por nome: o contrato da API fica legível e deixa de
        // depender da ordem numérica dos membros no código.
        opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opcoes.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// A validação do ModelState do MVC é desligada porque o FiltroDeValidacao já responde
// no formato ProblemDetails padronizado pela API.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(opcoes =>
    opcoes.SuppressModelStateInvalidFilter = false);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

// ---------------------------------------------------------------------------
// Autenticação JWT
// ---------------------------------------------------------------------------
var opcoesDeJwt = builder.Configuration.GetSection(OpcoesDeJwt.SecaoDeConfiguracao).Get<OpcoesDeJwt>()
    ?? throw new InvalidOperationException(
        "A seção de configuração 'Jwt' é obrigatória. Defina Jwt__ChaveDeAssinatura, Jwt__Emissor e Jwt__Audiencia.");

if (string.IsNullOrWhiteSpace(opcoesDeJwt.ChaveDeAssinatura)
    || opcoesDeJwt.ChaveDeAssinatura.Length < OpcoesDeJwt.ComprimentoMinimoDaChave)
{
    throw new InvalidOperationException(
        $"A chave de assinatura JWT deve ter no mínimo {OpcoesDeJwt.ComprimentoMinimoDaChave} caracteres. "
        + "Configure a variável de ambiente Jwt__ChaveDeAssinatura com um segredo forte.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        // Exigir HTTPS para transportar o token é o padrão; só é relaxado em
        // desenvolvimento, onde não há certificado.
        opcoes.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        opcoes.SaveToken = false;

        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opcoesDeJwt.Emissor,

            ValidateAudience = true,
            ValidAudience = opcoesDeJwt.Audiencia,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoesDeJwt.ChaveDeAssinatura)),

            ValidateLifetime = true,

            // Padrão do framework é 5 minutos de tolerância; zerar evita que um token
            // expirado continue sendo aceito por esse período.
            ClockSkew = TimeSpan.Zero,

            RequireSignedTokens = true,
            RequireExpirationTime = true
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(null);

builder.Services.AddAuthorization(opcoes =>
{
    foreach (var (politica, perfis) in PoliticasDeAutorizacao.PerfisPorPolitica)
    {
        opcoes.AddPolicy(politica, regra => regra
            .RequireAuthenticatedUser()
            .RequireRole(perfis.Select(p => p.ToString()).ToArray()));
    }
});

// ---------------------------------------------------------------------------
// Limitação de taxa
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // O endpoint de login é o alvo natural de força bruta. Um limite próprio, mais
    // apertado, complementa o bloqueio por tentativas que já existe no domínio.
    opcoes.AddPolicy("login", contexto => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Limite geral, para conter uso abusivo das demais rotas.
    opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// ---------------------------------------------------------------------------
// CORS
// ---------------------------------------------------------------------------
const string PoliticaDeCors = "AutoMecanicCors";

var origensPermitidas = builder.Configuration
    .GetSection("Cors:OrigensPermitidas")
    .Get<string[]>() ?? [];

builder.Services.AddCors(opcoes =>
    opcoes.AddPolicy(PoliticaDeCors, politica =>
    {
        if (origensPermitidas.Length == 0)
        {
            // Sem origens configuradas, nenhuma origem de navegador é liberada.
            // Curinga com credenciais é proibido pela especificação de CORS e seria,
            // além disso, uma porta aberta para CSRF.
            politica.DisallowCredentials();
            return;
        }

        politica.WithOrigins(origensPermitidas)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }));

// ---------------------------------------------------------------------------
// Verificação de saúde
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AutoMecanicDbContext>("banco-de-dados", tags: ["pronto"]);

// ---------------------------------------------------------------------------
// Documentação OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoMecanic — Sistema Integrado de Atendimento e Execução de Serviços",
        Version = "v1",
        Description = """
            API do MVP de gestão de uma oficina mecânica de médio porte.

            **Fluxo principal da Ordem de Serviço**

            1. `POST /api/v1/ordens-servico/recepcao` — identifica o cliente pelo CPF/CNPJ, localiza ou cadastra o veículo e abre a OS (status **Recebida**)
            2. `POST /api/v1/ordens-servico/{id}/diagnostico/iniciar` — status **Em diagnóstico**
            3. `POST /api/v1/ordens-servico/{id}/servicos` e `.../pecas` — compõem a OS; as peças são reservadas no estoque
            4. `POST /api/v1/ordens-servico/{id}/orcamento` — gera o orçamento automaticamente a partir dos itens
            5. `POST /api/v1/ordens-servico/{id}/orcamento/enviar` — status **Aguardando aprovação**
            6. `POST /api/v1/ordens-servico/{id}/orcamento/aprovar` — status **Em execução**; as peças reservadas são baixadas
               ou `.../orcamento/reprovar` — status **Cancelada**; as reservas são devolvidas
            7. `POST /api/v1/ordens-servico/{id}/finalizar` — status **Finalizada**
            8. `POST /api/v1/ordens-servico/{id}/entregar` — status **Entregue**

            **Autenticação** — obtenha o token em `POST /api/v1/autenticacao/login` e informe-o
            no botão *Authorize* como `Bearer <token>`. A consulta pública de acompanhamento
            (`/api/v1/acompanhamento`) não exige autenticação.
            """,
        Contact = new OpenApiContact
        {
            Name = "Equipe AutoMecanic — Pós-Tech FIAP 15SOAT",
            Url = new Uri("https://github.com/")
        },
        License = new OpenApiLicense { Name = "MIT" }
    });

    opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe apenas o token JWT; o prefixo 'Bearer' é adicionado automaticamente."
    });

    // Aplica o esquema Bearer a todas as operações. Os endpoints marcados com
    // [AllowAnonymous] continuam acessíveis; o cadeado no Swagger apenas indica que a
    // interface enviará o token quando houver um.
    opcoes.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", documento)] = []
    });

    // Traz para o Swagger os comentários XML escritos nos controladores e DTOs,
    // que é onde a documentação de negócio já vive.
    foreach (var arquivo in Directory.GetFiles(AppContext.BaseDirectory, "AutoMecanic.*.xml"))
    {
        opcoes.IncludeXmlComments(arquivo, includeControllerXmlComments: true);
    }

    opcoes.SupportNonNullableReferenceTypes();
    opcoes.UseAllOfToExtendReferenceSchemas();
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------

// Primeiro do pipeline: qualquer exceção lançada adiante é convertida em problem+json.
app.UseMiddleware<MiddlewareDeTratamentoDeExcecoes>();

app.UseSerilogRequestLogging(opcoes =>
    opcoes.GetLevel = (contexto, _, excecao) =>
        excecao is not null || contexto.Response.StatusCode >= 500
            ? Serilog.Events.LogEventLevel.Error
            : Serilog.Events.LogEventLevel.Information);

app.UseMiddleware<MiddlewareDeCabecalhosDeSeguranca>();

if (!app.Environment.IsDevelopment())
{
    // HSTS instrui o navegador a nunca mais acessar este host por HTTP.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSwagger(opcoes => opcoes.RouteTemplate = "swagger/{documentName}/swagger.json");
app.UseSwaggerUI(opcoes =>
{
    opcoes.SwaggerEndpoint("/swagger/v1/swagger.json", "AutoMecanic API v1");
    opcoes.DocumentTitle = "AutoMecanic — Documentação da API";
    opcoes.DisplayRequestDuration();
    opcoes.EnableTryItOutByDefault();
});

app.UseCors(PoliticaDeCors);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/vivo", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    // Vivacidade: responde enquanto o processo estiver de pé, sem tocar no banco.
    Predicate = _ => false
});

app.MapHealthChecks("/health/pronto", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    // Prontidão: só reporta saudável quando o banco estiver acessível.
    Predicate = verificacao => verificacao.Tags.Contains("pronto")
});

// Raiz redireciona para a documentação: é o que uma pessoa espera ao abrir a URL.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ---------------------------------------------------------------------------
// Preparação do banco na inicialização
// ---------------------------------------------------------------------------
await PrepararBancoDeDadosAsync(app);

await app.RunAsync();

// Aplica as migrações pendentes e executa a carga inicial de dados.
//
// Migrar na inicialização é adequado a este MVP em contêiner, em que a API é a única
// escritora do esquema. Em um cenário com múltiplas réplicas, este passo migraria para um
// job de implantação dedicado, para que duas instâncias não tentem migrar ao mesmo tempo.
static async Task PrepararBancoDeDadosAsync(WebApplication aplicacao)
{
    if (!aplicacao.Configuration.GetValue("BancoDeDados:MigrarNaInicializacao", true))
    {
        return;
    }

    using var escopo = aplicacao.Services.CreateScope();
    var logger = escopo.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var contexto = escopo.ServiceProvider.GetRequiredService<AutoMecanicDbContext>();

        logger.LogInformation("Aplicando migrações pendentes...");
        await contexto.Database.MigrateAsync();

        if (!aplicacao.Configuration.GetValue("BancoDeDados:SemearDados", true))
        {
            return;
        }

        var senhaDoAdministrador = aplicacao.Configuration["Seed:SenhaDoAdministrador"];

        if (string.IsNullOrWhiteSpace(senhaDoAdministrador))
        {
            logger.LogWarning(
                "Seed ignorado: defina Seed__SenhaDoAdministrador para criar o usuário administrador inicial.");
            return;
        }

        var semeador = escopo.ServiceProvider.GetRequiredService<SemeadorDeDados>();

        await semeador.ExecutarAsync(
            senhaDoAdministrador,
            aplicacao.Configuration.GetValue("Seed:IncluirDadosDeDemonstracao", false));

        logger.LogInformation("Banco de dados pronto.");
    }
    catch (Exception excecao)
    {
        logger.LogCritical(excecao, "Falha ao preparar o banco de dados. A aplicação não pode iniciar.");
        throw;
    }
}

/// <summary>
/// Exposto para que os testes de integração possam instanciar a aplicação com
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
