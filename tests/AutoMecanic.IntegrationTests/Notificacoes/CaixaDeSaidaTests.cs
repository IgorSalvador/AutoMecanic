using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Infrastructure.Notificacoes;
using AutoMecanic.Infrastructure.Persistencia;
using AutoMecanic.IntegrationTests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AutoMecanic.IntegrationTests.Notificacoes;

/// <summary>
/// "Atualização de status da OS via e-mail", de ponta a ponta: a mudança grava a mensagem na
/// caixa de saída na mesma transação, e o processador a entrega por SMTP — aqui, a um Mailpit
/// real em contêiner.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class CaixaDeSaidaTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task AberturaDaOS_GravaMensagemEProcessadorEntregaPorSmtp()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var email = GeradorDeDadosValidos.ProximoEmail();

        var os = await ReceberAsync(http, email);
        var numero = os.GetProperty("numero").GetString()!;

        // Gravada com a OS, ainda não enviada.
        var pendente = (await MensagensDaOsAsync(numero)).ShouldHaveSingleItem();
        pendente.Situacao.ShouldBe(SituacaoDaMensagem.Pendente);
        pendente.Destinatario.ShouldBe(email);

        await ProcessarAteEsvaziarAsync();

        (await MensagensDaOsAsync(numero)).ShouldHaveSingleItem().Situacao.ShouldBe(SituacaoDaMensagem.Enviada);

        var recebidos = await EmailsRecebidosAsync(email);
        recebidos.ShouldHaveSingleItem().GetProperty("Subject").GetString()!.ShouldContain(numero);
    }

    [Fact]
    public async Task CadaMudancaDeSituacao_GeraUmEmail()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var email = GeradorDeDadosValidos.ProximoEmail();
        var os = await ReceberAsync(http, email);
        var id = os.GetProperty("id").GetGuid();

        (await http.PostAsJsonAsync($"/api/v1/ordens-servico/{id}/diagnostico/iniciar", new { }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await http.PostAsJsonAsync($"/api/v1/ordens-servico/{id}/cancelar", new { motivo = "Cliente desistiu." }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        await ProcessarAteEsvaziarAsync();

        var assuntos = (await EmailsRecebidosAsync(email)).Select(m => m.GetProperty("Subject").GetString()).ToList();

        assuntos.Count.ShouldBe(3);
        assuntos.ShouldContain(a => a!.Contains("aberta"));
        assuntos.ShouldContain(a => a!.Contains("Em diagnóstico"));
        assuntos.ShouldContain(a => a!.Contains("Cancelada"));
    }

    [Fact]
    public async Task OperacaoDesfeita_NaoDeixaMensagemNaCaixa()
    {
        // A mensagem é gravada na transação da mudança: sem mudança, sem e-mail.
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var email = GeradorDeDadosValidos.ProximoEmail();

        var resposta = await http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = GeradorDeDadosValidos.ProximoCpf(),
            nomeCliente = "Cliente da Operação Desfeita",
            emailCliente = email,
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Fiat",
            modelo = "Mobi",
            anoFabricacao = 2020,
            descricaoProblema = "Revisão.",
            pecas = new[] { new { pecaId = Guid.CreateVersion7(), quantidade = 1 } }
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var escopo = ambiente.Services.CreateScope();
        (await Contexto(escopo).MensagensDeSaida.AnyAsync(m => m.Destinatario == email)).ShouldBeFalse();
    }

    [Fact]
    public async Task FalhaNoEnvio_MantemAMensagemEAgendaNovaTentativa()
    {
        var referencia = await InserirMensagemAsync("falha");

        using var escopo = ambiente.Services.CreateScope();
        var processador = CriarProcessador(escopo, new EnviadorQueFalha());

        await processador.ProcessarLoteAsync();

        var mensagem = await Contexto(escopo).MensagensDeSaida.AsNoTracking().SingleAsync(m => m.Referencia == referencia);

        mensagem.Situacao.ShouldBe(SituacaoDaMensagem.Pendente);
        mensagem.Tentativas.ShouldBe(1);
        mensagem.UltimoErro.ShouldBe("SMTP indisponível");
        mensagem.ProximaTentativaEm.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ProcessadoresConcorrentes_EnviamCadaMensagemUmaUnicaVez()
    {
        // Duas réplicas da API processando ao mesmo tempo: o SKIP LOCKED reparte o trabalho.
        var referencias = new List<string>();

        for (var i = 0; i < 12; i++)
        {
            referencias.Add(await InserirMensagemAsync($"concorrente-{i}"));
        }

        var enviador = new EnviadorQueRegistra();

        await Task.WhenAll(
            Task.Run(() => ProcessarComAsync(enviador)),
            Task.Run(() => ProcessarComAsync(enviador)));

        foreach (var referencia in referencias)
        {
            enviador.Envios.Count(e => e == referencia).ShouldBe(1, $"a mensagem {referencia} deveria sair uma única vez");
        }
    }

    // -----------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------

    private async Task ProcessarComAsync(IEnviadorDeEmail enviador)
    {
        using var escopo = ambiente.Services.CreateScope();
        var processador = CriarProcessador(escopo, enviador, tamanhoDoLote: 3);

        while (await processador.ProcessarLoteAsync() > 0)
        {
        }
    }

    private async Task ProcessarAteEsvaziarAsync()
    {
        using var escopo = ambiente.Services.CreateScope();
        var processador = escopo.ServiceProvider.GetRequiredService<ProcessadorDaCaixaDeSaida>();

        while (await processador.ProcessarLoteAsync() > 0)
        {
        }
    }

    private static ProcessadorDaCaixaDeSaida CriarProcessador(IServiceScope escopo, IEnviadorDeEmail enviador, int tamanhoDoLote = 20) =>
        new(
            Contexto(escopo),
            enviador,
            TimeProvider.System,
            Options.Create(new OpcoesDaCaixaDeSaida { TamanhoDoLote = tamanhoDoLote, MaximoDeTentativas = 5 }),
            NullLogger<ProcessadorDaCaixaDeSaida>.Instance);

    /// <summary>Mensagem criada no passado distante, para ficar à frente da fila.</summary>
    private async Task<string> InserirMensagemAsync(string prefixo)
    {
        var unica = $"{prefixo}-{Guid.CreateVersion7():N}";
        var referencia = unica.Length > 40 ? unica[^40..] : unica;

        using var escopo = ambiente.Services.CreateScope();
        var contexto = Contexto(escopo);

        var mensagem = MensagemDeSaida.De(
            new EmailDeSaida("teste@exemplo.com", "Teste", "Assunto", "<p>Corpo</p>", "Corpo", referencia),
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero));

        contexto.MensagensDeSaida.Add(mensagem);
        await contexto.SaveChangesAsync();

        return referencia;
    }

    private async Task<List<MensagemDeSaida>> MensagensDaOsAsync(string numero)
    {
        using var escopo = ambiente.Services.CreateScope();

        return await Contexto(escopo).MensagensDeSaida.AsNoTracking().Where(m => m.Referencia == numero).ToListAsync();
    }

    private async Task<List<JsonElement>> EmailsRecebidosAsync(string destinatario)
    {
        using var mailpit = new HttpClient { BaseAddress = ambiente.UrlDoMailpit };

        var resposta = await mailpit.GetFromJsonAsync<JsonElement>(
            $"/api/v1/search?query={Uri.EscapeDataString($"to:\"{destinatario}\"")}");

        return [.. resposta.GetProperty("messages").EnumerateArray()];
    }

    private static AutoMecanicDbContext Contexto(IServiceScope escopo) =>
        escopo.ServiceProvider.GetRequiredService<AutoMecanicDbContext>();

    private static async Task<JsonElement> ReceberAsync(HttpClient http, string email)
    {
        var resposta = await http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = GeradorDeDadosValidos.ProximoCpf(),
            nomeCliente = "Cliente da Notificação",
            emailCliente = email,
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Renault",
            modelo = "Kardian",
            anoFabricacao = 2024,
            descricaoProblema = "Revisão dos 10 mil km.",
            quilometragemEntrada = 10_000
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private sealed class EnviadorQueFalha : IEnviadorDeEmail
    {
        public Task EnviarAsync(EmailDeSaida email, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("SMTP indisponível");
    }

    private sealed class EnviadorQueRegistra : IEnviadorDeEmail
    {
        public ConcurrentBag<string> Envios { get; } = [];

        public async Task EnviarAsync(EmailDeSaida email, CancellationToken cancellationToken = default)
        {
            // Envio lento o bastante para os dois processadores se sobreporem.
            await Task.Delay(20, cancellationToken);
            Envios.Add(email.Referencia);
        }
    }
}
