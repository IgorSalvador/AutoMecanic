using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.IntegrationTests.Infraestrutura;

namespace AutoMecanic.IntegrationTests.Fluxos;

/// <summary>
/// Listagem de OS exigida pela Fase 2, verificada contra o PostgreSQL: ordem por prioridade de
/// atendimento (Em execução, Aguardando aprovação, Em diagnóstico, Recebida), mais antigas
/// primeiro dentro de cada situação, e sem as finalizadas, entregues e canceladas.
/// <para>
/// As OS são todas do mesmo cliente e criadas numa ordem que <b>não</b> coincide com a esperada
/// — nem crescente nem decrescente —, para que só a regra de prioridade explique o resultado.
/// </para>
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ListagemPriorizadaTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task Listagem_OrdenaPorPrioridadeEAntiguidadeEOmiteAsEncerradas()
    {
        var (http, clienteId, os) = await MontarFilaAsync();

        var itens = await ListarAsync(http, $"clienteId={clienteId}");

        itens.Select(Id).ShouldBe(
        [
            os["emExecucao"],
            os["aguardando"],
            os["emDiagnostico"],
            os["recebidaAntiga"],
            os["recebidaNova"]
        ]);

        // Sem orçamento, o valor de referência é a soma dos itens já incluídos.
        var emDiagnostico = itens.Single(i => Id(i) == os["emDiagnostico"]);
        emDiagnostico.GetProperty("valorTotal").GetDecimal().ShouldBeGreaterThan(0m);
    }

    [Fact]
    public async Task Listagem_ComEncerradas_TrazFinalizadasECanceladasAoFinal()
    {
        var (http, clienteId, os) = await MontarFilaAsync();

        var itens = await ListarAsync(http, $"clienteId={clienteId}&incluirEncerradas=true");

        itens.Count.ShouldBe(7);
        itens.Select(Id).TakeLast(2).ShouldBe([os["finalizada"], os["cancelada"]]);
    }

    [Fact]
    public async Task Listagem_ComSituacaoEncerradaExplicita_ATrazMesmoSemIncluirEncerradas()
    {
        var (http, clienteId, os) = await MontarFilaAsync();

        var itens = await ListarAsync(http, $"clienteId={clienteId}&status=Cancelada");

        itens.Select(Id).ShouldBe([os["cancelada"]]);
    }

    private static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    private static async Task<List<JsonElement>> ListarAsync(HttpClient http, string filtros)
    {
        var pagina = await LerAsync(await http.GetAsync($"/api/v1/ordens-servico?{filtros}&tamanhoPagina=50"));

        return [.. pagina.GetProperty("itens").EnumerateArray()];
    }

    /// <summary>Uma OS em cada situação, para o mesmo cliente, criadas fora da ordem esperada.</summary>
    private async Task<(HttpClient Http, Guid ClienteId, Dictionary<string, Guid> Os)> MontarFilaAsync()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var cpf = GeradorDeDadosValidos.ProximoCpf();
        var servicoId = (await LerAsync(await http.GetAsync("/api/v1/servicos?apenasAtivos=true&tamanhoPagina=1")))
            .GetProperty("itens")[0].GetProperty("id").GetGuid();

        var os = new Dictionary<string, Guid>();
        Guid clienteId = default;

        async Task<Guid> ReceberAsync(bool comServico)
        {
            var corpo = await LerAsync(await http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
            {
                documentoCliente = cpf,
                nomeCliente = "Cliente da Fila de Atendimento",
                emailCliente = GeradorDeDadosValidos.ProximoEmail(),
                telefoneCliente = "11987654321",
                placa = GeradorDeDadosValidos.ProximaPlaca(),
                marca = "Chevrolet",
                modelo = "Tracker",
                anoFabricacao = 2022,
                descricaoProblema = "Revisão.",
                quilometragemEntrada = 20_000,
                servicos = comServico ? new[] { new { servicoId, quantidade = 1 } } : null
            }, AmbienteDaApi.Json), HttpStatusCode.Created);

            clienteId = corpo.GetProperty("clienteId").GetGuid();

            return corpo.GetProperty("id").GetGuid();
        }

        async Task AcaoAsync(Guid id, string acao, object? corpo = null) =>
            await LerAsync(await http.PostAsJsonAsync($"/api/v1/ordens-servico/{id}/{acao}", corpo ?? new { }, AmbienteDaApi.Json));

        async Task<Guid> ComOrcamentoEnviadoAsync()
        {
            var id = await ReceberAsync(comServico: true);
            await AcaoAsync(id, "orcamento", new { percentualDesconto = 0m });
            await AcaoAsync(id, "orcamento/enviar", new { validadeEmDias = 7 });
            return id;
        }

        os["recebidaAntiga"] = await ReceberAsync(comServico: false);

        os["cancelada"] = await ReceberAsync(comServico: false);
        await AcaoAsync(os["cancelada"], "cancelar", new { motivo = "Cliente desistiu." });

        os["emDiagnostico"] = await ReceberAsync(comServico: true);
        await AcaoAsync(os["emDiagnostico"], "diagnostico/iniciar");

        os["finalizada"] = await ComOrcamentoEnviadoAsync();
        await AcaoAsync(os["finalizada"], "orcamento/aprovar");
        await AcaoAsync(os["finalizada"], "finalizar", new { observacao = "Concluído." });

        os["aguardando"] = await ComOrcamentoEnviadoAsync();

        os["emExecucao"] = await ComOrcamentoEnviadoAsync();
        await AcaoAsync(os["emExecucao"], "orcamento/aprovar");

        os["recebidaNova"] = await ReceberAsync(comServico: false);

        return (http, clienteId, os);
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        resposta.StatusCode.ShouldBe(esperado, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
