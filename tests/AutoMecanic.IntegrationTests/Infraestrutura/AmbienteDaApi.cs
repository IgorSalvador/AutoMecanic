using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace AutoMecanic.IntegrationTests.Infraestrutura;

/// <summary>
/// Sobe a API real contra um PostgreSQL real, em contêiner descartável.
/// <para>
/// A escolha por banco de verdade — e não por provedor em memória — é deliberada: boa parte
/// do que precisa ser testado (conversores de Objeto de Valor, restrições CHECK, unicidade,
/// <c>xmin</c> de concorrência, tradução de <c>ILIKE</c>) simplesmente não existe fora do
/// PostgreSQL. Um teste que passa em memória e falha em produção não é um teste.
/// </para>
/// </summary>
public sealed class AmbienteDaApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Senha do administrador semeado, usada para autenticar os testes.</summary>
    public const string SenhaDoAdministrador = "Admin@Teste2026";

    /// <summary>E-mail do administrador semeado.</summary>
    public const string EmailDoAdministrador = "admin@automecanic.com.br";

    private readonly PostgreSqlContainer _banco = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("automecanic_testes")
        .WithUsername("automecanic")
        .WithPassword("automecanic")
        .WithCleanUp(true)
        .Build();

    /// <summary>
    /// Servidor SMTP de teste: recebe os e-mails de verdade, por SMTP, e os expõe por uma API HTTP
    /// para as verificações. É o mesmo Mailpit do ambiente de desenvolvimento.
    /// </summary>
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.3")
        .WithPortBinding(PortaSmtp, true)
        .WithPortBinding(PortaHttp, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(PortaHttp).ForPath("/livez")))
        .WithCleanUp(true)
        .Build();

    private const ushort PortaSmtp = 1025;

    private const ushort PortaHttp = 8025;

    /// <summary>Endereço da API HTTP do Mailpit, para consultar os e-mails recebidos.</summary>
    public Uri UrlDoMailpit => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(PortaHttp)}");

    /// <summary>Opções de serialização espelhando as da API (enums por nome, camelCase).</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Sobe o contêiner e publica a configuração como variáveis de ambiente do processo.
    /// <para>
    /// A API usa hospedagem mínima e lê <c>builder.Configuration</c> na primeira linha de
    /// <c>Program.cs</c> — antes de o host existir. Por isso, fontes de configuração
    /// adicionadas via <c>ConfigureAppConfiguration</c> chegam tarde demais. Variável de
    /// ambiente é lida pelo <c>WebApplicationBuilder</c> na construção, que é o único ponto
    /// em que ainda dá para influenciá-lo sem alterar o código de produção.
    /// </para>
    /// </summary>
    public async Task InitializeAsync()
    {
        await Task.WhenAll(_banco.StartAsync(), _mailpit.StartAsync());

        Definir("ConnectionStrings__PostgreSQL", _banco.GetConnectionString());

        // Chave exclusiva do ambiente de teste: nenhum token emitido aqui é aceito
        // por outra instância da aplicação.
        Definir("Jwt__ChaveDeAssinatura", "chave-de-teste-com-mais-de-32-caracteres-para-hmac-sha256");
        Definir("Jwt__Emissor", "AutoMecanic.Api");
        Definir("Jwt__Audiencia", "AutoMecanic.Clientes");
        Definir("Jwt__ValidadeEmMinutos", "60");

        Definir("BancoDeDados__MigrarNaInicializacao", "true");
        Definir("BancoDeDados__SemearDados", "true");
        Definir("Seed__SenhaDoAdministrador", SenhaDoAdministrador);
        Definir("Seed__IncluirDadosDeDemonstracao", "false");

        // O limitador de taxa continua ligado, mas com folga: uma suíte automatizada
        // dispara de um único endereço o que, em produção, viriam de dezenas de máquinas.
        Definir("LimiteDeTaxa__LoginPorMinuto", "1000");
        Definir("LimiteDeTaxa__GlobalPorMinuto", "10000");
        Definir("LimiteDeTaxa__ClientePorMinuto", "1000");

        // E-mail: SMTP real (Mailpit), mas sem o processo em segundo plano — os testes disparam o
        // processamento da caixa de saída quando querem, para verificar o resultado sem esperas.
        Definir("Notificacoes__Habilitadas", "false");
        Definir("Smtp__Host", _mailpit.Hostname);
        Definir("Smtp__Porta", _mailpit.GetMappedPublicPort(PortaSmtp).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Definir("Smtp__Seguranca", "Nenhuma");
        Definir("Smtp__Remetente", "oficina@automecanic.com.br");

        Definir("Serilog__MinimumLevel__Default", "Warning");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _banco.DisposeAsync();
        await _mailpit.DisposeAsync();
    }

    /// <summary>
    /// Relógio da aplicação sob teste. Anda junto com o relógio real, mas pode ser adiantado
    /// para exercitar regras de prazo (validade do orçamento) sem esperar dias.
    /// </summary>
    public RelogioDeTeste Relogio { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Substitui a fonte única de tempo: domínio e aplicação passam a ler este relógio.
        builder.ConfigureTestServices(servicos =>
            servicos.Replace(ServiceDescriptor.Singleton<TimeProvider>(Relogio)));
    }

    private static void Definir(string chave, string valor) =>
        Environment.SetEnvironmentVariable(chave, valor);

    /// <summary>Cliente HTTP anônimo, para os endpoints públicos.</summary>
    public HttpClient CriarClienteAnonimo() => CreateClient();

    /// <summary>Cliente HTTP já autenticado como administrador.</summary>
    public async Task<HttpClient> CriarClienteAutenticadoAsync()
    {
        var http = CreateClient();
        var token = await ObterTokenAsync(http, EmailDoAdministrador, SenhaDoAdministrador);

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return http;
    }

    /// <summary>Autentica e devolve apenas o token, para cenários de autorização por perfil.</summary>
    public static async Task<string> ObterTokenAsync(HttpClient http, string email, string senha)
    {
        var resposta = await http.PostAsJsonAsync("/api/v1/autenticacao/login", new { email, senha }, Json);

        resposta.EnsureSuccessStatusCode();

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        return corpo.GetProperty("token").GetString()!;
    }
}

/// <summary>
/// Compartilha um único contêiner de banco entre todas as classes de teste da coleção.
/// Subir um PostgreSQL por classe multiplicaria o tempo de execução sem ganho de isolamento
/// — os testes já usam dados distintos entre si.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoDeIntegracao : ICollectionFixture<AmbienteDaApi>
{
    public const string Nome = "Integração da API";
}
