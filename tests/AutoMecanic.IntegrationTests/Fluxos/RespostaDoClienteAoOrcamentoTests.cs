using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.IntegrationTests.Infraestrutura;

namespace AutoMecanic.IntegrationTests.Fluxos;

/// <summary>
/// Aprovação e recusa do orçamento pelo próprio cliente, exigidas pela Fase 2: rotas públicas
/// protegidas pelo par número da OS + CPF/CNPJ, com os mesmos efeitos sobre o estoque que a
/// decisão registrada pelo atendente.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class RespostaDoClienteAoOrcamentoTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task ClienteAprova_ComNumeroEDocumento_IniciaExecucaoEBaixaAsPecas()
    {
        var (numero, cpf, pecaId) = await PrepararOrcamentoEnviadoAsync(quantidadeDePeca: 3);
        var cliente = ambiente.CriarClienteAnonimo();

        var aprovacao = await cliente.PostAsJsonAsync("/api/v1/acompanhamento/orcamento/aprovar",
            new { numero, documento = cpf }, AmbienteDaApi.Json);

        var corpo = await LerAsync(aprovacao);
        corpo.GetProperty("status").GetString().ShouldBe("EmExecucao");
        corpo.GetProperty("situacaoOrcamento").GetString().ShouldBe("Aprovado");

        // Visão pública: sem identificadores internos nem dados de custo por peça.
        corpo.TryGetProperty("id", out _).ShouldBeFalse();
        corpo.TryGetProperty("pecas", out _).ShouldBeFalse();

        var saldo = await SaldoAsync(pecaId);
        saldo.GetProperty("quantidadeReservada").GetInt32().ShouldBe(0);
        saldo.GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(7);

        // Responder de novo não altera nada.
        (await cliente.PostAsJsonAsync("/api/v1/acompanhamento/orcamento/aprovar",
                new { numero, documento = cpf }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ClienteRecusa_CancelaAOSEDevolveAsReservas()
    {
        var (numero, cpf, pecaId) = await PrepararOrcamentoEnviadoAsync(quantidadeDePeca: 2);

        var recusa = await ambiente.CriarClienteAnonimo().PostAsJsonAsync("/api/v1/acompanhamento/orcamento/reprovar",
            new { numero, documento = cpf, motivo = "Encontrei preço melhor." }, AmbienteDaApi.Json);

        var corpo = await LerAsync(recusa);
        corpo.GetProperty("status").GetString().ShouldBe("Cancelada");
        corpo.GetProperty("situacaoOrcamento").GetString().ShouldBe("Reprovado");

        var saldo = await SaldoAsync(pecaId);
        saldo.GetProperty("quantidadeReservada").GetInt32().ShouldBe(0);
        saldo.GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(10);
    }

    [Fact]
    public async Task DocumentoDeOutraPessoa_RespondeIgualAOSInexistente()
    {
        var (numero, _, pecaId) = await PrepararOrcamentoEnviadoAsync(quantidadeDePeca: 1);
        var cliente = ambiente.CriarClienteAnonimo();

        var documentoErrado = await cliente.PostAsJsonAsync("/api/v1/acompanhamento/orcamento/aprovar",
            new { numero, documento = GeradorDeDadosValidos.ProximoCpf() }, AmbienteDaApi.Json);

        var numeroInexistente = await cliente.PostAsJsonAsync("/api/v1/acompanhamento/orcamento/aprovar",
            new { numero = "OS-2099-999999", documento = GeradorDeDadosValidos.ProximoCpf() }, AmbienteDaApi.Json);

        documentoErrado.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        numeroInexistente.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await Detalhe(documentoErrado)).ShouldBe(await Detalhe(numeroInexistente));

        // E a reserva continua de pé.
        (await SaldoAsync(pecaId)).GetProperty("quantidadeReservada").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("OS-123", "52998224725")]
    [InlineData("OS-2026-000001", "00000000000")]
    public async Task DadosEmFormatoInvalido_Respondem400(string numero, string documento) =>
        (await ambiente.CriarClienteAnonimo().PostAsJsonAsync("/api/v1/acompanhamento/orcamento/aprovar",
                new { numero, documento }, AmbienteDaApi.Json))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    private async Task<(string Numero, string Cpf, Guid PecaId)> PrepararOrcamentoEnviadoAsync(int quantidadeDePeca)
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var cpf = GeradorDeDadosValidos.ProximoCpf();

        var servicos = await LerAsync(await http.GetAsync("/api/v1/servicos?apenasAtivos=true&tamanhoPagina=1"));
        var peca = await LerAsync(await http.PostAsJsonAsync("/api/v1/pecas", new
        {
            codigo = GeradorDeDadosValidos.ProximoCodigoDePeca(),
            nome = "Peça da resposta do cliente",
            unidadeMedida = "Unidade",
            precoUnitario = 60.00m,
            quantidadeInicial = 10,
            estoqueMinimo = 0
        }, AmbienteDaApi.Json), HttpStatusCode.Created);

        var pecaId = peca.GetProperty("id").GetGuid();

        var os = await LerAsync(await http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = cpf,
            nomeCliente = "Cliente que responde o orçamento",
            emailCliente = GeradorDeDadosValidos.ProximoEmail(),
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Jeep",
            modelo = "Renegade",
            anoFabricacao = 2022,
            descricaoProblema = "Ruído na suspensão dianteira.",
            quilometragemEntrada = 50_000,
            servicos = new[] { new { servicoId = servicos.GetProperty("itens")[0].GetProperty("id").GetGuid(), quantidade = 1 } },
            pecas = new[] { new { pecaId, quantidade = quantidadeDePeca } }
        }, AmbienteDaApi.Json), HttpStatusCode.Created);

        var id = os.GetProperty("id").GetGuid();

        await LerAsync(await http.PostAsJsonAsync($"/api/v1/ordens-servico/{id}/orcamento", new { percentualDesconto = 0m }, AmbienteDaApi.Json));
        await LerAsync(await http.PostAsJsonAsync($"/api/v1/ordens-servico/{id}/orcamento/enviar", new { validadeEmDias = 7 }, AmbienteDaApi.Json));

        return (os.GetProperty("numero").GetString()!, cpf, pecaId);
    }

    private async Task<JsonElement> SaldoAsync(Guid pecaId)
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        return await LerAsync(await http.GetAsync($"/api/v1/pecas/{pecaId}"));
    }

    private static async Task<string?> Detalhe(HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json)).GetProperty("detail").GetString();

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        resposta.StatusCode.ShouldBe(esperado, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
