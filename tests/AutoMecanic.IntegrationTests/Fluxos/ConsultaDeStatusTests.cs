using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.IntegrationTests.Infraestrutura;

namespace AutoMecanic.IntegrationTests.Fluxos;

/// <summary>
/// Consulta de status exigida pela Fase 2: situação atual da OS, lida por projeção no banco,
/// acompanhando cada mudança feita pelas ações do fluxo.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ConsultaDeStatusTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task ConsultarStatus_AcompanhaAsMudancasDeSituacao()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var os = await ReceberAsync(http);
        var id = os.GetProperty("id").GetGuid();

        var recebida = await StatusAsync(http, id);
        recebida.GetProperty("status").GetString().ShouldBe("Recebida");
        recebida.GetProperty("statusDescricao").GetString().ShouldBe("Recebida");
        recebida.GetProperty("numero").GetString().ShouldBe(os.GetProperty("numero").GetString());
        recebida.GetProperty("encerrada").GetBoolean().ShouldBeFalse();

        (await http.PostAsJsonAsync($"/api/v1/ordens-servico/{id}/diagnostico/iniciar", new { }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var emDiagnostico = await StatusAsync(http, id);
        emDiagnostico.GetProperty("status").GetString().ShouldBe("EmDiagnostico");
        emDiagnostico.GetProperty("statusDescricao").GetString().ShouldBe("Em diagnóstico");
        emDiagnostico.GetProperty("noStatusDesde").GetDateTimeOffset()
            .ShouldBeGreaterThanOrEqualTo(recebida.GetProperty("noStatusDesde").GetDateTimeOffset());

        (await http.PostAsJsonAsync($"/api/v1/ordens-servico/{id}/cancelar", new { motivo = "Cliente desistiu." }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancelada = await StatusAsync(http, id);
        cancelada.GetProperty("status").GetString().ShouldBe("Cancelada");
        cancelada.GetProperty("encerrada").GetBoolean().ShouldBeTrue();

        // Enxuta: sem itens, orçamento nem dados do cliente.
        cancelada.TryGetProperty("servicos", out _).ShouldBeFalse();
        cancelada.TryGetProperty("orcamento", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ConsultarStatus_DeOSInexistente_Responde404()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        (await http.GetAsync($"/api/v1/ordens-servico/{Guid.CreateVersion7()}/status"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConsultarStatus_SemToken_Responde401() =>
        (await ambiente.CriarClienteAnonimo().GetAsync($"/api/v1/ordens-servico/{Guid.CreateVersion7()}/status"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    private static async Task<JsonElement> StatusAsync(HttpClient http, Guid id)
    {
        var resposta = await http.GetAsync($"/api/v1/ordens-servico/{id}/status");

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> ReceberAsync(HttpClient http)
    {
        var resposta = await http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = GeradorDeDadosValidos.ProximoCpf(),
            nomeCliente = "Cliente da Consulta de Status",
            emailCliente = GeradorDeDadosValidos.ProximoEmail(),
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Fiat",
            modelo = "Pulse",
            anoFabricacao = 2023,
            descricaoProblema = "Luz de óleo acesa.",
            quilometragemEntrada = 15_000
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
