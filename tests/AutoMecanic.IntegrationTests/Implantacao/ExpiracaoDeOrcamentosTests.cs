using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoMecanic.Api;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Infrastructure.Persistencia;
using AutoMecanic.IntegrationTests.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AutoMecanic.IntegrationTests.Implantacao;

/// <summary>
/// O modo <c>--expirar-orcamentos</c> é o comando do CronJob de manutenção. Estes testes
/// vencem um orçamento de verdade — adiantando o relógio da aplicação — e conferem, no banco,
/// que a OS foi cancelada e a peça reservada voltou ao estoque disponível.
/// </summary>
[Collection(ColecaoDeIntegracao.Nome)]
public sealed class ExpiracaoDeOrcamentosTests(AmbienteDaApi ambiente)
{
    [Fact]
    public async Task ExpirarOrcamentos_ComOrcamentoVencido_CancelaAOSEDevolveAReserva()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var (ordemId, pecaId) = await CriarOrdemComOrcamentoEnviadoAsync(http, quantidadeDePeca: 3, validadeEmDias: 1);

        (await ObterPecaAsync(pecaId)).QuantidadeReservada.ShouldBe(3);

        int codigoDeSaida;

        using (ambiente.Relogio.Adiantar(TimeSpan.FromDays(2)))
        {
            codigoDeSaida = await ExecutarTarefaAsync();
        }

        codigoDeSaida.ShouldBe(0);

        var ordem = await ObterOrdemAsync(ordemId);
        ordem.Status.ShouldBe(StatusOrdemServico.Cancelada);
        ordem.Orcamento!.Status.ShouldBe(StatusOrcamento.Expirado);

        var peca = await ObterPecaAsync(pecaId);
        peca.QuantidadeReservada.ShouldBe(0);
        peca.QuantidadeDisponivel.ShouldBe(peca.QuantidadeEmEstoque);
    }

    [Fact]
    public async Task ExpirarOrcamentos_ComOrcamentoDentroDaValidade_NaoAlteraNada()
    {
        var http = await ambiente.CriarClienteAutenticadoAsync();
        var (ordemId, pecaId) = await CriarOrdemComOrcamentoEnviadoAsync(http, quantidadeDePeca: 2, validadeEmDias: 7);

        int codigoDeSaida;

        // Um dia depois, ainda faltam seis para o prazo vencer.
        using (ambiente.Relogio.Adiantar(TimeSpan.FromDays(1)))
        {
            codigoDeSaida = await ExecutarTarefaAsync();
        }

        codigoDeSaida.ShouldBe(0);
        (await ObterOrdemAsync(ordemId)).Status.ShouldBe(StatusOrdemServico.AguardandoAprovacao);
        (await ObterPecaAsync(pecaId)).QuantidadeReservada.ShouldBe(2);
    }

    [Fact]
    public async Task ExpirarOrcamentos_ComBancoInacessivel_SaiComCodigoDeFalha()
    {
        // Sem banco, o CronJob precisa enxergar a falha pelo código de saída.
        var configuracao = new ConfigurationBuilder().Build();
        var servicos = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        (await TarefasAvulsas.ExecutarAsync(ModoDeExecucao.ExpirarOrcamentos, servicos, configuracao)).ShouldBe(1);
    }

    private Task<int> ExecutarTarefaAsync() =>
        TarefasAvulsas.ExecutarAsync(
            ModoDeExecucao.ExpirarOrcamentos,
            ambiente.Services,
            ambiente.Services.GetRequiredService<IConfiguration>());

    private async Task<OrdemServico> ObterOrdemAsync(Guid id)
    {
        using var escopo = ambiente.Services.CreateScope();

        return await escopo.ServiceProvider.GetRequiredService<AutoMecanicDbContext>()
            .OrdensDeServico.AsNoTracking().SingleAsync(o => o.Id == id);
    }

    private async Task<Domain.Estoque.Peca> ObterPecaAsync(Guid id)
    {
        using var escopo = ambiente.Services.CreateScope();

        return await escopo.ServiceProvider.GetRequiredService<AutoMecanicDbContext>()
            .Pecas.AsNoTracking().SingleAsync(p => p.Id == id);
    }

    private static async Task<(Guid OrdemId, Guid PecaId)> CriarOrdemComOrcamentoEnviadoAsync(
        HttpClient http,
        int quantidadeDePeca,
        int validadeEmDias)
    {
        var servicos = await LerAsync(await http.GetAsync("/api/v1/servicos?apenasAtivos=true&tamanhoPagina=1"));
        var servicoId = servicos.GetProperty("itens")[0].GetProperty("id").GetGuid();

        var peca = await LerAsync(await http.PostAsJsonAsync("/api/v1/pecas", new
        {
            codigo = GeradorDeDadosValidos.ProximoCodigoDePeca(),
            nome = "Peça para expiração",
            unidadeMedida = "Unidade",
            precoUnitario = 50.00m,
            quantidadeInicial = 10,
            estoqueMinimo = 1
        }, AmbienteDaApi.Json), HttpStatusCode.Created);

        var pecaId = peca.GetProperty("id").GetGuid();

        var ordem = await LerAsync(await http.PostAsJsonAsync("/api/v1/ordens-servico/recepcao", new
        {
            documentoCliente = GeradorDeDadosValidos.ProximoCpf(),
            nomeCliente = "Cliente da Expiração",
            emailCliente = GeradorDeDadosValidos.ProximoEmail(),
            telefoneCliente = "11987654321",
            placa = GeradorDeDadosValidos.ProximaPlaca(),
            marca = "Fiat",
            modelo = "Argo",
            anoFabricacao = 2022,
            anoModelo = 2023,
            descricaoProblema = "Revisão com troca de peças.",
            quilometragemEntrada = 30_000
        }, AmbienteDaApi.Json), HttpStatusCode.Created);

        var ordemId = ordem.GetProperty("id").GetGuid();
        var base_ = $"/api/v1/ordens-servico/{ordemId}";

        await LerAsync(await http.PostAsJsonAsync($"{base_}/diagnostico/iniciar", new { }, AmbienteDaApi.Json));
        await LerAsync(await http.PostAsJsonAsync($"{base_}/servicos", new { servicoId, quantidade = 1 }, AmbienteDaApi.Json));
        await LerAsync(await http.PostAsJsonAsync($"{base_}/pecas", new { pecaId, quantidade = quantidadeDePeca }, AmbienteDaApi.Json));
        await LerAsync(await http.PostAsJsonAsync($"{base_}/orcamento", new { percentualDesconto = 0m }, AmbienteDaApi.Json));
        await LerAsync(await http.PostAsJsonAsync($"{base_}/orcamento/enviar", new { validadeEmDias }, AmbienteDaApi.Json));

        return (ordemId, pecaId);
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        resposta.StatusCode.ShouldBe(esperado, await resposta.Content.ReadAsStringAsync());

        return await resposta.Content.ReadFromJsonAsync<JsonElement>(AmbienteDaApi.Json);
    }
}
