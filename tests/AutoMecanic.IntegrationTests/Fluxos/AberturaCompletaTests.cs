using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.IntegrationTests.Infraestrutura;

namespace AutoMecanic.IntegrationTests.Fluxos;

/// <summary>
/// Abertura de OS exigida pela Fase 2: uma chamada recebe cliente, veículo, serviços e peças e
/// devolve a identificação da OS. A transação é única — faltando saldo de uma peça, nada é
/// gravado, nem o cliente novo, nem o número da OS.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class AberturaCompletaTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task Recepcao_ComServicosEPecas_DevolveAOSComItensEReservaOEstoque()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var servico = await ObterPrimeiroServicoAsync(http);
        var peca = await CriarPecaAsync(http, quantidadeInicial: 10);

        var resposta = await ReceberAsync(http, GeradorDeDadosValidos.ProximoCpf(),
            servicos: [new { servicoId = servico.GetProperty("id").GetGuid(), quantidade = 1 }],
            pecas: [new { pecaId = peca.GetProperty("id").GetGuid(), quantidade = 4 }]);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());
        resposta.Headers.Location.ShouldNotBeNull();

        var os = await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);

        os.GetProperty("id").GetGuid().ShouldNotBe(Guid.Empty);
        os.GetProperty("numero").GetString().ShouldStartWith("OS-");
        os.GetProperty("status").GetString().ShouldBe("Recebida");
        os.GetProperty("servicos").GetArrayLength().ShouldBe(1);
        os.GetProperty("pecas")[0].GetProperty("reservada").GetBoolean().ShouldBeTrue();

        var saldo = await LerAsync(await http.GetAsync($"/api/v1/pecas/{peca.GetProperty("id").GetGuid()}"));
        saldo.GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(10);
        saldo.GetProperty("quantidadeReservada").GetInt32().ShouldBe(4);

        // A OS aberta já pode seguir o fluxo: o orçamento sai dos itens informados na abertura.
        var comOrcamento = await LerAsync(await http.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{os.GetProperty("id").GetGuid()}/orcamento",
            new { percentualDesconto = 0m }, AmbienteDaApi.Json));

        comOrcamento.GetProperty("orcamento").GetProperty("valorBruto").GetDecimal()
            .ShouldBe(servico.GetProperty("preco").GetDecimal() + (peca.GetProperty("precoUnitario").GetDecimal() * 4));
    }

    [Fact]
    public async Task Recepcao_ComPecaSemSaldo_NaoGravaNadaNemConsomeNumero()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var peca = await CriarPecaAsync(http, quantidadeInicial: 2);

        var anterior = await LerAsync(await ReceberAsync(http, GeradorDeDadosValidos.ProximoCpf()), HttpStatusCode.Created);

        var cpfDoClienteNovo = GeradorDeDadosValidos.ProximoCpf();
        var falha = await ReceberAsync(http, cpfDoClienteNovo,
            pecas: [new { pecaId = peca.GetProperty("id").GetGuid(), quantidade = 3 }]);

        falha.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity, await falha.Content.ReadAsStringAsync());

        // Nem o cliente novo, nem a reserva, nem o número sobreviveram à falha.
        (await http.GetAsync($"/api/v1/clientes/documento/{cpfDoClienteNovo}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var saldo = await LerAsync(await http.GetAsync($"/api/v1/pecas/{peca.GetProperty("id").GetGuid()}"));
        saldo.GetProperty("quantidadeReservada").GetInt32().ShouldBe(0);

        var seguinte = await LerAsync(await ReceberAsync(http, GeradorDeDadosValidos.ProximoCpf()), HttpStatusCode.Created);

        Sequencial(seguinte).ShouldBe(Sequencial(anterior) + 1);
    }

    [Fact]
    public async Task Recepcao_ComPecaRepetida_Responde400()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var pecaId = Guid.CreateVersion7();

        var resposta = await ReceberAsync(http, GeradorDeDadosValidos.ProximoCpf(),
            pecas: [new { pecaId, quantidade = 1 }, new { pecaId, quantidade = 2 }]);

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static int Sequencial(JsonElement os) =>
        int.Parse(os.GetProperty("numero").GetString()!.Split('-')[^1], System.Globalization.CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> ReceberAsync(
        HttpClient http,
        string cpf,
        object[]? servicos = null,
        object[]? pecas = null) =>
        http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = cpf,
            nomeCliente = "Cliente da Abertura Completa",
            emailCliente = GeradorDeDadosValidos.ProximoEmail(),
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Honda",
            modelo = "Civic",
            anoFabricacao = 2021,
            anoModelo = 2022,
            descricaoProblema = "Revisão dos 40 mil km.",
            quilometragemEntrada = 40_000,
            servicos,
            pecas
        }, AmbienteDaApi.Json);

    private static async Task<JsonElement> CriarPecaAsync(HttpClient http, int quantidadeInicial) =>
        await LerAsync(await http.PostAsJsonAsync("/api/v1/pecas", new
        {
            codigo = GeradorDeDadosValidos.ProximoCodigoDePeca(),
            nome = "Peça da abertura completa",
            unidadeMedida = "Unidade",
            precoUnitario = 80.00m,
            quantidadeInicial,
            estoqueMinimo = 0
        }, AmbienteDaApi.Json), HttpStatusCode.Created);

    private static async Task<JsonElement> ObterPrimeiroServicoAsync(HttpClient http) =>
        (await LerAsync(await http.GetAsync("/api/v1/servicos?apenasAtivos=true&tamanhoPagina=1"))).GetProperty("itens")[0];

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        resposta.StatusCode.ShouldBe(esperado, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
