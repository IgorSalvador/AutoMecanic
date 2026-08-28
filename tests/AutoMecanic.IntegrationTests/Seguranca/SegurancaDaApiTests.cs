using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.IntegrationTests.Infraestrutura;

namespace AutoMecanic.IntegrationTests.Seguranca;

/// <summary>
/// Verifica as garantias de segurança exigidas pelo requisito: autenticação JWT nas APIs
/// administrativas, autorização por perfil, validação de dados sensíveis e ausência de
/// vazamento de informação nas respostas de erro.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class SegurancaDaApiTests(AmbienteDaApi ambiente)
{
    [Theory]
    [InlineData("/api/v1/clientes")]
    [InlineData("/api/v1/veiculos")]
    [InlineData("/api/v1/servicos")]
    [InlineData("/api/v1/pecas")]
    [InlineData("/api/v1/ordens-servico")]
    [InlineData("/api/v1/usuarios")]
    [InlineData("/api/v1/indicadores/painel")]
    public async Task RotasAdministrativas_SemToken_Respondem401(string rota)
    {
        var resposta = await ambiente.CriarClienteAnonimo().GetAsync(rota);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RotaAdministrativa_ComTokenForjado_Responde401()
    {
        var http = ambiente.CriarClienteAnonimo();

        // Token com estrutura de JWT, mas assinado com outra chave.
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.assinatura-invalida");

        (await http.GetAsync("/api/v1/clientes")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ComEmailInexistenteOuSenhaErrada_RespondeIgual()
    {
        var http = ambiente.CriarClienteAnonimo();

        var inexistente = await http.PostAsJsonAsync("/api/v1/autenticacao/login",
            new { email = "ninguem@automecanic.com.br", senha = "Qualquer@1" }, AmbienteDaApi.Json);

        var senhaErrada = await http.PostAsJsonAsync("/api/v1/autenticacao/login",
            new { email = AmbienteDaApi.EmailDoAdministrador, senha = "Errada@123" }, AmbienteDaApi.Json);

        inexistente.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        senhaErrada.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var corpoInexistente = await inexistente.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
        var corpoSenhaErrada = await senhaErrada.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);

        // Respostas idênticas: distingui-las permitiria enumerar contas válidas.
        corpoInexistente.GetProperty("detail").GetString()
            .ShouldBe(corpoSenhaErrada.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task RespostasDeUsuario_NuncaExpoemOHashDaSenha()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var resposta = await http.GetAsync("/api/v1/usuarios/eu");
        var corpo = await resposta.Content.ReadAsStringAsync();

        corpo.ShouldNotContain("senhaHash", Case.Insensitive);
        corpo.ShouldNotContain("$2a$", Case.Insensitive); // prefixo de hash BCrypt
        corpo.ShouldNotContain("$2b$", Case.Insensitive);
    }

    [Fact]
    public async Task PerfilMecanico_NaoPodeGerenciarUsuarios()
    {
        var admin = await ambiente.CriarClienteAutenticadoAsync();
        var email = GeradorDeDadosValidos.ProximoEmail();

        var criacao = await admin.PostAsJsonAsync("/api/v1/usuarios", new
        {
            nome = "Mecânico de Integração",
            email,
            senha = "Mecanico@2026",
            perfil = "Mecanico"
        }, AmbienteDaApi.Json);

        criacao.StatusCode.ShouldBe(HttpStatusCode.Created);

        var mecanico = ambiente.CriarClienteAnonimo();
        var token = await AmbienteDaApi.ObterTokenAsync(mecanico, email, "Mecanico@2026");
        mecanico.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // A política Administrar não inclui o perfil Mecânico.
        var tentativa = await mecanico.PostAsJsonAsync("/api/v1/usuarios", new
        {
            nome = "Alguém",
            email = GeradorDeDadosValidos.ProximoEmail(),
            senha = "Alguem@2026",
            perfil = "Atendente"
        }, AmbienteDaApi.Json);

        tentativa.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Mas ele consegue consultar, porque a política Consultar o inclui.
        (await mecanico.GetAsync("/api/v1/ordens-servico")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ContaBloqueiaAposCincoTentativasMalsucedidas()
    {
        var admin = await ambiente.CriarClienteAutenticadoAsync();
        var email = GeradorDeDadosValidos.ProximoEmail();

        await admin.PostAsJsonAsync("/api/v1/usuarios", new
        {
            nome = "Alvo de Força Bruta",
            email,
            senha = "Original@2026",
            perfil = "Atendente"
        }, AmbienteDaApi.Json);

        var http = ambiente.CriarClienteAnonimo();

        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            await http.PostAsJsonAsync("/api/v1/autenticacao/login",
                new { email, senha = "Errada@123" }, AmbienteDaApi.Json);
        }

        // A partir daqui, nem a senha correta entra.
        var comSenhaCorreta = await http.PostAsJsonAsync("/api/v1/autenticacao/login",
            new { email, senha = "Original@2026" }, AmbienteDaApi.Json);

        comSenhaCorreta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var corpo = await comSenhaCorreta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
        corpo.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("bloqueada");
    }

    [Theory]
    [InlineData("11111111111")]
    [InlineData("12345678900")]
    [InlineData("123")]
    public async Task CadastroDeCliente_ComCpfInvalido_Responde400(string documento)
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var resposta = await http.PostAsJsonAsync("/api/v1/clientes", new
        {
            nome = "Cliente Inválido",
            documento,
            email = GeradorDeDadosValidos.ProximoEmail(),
            telefone = "11987654321"
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("PLACA-RUIM")]
    [InlineData("AB1234")]
    [InlineData("ABC12345")]
    public async Task CadastroDeVeiculo_ComPlacaInvalida_Responde400(string placa)
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var cliente = await CriarClienteAsync(http);

        var resposta = await http.PostAsJsonAsync("/api/v1/veiculos", new
        {
            clienteId = cliente.GetProperty("id").GetGuid(),
            placa,
            marca = "Fiat",
            modelo = "Argo",
            anoFabricacao = 2022
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CadastroDeCliente_ComDocumentoDuplicado_Responde409()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var documento = GeradorDeDadosValidos.ProximoCpf();

        var corpo = new
        {
            nome = "Cliente Duplicado",
            documento,
            email = GeradorDeDadosValidos.ProximoEmail(),
            telefone = "11987654321"
        };

        (await http.PostAsJsonAsync("/api/v1/clientes", corpo, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await http.PostAsJsonAsync("/api/v1/clientes", corpo, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ErroDeValidacao_DevolveTodosOsCamposDeUmaVez()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var resposta = await http.PostAsJsonAsync("/api/v1/clientes", new
        {
            nome = "",
            documento = "invalido",
            email = "sem-arroba",
            telefone = ""
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
        var erros = problema.GetProperty("errors");

        // Corrigir um campo por requisição seria uma péssima experiência de integração.
        erros.EnumerateObject().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task RespostaDeErro_NaoVazaPilhaDeChamadasNemDetalhesInternos()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var resposta = await http.GetAsync($"/api/v1/clientes/{Guid.CreateVersion7()}");
        var corpo = await resposta.Content.ReadAsStringAsync();

        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        corpo.ShouldNotContain("Microsoft.EntityFrameworkCore");
        corpo.ShouldNotContain("   at ");
        corpo.ShouldNotContain("Npgsql");
    }

    [Fact]
    public async Task CabecalhosDeSeguranca_EstaoPresentesEmTodaResposta()
    {
        var resposta = await ambiente.CriarClienteAnonimo().GetAsync("/health/vivo");

        resposta.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        resposta.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        resposta.Headers.GetValues("Referrer-Policy").ShouldContain("no-referrer");
        resposta.Headers.Contains("Server").ShouldBeFalse();
    }

    [Fact]
    public async Task Paginacao_LimitaOTamanhoDePaginaMesmoQuandoOClientePedeMais()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var pagina = await http.GetFromJsonAsync<JsonElement>(
            "/api/v1/servicos?tamanhoPagina=100000", AmbienteDaApi.Json);

        // Sem esse limite, uma única requisição poderia arrastar a base inteira.
        pagina.GetProperty("tamanhoPagina").GetInt32().ShouldBeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task CriacaoDeUsuario_ComSenhaForaDaPolitica_Responde400()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var resposta = await http.PostAsJsonAsync("/api/v1/usuarios", new
        {
            nome = "Senha Fraca",
            email = GeradorDeDadosValidos.ProximoEmail(),
            senha = "123456",
            perfil = "Atendente"
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<JsonElement> CriarClienteAsync(HttpClient http)
    {
        var resposta = await http.PostAsJsonAsync("/api/v1/clientes", new
        {
            nome = "Cliente de Integração",
            documento = GeradorDeDadosValidos.ProximoCpf(),
            email = GeradorDeDadosValidos.ProximoEmail(),
            telefone = "11987654321"
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
