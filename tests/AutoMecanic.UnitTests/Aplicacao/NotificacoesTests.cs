using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Notificacoes;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.Events;
using AutoMecanic.Domain.Veiculos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AutoMecanic.UnitTests.Aplicacao;

/// <summary>
/// Quando e o que o cliente recebe por e-mail. O envio em si é da infraestrutura; aqui se
/// verifica a decisão: qual evento gera mensagem, para quem e com qual conteúdo.
/// </summary>
public sealed class NotificacoesTests
{
    private readonly IRepositorioDeClientes _clientes = Substitute.For<IRepositorioDeClientes>();
    private readonly IRepositorioDeVeiculos _veiculos = Substitute.For<IRepositorioDeVeiculos>();
    private readonly ICaixaDeSaidaDeEmails _caixa = Substitute.For<ICaixaDeSaidaDeEmails>();
    private readonly List<EmailDeSaida> _enfileirados = [];
    private readonly Cliente _cliente = Cliente.Cadastrar("Maria Souza", "52998224725", "maria@exemplo.com", "11987654321");
    private readonly NotificarClienteHandler _handler;

    public NotificacoesTests()
    {
        _clientes.ObterPorIdAsync(_cliente.Id, Arg.Any<CancellationToken>()).Returns(_cliente);
        _caixa.EnfileirarAsync(Arg.Do<EmailDeSaida>(_enfileirados.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _handler = new NotificarClienteHandler(
            _clientes,
            _veiculos,
            _caixa,
            Options.Create(new OpcoesDeNotificacao { UrlPublicaDaApi = "https://api.oficina.com.br/", NomeDaOficina = "Oficina Teste" }),
            NullLogger<NotificarClienteHandler>.Instance);
    }

    [Fact]
    public async Task OrdemAberta_AvisaOClienteComNumeroEVeiculo()
    {
        var veiculo = Veiculo.Cadastrar(_cliente.Id, "ABC1D23", "Volkswagen", "Gol", 2020, 2021, "Branco", 50_000);
        _veiculos.ObterPorIdAsync(veiculo.Id, Arg.Any<CancellationToken>()).Returns(veiculo);

        await _handler.TratarAsync(new OrdemDeServicoAberta(Guid.CreateVersion7(), "OS-2026-000042", _cliente.Id, veiculo.Id));

        var email = _enfileirados.ShouldHaveSingleItem();
        email.Destinatario.ShouldBe("maria@exemplo.com");
        email.Referencia.ShouldBe("OS-2026-000042");
        email.Assunto.ShouldContain("OS-2026-000042");
        email.CorpoTexto.ShouldContain("Volkswagen Gol");
    }

    [Theory]
    [InlineData(StatusOrdemServico.EmDiagnostico, "Em diagnóstico")]
    [InlineData(StatusOrdemServico.EmExecucao, "Em execução")]
    [InlineData(StatusOrdemServico.Finalizada, "Finalizada")]
    [InlineData(StatusOrdemServico.Entregue, "Entregue")]
    [InlineData(StatusOrdemServico.Cancelada, "Cancelada")]
    public async Task MudancaDeSituacao_AvisaANovaSituacao(StatusOrdemServico situacao, string descricao)
    {
        await _handler.TratarAsync(new StatusDaOrdemAlterado(Guid.CreateVersion7(), "OS-2026-000042", null, situacao, _cliente.Id));

        _enfileirados.ShouldHaveSingleItem().Assunto.ShouldContain(descricao);
    }

    [Fact]
    public async Task EnvioDoOrcamento_NaoDuplicaOAvisoDeMudancaDeSituacao()
    {
        // A mudança para "aguardando aprovação" é avisada pelo e-mail do orçamento, mais completo.
        await _handler.TratarAsync(new StatusDaOrdemAlterado(
            Guid.CreateVersion7(), "OS-2026-000042", StatusOrdemServico.EmDiagnostico, StatusOrdemServico.AguardandoAprovacao, _cliente.Id));

        _enfileirados.ShouldBeEmpty();
    }

    [Fact]
    public async Task OrcamentoEnviado_TrazValorValidadeEComoResponder()
    {
        var validoAte = new DateTimeOffset(2026, 3, 17, 13, 30, 0, TimeSpan.Zero);

        await _handler.TratarAsync(new OrcamentoEnviadoAoCliente(
            Guid.CreateVersion7(), Guid.CreateVersion7(), _cliente.Id, 1234.5m, validoAte, "OS-2026-000042"));

        var email = _enfileirados.ShouldHaveSingleItem();
        email.Assunto.ShouldContain("aguardando sua aprovação");
        email.CorpoTexto.ShouldContain("R$");
        email.CorpoTexto.ShouldContain("1.234,50");
        email.CorpoTexto.ShouldContain("17/03/2026 às 10:30"); // horário de Brasília
        email.CorpoTexto.ShouldContain("https://api.oficina.com.br/api/v1/acompanhamento/orcamento/aprovar");
    }

    [Fact]
    public async Task ClienteInexistente_NaoRegistraMensagem()
    {
        await _handler.TratarAsync(new StatusDaOrdemAlterado(
            Guid.CreateVersion7(), "OS-2026-000042", null, StatusOrdemServico.Finalizada, Guid.CreateVersion7()));

        _enfileirados.ShouldBeEmpty();
    }

    [Fact]
    public void Modelos_CodificamDadosDeCadastroNoHtml()
    {
        var conteudo = ModelosDeEmailDaOrdemServico.OrdemAberta("<script>alert(1)</script>", "OS-2026-000001", "Gol", "Oficina");

        conteudo.CorpoHtml.ShouldNotContain("<script>");
        conteudo.CorpoHtml.ShouldContain("&lt;script&gt;");
    }
}
