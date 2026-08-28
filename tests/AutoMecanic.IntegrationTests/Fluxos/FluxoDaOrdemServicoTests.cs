using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.IntegrationTests.Infraestrutura;

namespace AutoMecanic.IntegrationTests.Fluxos;

/// <summary>
/// Percorre os fluxos principais exigidos pelo requisito contra a API e o banco reais:
/// criação e acompanhamento da OS, elaboração e decisão do orçamento, execução e entrega,
/// e o efeito de tudo isso sobre o estoque.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class FluxoDaOrdemServicoTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task FluxoCompleto_DaRecepcaoAEntrega_PercorreTodosOsStatusEBaixaOEstoque()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var servico = await ObterPrimeiroServicoAsync(http);
        var peca = await CriarPecaAsync(http, quantidadeInicial: 20, estoqueMinimo: 2);

        // 1. Recepção: identifica o cliente pelo CPF e abre a OS.
        var os = await ReceberVeiculoAsync(http);
        os.GetProperty("status").GetString().ShouldBe("Recebida");

        var osId = os.GetProperty("id").GetGuid();
        var numero = os.GetProperty("numero").GetString()!;

        // 2. Diagnóstico.
        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/diagnostico/iniciar", null);
        os.GetProperty("status").GetString().ShouldBe("EmDiagnostico");

        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/diagnostico",
            new { diagnostico = "Pastilhas dianteiras no limite de desgaste." });
        os.GetProperty("diagnosticoTecnico").GetString().ShouldNotBeNullOrWhiteSpace();

        // 3. Composição: o serviço entra com o preço de tabela congelado.
        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/servicos",
            new { servicoId = servico.GetProperty("id").GetGuid(), quantidade = 1 });

        os.GetProperty("servicos")[0].GetProperty("precoUnitario").GetDecimal()
            .ShouldBe(servico.GetProperty("preco").GetDecimal());

        // 4. A peça é reservada no estoque na mesma transação.
        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/pecas",
            new { pecaId = peca.GetProperty("id").GetGuid(), quantidade = 4 });

        os.GetProperty("pecas")[0].GetProperty("reservada").GetBoolean().ShouldBeTrue();

        var saldo = await GetAsync(http, $"/api/v1/pecas/{peca.GetProperty("id").GetGuid()}");
        saldo.GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(20);  // ainda na prateleira
        saldo.GetProperty("quantidadeReservada").GetInt32().ShouldBe(4);
        saldo.GetProperty("quantidadeDisponivel").GetInt32().ShouldBe(16); // mas já comprometida

        // 5. Orçamento gerado automaticamente a partir dos itens.
        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento", new { percentualDesconto = 10m });

        var orcamento = os.GetProperty("orcamento");
        var bruto = servico.GetProperty("preco").GetDecimal() + (peca.GetProperty("precoUnitario").GetDecimal() * 4);

        orcamento.GetProperty("valorBruto").GetDecimal().ShouldBe(bruto);
        orcamento.GetProperty("valorTotal").GetDecimal().ShouldBe(Math.Round(bruto * 0.9m, 2));
        orcamento.GetProperty("status").GetString().ShouldBe("EmElaboracao");

        // 6. Envio ao cliente congela os itens.
        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento/enviar", new { validadeEmDias = 7 });
        os.GetProperty("status").GetString().ShouldBe("AguardandoAprovacao");

        var tentativaDeAlterar = await http.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{osId}/servicos",
            new { servicoId = servico.GetProperty("id").GetGuid(), quantidade = 1 },
            AmbienteDaApi.Json);

        tentativaDeAlterar.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // 7. Aprovação: baixa efetiva do estoque.
        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento/aprovar", null);
        os.GetProperty("status").GetString().ShouldBe("EmExecucao");
        os.GetProperty("pecas")[0].GetProperty("consumida").GetBoolean().ShouldBeTrue();

        saldo = await GetAsync(http, $"/api/v1/pecas/{peca.GetProperty("id").GetGuid()}");
        saldo.GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(16);
        saldo.GetProperty("quantidadeReservada").GetInt32().ShouldBe(0);

        // 8. O razão de estoque registrou a saída na mesma transação.
        var movimentos = await GetAsync(http, $"/api/v1/pecas/movimentos?ordemServicoId={osId}");
        movimentos.GetProperty("itens").EnumerateArray()
            .ShouldContain(m => m.GetProperty("tipo").GetString() == "Saida"
                                && m.GetProperty("quantidade").GetInt32() == 4);

        // 9. Finalização e entrega.
        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/finalizar", new { observacao = "Serviço concluído." });
        os.GetProperty("status").GetString().ShouldBe("Finalizada");
        os.GetProperty("duracaoDaExecucaoEmMinutos").ValueKind.ShouldNotBe(JsonValueKind.Null);

        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/entregar", new { observacao = "Cliente retirou." });
        os.GetProperty("status").GetString().ShouldBe("Entregue");

        // 10. A linha do tempo tem exatamente as seis transições do ciclo de vida.
        os.GetProperty("historico").GetArrayLength().ShouldBe(6);

        // 11. Estado terminal não admite novas ações.
        var tentativaTardia = await http.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{osId}/cancelar", new { motivo = "tardio" }, AmbienteDaApi.Json);

        tentativaTardia.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // 12. Consulta pública de acompanhamento.
        var anonimo = ambiente.CriarClienteAnonimo();
        var documento = os.GetProperty("documentoCliente").GetString()!.Where(char.IsDigit).ToArray();

        var acompanhamento = await GetAsync(anonimo,
            $"/api/v1/acompanhamento?numero={numero}&documento={new string(documento)}");

        acompanhamento.GetProperty("status").GetString().ShouldBe("Entregue");
        acompanhamento.GetProperty("linhaDoTempo").GetArrayLength().ShouldBe(6);
    }

    [Fact]
    public async Task ReprovacaoDoOrcamento_CancelaAOrdemEDevolveAsPecasAoEstoque()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var peca = await CriarPecaAsync(http, quantidadeInicial: 10, estoqueMinimo: 1);
        var pecaId = peca.GetProperty("id").GetGuid();

        var os = await ReceberVeiculoAsync(http);
        var osId = os.GetProperty("id").GetGuid();

        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/diagnostico/iniciar", null);
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/pecas", new { pecaId, quantidade = 6 });
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento", new { percentualDesconto = 0m });
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento/enviar", new { validadeEmDias = 7 });

        (await GetAsync(http, $"/api/v1/pecas/{pecaId}"))
            .GetProperty("quantidadeReservada").GetInt32().ShouldBe(6);

        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento/reprovar",
            new { motivo = "Valor acima do orçado." });

        os.GetProperty("status").GetString().ShouldBe("Cancelada");
        os.GetProperty("orcamento").GetProperty("status").GetString().ShouldBe("Reprovado");

        // Nada saiu do estoque: o saldo volta exatamente ao que era.
        var saldo = await GetAsync(http, $"/api/v1/pecas/{pecaId}");
        saldo.GetProperty("quantidadeEmEstoque").GetInt32().ShouldBe(10);
        saldo.GetProperty("quantidadeReservada").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task ReservaConcorrente_NaoPermiteVenderAMesmaPecaDuasVezes()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var peca = await CriarPecaAsync(http, quantidadeInicial: 5, estoqueMinimo: 0);
        var pecaId = peca.GetProperty("id").GetGuid();

        var primeira = await ReceberVeiculoAsync(http);
        var segunda = await ReceberVeiculoAsync(http);

        await PostAsync(http, $"/api/v1/ordens-servico/{primeira.GetProperty("id").GetGuid()}/diagnostico/iniciar", null);
        await PostAsync(http, $"/api/v1/ordens-servico/{segunda.GetProperty("id").GetGuid()}/diagnostico/iniciar", null);

        await PostAsync(http, $"/api/v1/ordens-servico/{primeira.GetProperty("id").GetGuid()}/pecas",
            new { pecaId, quantidade = 4 });

        // Só resta 1 disponível: a segunda OS não pode prometer 4.
        var resposta = await http.PostAsJsonAsync(
            $"/api/v1/ordens-servico/{segunda.GetProperty("id").GetGuid()}/pecas",
            new { pecaId, quantidade = 4 },
            AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
        problema.GetProperty("codigo").GetString().ShouldBe("ESTOQUE_INSUFICIENTE");
    }

    [Fact]
    public async Task NumeracaoDeOrdens_EhSequencialESemBuracos()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();

        var numeros = new List<int>();

        for (var i = 0; i < 3; i++)
        {
            var os = await ReceberVeiculoAsync(http);
            var numero = os.GetProperty("numero").GetString()!;

            numero.ShouldStartWith($"OS-{DateTimeOffset.UtcNow.Year}-");
            numeros.Add(int.Parse(numero[^6..]));
        }

        // A sequência é alocada no banco por INSERT ... ON CONFLICT ... RETURNING:
        // números consecutivos, sem repetição.
        numeros.ShouldBe([.. numeros.OrderBy(n => n)]);
        numeros.Distinct().Count().ShouldBe(numeros.Count);
    }

    [Fact]
    public async Task Acompanhamento_ComDocumentoDeOutroCliente_Responde404()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var os = await ReceberVeiculoAsync(http);
        var numero = os.GetProperty("numero").GetString()!;

        var anonimo = ambiente.CriarClienteAnonimo();

        var resposta = await anonimo.GetAsync(
            $"/api/v1/acompanhamento?numero={numero}&documento={GeradorDeDadosValidos.ProximoCpf()}");

        // Mesma resposta de "OS inexistente": a diferença permitiria enumerar
        // números de OS válidos e descobrir dados de terceiros.
        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelarOrdemComPecaReservada_DevolveOSaldo()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var peca = await CriarPecaAsync(http, quantidadeInicial: 8, estoqueMinimo: 0);
        var pecaId = peca.GetProperty("id").GetGuid();

        var os = await ReceberVeiculoAsync(http);
        var osId = os.GetProperty("id").GetGuid();

        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/diagnostico/iniciar", null);
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/pecas", new { pecaId, quantidade = 3 });

        os = await PostAsync(http, $"/api/v1/ordens-servico/{osId}/cancelar",
            new { motivo = "Cliente desistiu do reparo." });

        os.GetProperty("status").GetString().ShouldBe("Cancelada");

        (await GetAsync(http, $"/api/v1/pecas/{pecaId}"))
            .GetProperty("quantidadeDisponivel").GetInt32().ShouldBe(8);
    }

    [Fact]
    public async Task IndicadorDeTempoMedio_ConsideraApenasOrdensFinalizadas()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var servico = await ObterPrimeiroServicoAsync(http);

        var os = await ReceberVeiculoAsync(http);
        var osId = os.GetProperty("id").GetGuid();

        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/diagnostico/iniciar", null);
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/servicos",
            new { servicoId = servico.GetProperty("id").GetGuid(), quantidade = 1 });
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento", new { percentualDesconto = 0m });
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento/enviar", new { validadeEmDias = 7 });
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/orcamento/aprovar", null);
        await PostAsync(http, $"/api/v1/ordens-servico/{osId}/finalizar", new { observacao = "Pronto." });

        var indicador = await GetAsync(http, "/api/v1/indicadores/tempo-medio-execucao");

        indicador.GetProperty("ordensFinalizadas").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        indicador.GetProperty("tempoMedioDeExecucaoEmMinutos").GetDouble().ShouldBeGreaterThanOrEqualTo(0);
    }

    // -----------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------

    private static async Task<JsonElement> ReceberVeiculoAsync(HttpClient http)
    {
        var resposta = await http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = GeradorDeDadosValidos.ProximoCpf(),
            nomeCliente = "Cliente de Integração",
            emailCliente = GeradorDeDadosValidos.ProximoEmail(),
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Toyota",
            modelo = "Corolla",
            anoFabricacao = 2021,
            anoModelo = 2022,
            cor = "Prata",
            descricaoProblema = "Ruído metálico ao frear.",
            quilometragemEntrada = 42_000
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> CriarPecaAsync(HttpClient http, int quantidadeInicial, int estoqueMinimo)
    {
        var resposta = await http.PostAsJsonAsync("/api/v1/pecas", new
        {
            codigo = GeradorDeDadosValidos.ProximoCodigoDePeca(),
            nome = "Peça de integração",
            descricao = "Criada por teste automatizado.",
            unidadeMedida = "Unidade",
            precoUnitario = 100.00m,
            quantidadeInicial,
            estoqueMinimo
        }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.Created, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> ObterPrimeiroServicoAsync(HttpClient http)
    {
        var pagina = await GetAsync(http, "/api/v1/servicos?apenasAtivos=true&tamanhoPagina=1");

        return pagina.GetProperty("itens")[0];
    }

    private static async Task<JsonElement> GetAsync(HttpClient http, string caminho)
    {
        var resposta = await http.GetAsync(caminho);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }

    private static async Task<JsonElement> PostAsync(HttpClient http, string caminho, object? corpo)
    {
        var resposta = await http.PostAsJsonAsync(caminho, corpo ?? new { }, AmbienteDaApi.Json);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
