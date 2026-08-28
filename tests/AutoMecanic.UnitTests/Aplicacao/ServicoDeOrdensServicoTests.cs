using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.Servicos;
using AutoMecanic.Domain.Veiculos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AutoMecanic.UnitTests.Aplicacao;

/// <summary>
/// A camada de aplicação não contém regra de negócio, mas é ela que coordena Ordem de
/// Serviço e Estoque. Estes testes verificam justamente essa costura: reservar ao incluir a
/// peça, consumir ao aprovar, devolver ao reprovar ou cancelar.
/// </summary>
public sealed class ServicoDeOrdensServicoTests
{
    private const string CpfValido = "52998224725";

    private readonly IRepositorioDeOrdensServico _ordens = Substitute.For<IRepositorioDeOrdensServico>();
    private readonly IRepositorioDeClientes _clientes = Substitute.For<IRepositorioDeClientes>();
    private readonly IRepositorioDeVeiculos _veiculos = Substitute.For<IRepositorioDeVeiculos>();
    private readonly IRepositorioDeServicos _servicos = Substitute.For<IRepositorioDeServicos>();
    private readonly IRepositorioDePecas _pecas = Substitute.For<IRepositorioDePecas>();
    private readonly IGeradorDeNumeroDeOrdemServico _gerador = Substitute.For<IGeradorDeNumeroDeOrdemServico>();
    private readonly IUsuarioAtual _usuarioAtual = Substitute.For<IUsuarioAtual>();
    private readonly IProvedorDeDataHora _relogio = Substitute.For<IProvedorDeDataHora>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly ServicoDeOrdensServico _servico;

    public ServicoDeOrdensServicoTests()
    {
        _relogio.Agora.Returns(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
        _gerador.ProximoSequencialAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1);
        _usuarioAtual.Id.Returns(Guid.CreateVersion7());

        // A transação é transparente para o caso de uso: o dublê apenas executa a operação.
        _unitOfWork
            .ExecutarEmTransacaoAsync(Arg.Any<Func<CancellationToken, Task<OrdemServicoResponse>>>(), Arg.Any<CancellationToken>())
            .Returns(chamada => chamada.Arg<Func<CancellationToken, Task<OrdemServicoResponse>>>()(CancellationToken.None));

        _servico = new ServicoDeOrdensServico(
            _ordens, _clientes, _veiculos, _servicos, _pecas,
            _gerador, _usuarioAtual, _relogio, _unitOfWork,
            NullLogger<ServicoDeOrdensServico>.Instance);
    }

    // -----------------------------------------------------------------
    // Abertura
    // -----------------------------------------------------------------

    [Fact]
    public async Task AbrirAsync_ComClienteEVeiculoValidos_AbreAOrdem()
    {
        var (cliente, veiculo) = ClienteComVeiculo();

        var resposta = await _servico.AbrirAsync(
            new AbrirOrdemServicoRequest(cliente.Id, veiculo.Id, "Barulho ao frear", 50_000));

        resposta.Numero.ShouldBe("OS-2026-000001");
        resposta.Status.ShouldBe(StatusOrdemServico.Recebida);
        resposta.NomeCliente.ShouldBe(cliente.Nome);

        await _ordens.Received(1).AdicionarAsync(Arg.Any<OrdemServico>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbrirAsync_ComClienteInexistente_LancaNaoEncontrado()
    {
        _clientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        await Should.ThrowAsync<RecursoNaoEncontradoException>(() =>
            _servico.AbrirAsync(new AbrirOrdemServicoRequest(Guid.CreateVersion7(), Guid.CreateVersion7(), "Problema")));
    }

    [Fact]
    public async Task AbrirAsync_ComVeiculoDeOutroCliente_LancaConflito()
    {
        var (cliente, _) = ClienteComVeiculo();
        var veiculoDeTerceiro = Veiculo.Cadastrar(Guid.CreateVersion7(), "XYZ4567", "Fiat", "Argo", 2022);

        _veiculos.ObterPorIdAsync(veiculoDeTerceiro.Id, Arg.Any<CancellationToken>()).Returns(veiculoDeTerceiro);

        // Abrir uma OS para o carro de outra pessoa é o tipo de erro que só o
        // cruzamento entre agregados detecta.
        await Should.ThrowAsync<ConflitoException>(() =>
            _servico.AbrirAsync(new AbrirOrdemServicoRequest(cliente.Id, veiculoDeTerceiro.Id, "Problema")));
    }

    [Fact]
    public async Task ReceberVeiculoAsync_ComClienteNovo_CadastraClienteEVeiculo()
    {
        _clientes.ObterPorDocumentoAsync(Arg.Any<Domain.Clientes.ValueObjects.Documento>(), Arg.Any<CancellationToken>())
            .Returns((Cliente?)null);
        _veiculos.ObterPorPlacaAsync(Arg.Any<Domain.Veiculos.ValueObjects.Placa>(), Arg.Any<CancellationToken>())
            .Returns((Veiculo?)null);

        var resposta = await _servico.ReceberVeiculoAsync(new ReceberVeiculoRequest(
            CpfValido, "Maria Souza", "maria@exemplo.com", "11987654321",
            "ABC1D23", "Volkswagen", "Gol", 2020, 2021, "Branco",
            "Barulho ao frear", 50_000));

        resposta.Status.ShouldBe(StatusOrdemServico.Recebida);

        await _clientes.Received(1).AdicionarAsync(Arg.Any<Cliente>(), Arg.Any<CancellationToken>());
        await _veiculos.Received(1).AdicionarAsync(Arg.Any<Veiculo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReceberVeiculoAsync_ComVeiculoNovoSemAno_ExigeOsDadosDoVeiculo()
    {
        _clientes.ObterPorDocumentoAsync(Arg.Any<Domain.Clientes.ValueObjects.Documento>(), Arg.Any<CancellationToken>())
            .Returns((Cliente?)null);
        _veiculos.ObterPorPlacaAsync(Arg.Any<Domain.Veiculos.ValueObjects.Placa>(), Arg.Any<CancellationToken>())
            .Returns((Veiculo?)null);

        await Should.ThrowAsync<ValidacaoException>(() =>
            _servico.ReceberVeiculoAsync(new ReceberVeiculoRequest(
                CpfValido, "Maria Souza", "maria@exemplo.com", "11987654321",
                "ABC1D23", null, null, null, null, null, "Barulho", null)));
    }

    // -----------------------------------------------------------------
    // Coordenação com o estoque
    // -----------------------------------------------------------------

    [Fact]
    public async Task AdicionarPecaAsync_ReservaNoEstoqueEMarcaOItem()
    {
        var ordem = OrdemEmDiagnostico();
        var peca = Peca.Cadastrar("OL-5W30", "Óleo 5W30", null, UnidadeMedida.Litro, 48.90m, 10, 2);

        _ordens.ObterCompletaPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        var resposta = await _servico.AdicionarPecaAsync(ordem.Id, new AdicionarPecaRequest(peca.Id, 4));

        peca.QuantidadeReservada.ShouldBe(4);
        peca.QuantidadeDisponivel.ShouldBe(6);
        peca.QuantidadeEmEstoque.ShouldBe(10);

        resposta.Pecas.ShouldHaveSingleItem().Reservada.ShouldBeTrue();
    }

    [Fact]
    public async Task AdicionarPecaAsync_SemSaldoDisponivel_NaoAlteraAOrdem()
    {
        var ordem = OrdemEmDiagnostico();
        var peca = Peca.Cadastrar("OL-5W30", "Óleo 5W30", null, UnidadeMedida.Litro, 48.90m, 2, 0);

        _ordens.ObterCompletaPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        await Should.ThrowAsync<Domain.Abstractions.DomainException>(() =>
            _servico.AdicionarPecaAsync(ordem.Id, new AdicionarPecaRequest(peca.Id, 5)));

        // A reserva acontece antes de tocar na OS: a falha deixa os dois agregados intactos.
        ordem.ItensPeca.ShouldBeEmpty();
        peca.QuantidadeReservada.ShouldBe(0);
    }

    [Fact]
    public async Task RemoverPecaAsync_DevolveAReservaAoEstoque()
    {
        var ordem = OrdemEmDiagnostico();
        var peca = Peca.Cadastrar("OL-5W30", "Óleo 5W30", null, UnidadeMedida.Litro, 48.90m, 10, 0);

        _ordens.ObterCompletaPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        await _servico.AdicionarPecaAsync(ordem.Id, new AdicionarPecaRequest(peca.Id, 3));
        var itemId = ordem.ItensPeca.First().Id;

        await _servico.RemoverPecaAsync(ordem.Id, itemId);

        peca.QuantidadeReservada.ShouldBe(0);
        peca.QuantidadeDisponivel.ShouldBe(10);
        ordem.ItensPeca.ShouldBeEmpty();
    }

    [Fact]
    public async Task AprovarOrcamentoAsync_ConsomeAsPecasReservadas()
    {
        var (ordem, peca) = OrdemComOrcamentoEnviado();

        await _servico.AprovarOrcamentoAsync(ordem.Id);

        ordem.Status.ShouldBe(StatusOrdemServico.EmExecucao);

        // A aprovação é o momento em que a peça sai fisicamente da prateleira.
        peca.QuantidadeEmEstoque.ShouldBe(7);
        peca.QuantidadeReservada.ShouldBe(0);
        ordem.ItensPeca.First().Consumida.ShouldBeTrue();
    }

    [Fact]
    public async Task ReprovarOrcamentoAsync_DevolveAsReservasECancelaAOrdem()
    {
        var (ordem, peca) = OrdemComOrcamentoEnviado();

        await _servico.ReprovarOrcamentoAsync(ordem.Id, new ReprovarOrcamentoRequest("Valor alto"));

        ordem.Status.ShouldBe(StatusOrdemServico.Cancelada);

        // Nada saiu do estoque: o saldo volta ao que era antes do orçamento.
        peca.QuantidadeEmEstoque.ShouldBe(10);
        peca.QuantidadeReservada.ShouldBe(0);
        ordem.ItensPeca.First().Reservada.ShouldBeFalse();
    }

    [Fact]
    public async Task CancelarAsync_DevolveAsReservasAoEstoque()
    {
        var ordem = OrdemEmDiagnostico();
        var peca = Peca.Cadastrar("OL-5W30", "Óleo 5W30", null, UnidadeMedida.Litro, 48.90m, 10, 0);

        _ordens.ObterCompletaPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);
        _pecas.ObterPorIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([peca]);

        await _servico.AdicionarPecaAsync(ordem.Id, new AdicionarPecaRequest(peca.Id, 3));

        await _servico.CancelarAsync(ordem.Id, new CancelarOrdemServicoRequest("Cliente desistiu"));

        ordem.Status.ShouldBe(StatusOrdemServico.Cancelada);
        peca.QuantidadeReservada.ShouldBe(0);
    }

    // -----------------------------------------------------------------
    // Catálogo
    // -----------------------------------------------------------------

    [Fact]
    public async Task AdicionarServicoAsync_CopiaPrecoETempoDoCatalogo()
    {
        var ordem = OrdemEmDiagnostico();
        var servicoCatalogo = Servico.Cadastrar("Troca de óleo", null, CategoriaServico.ManutencaoPreventiva, 120m, 45);

        _ordens.ObterCompletaPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        _servicos.ObterPorIdAsync(servicoCatalogo.Id, Arg.Any<CancellationToken>()).Returns(servicoCatalogo);

        var resposta = await _servico.AdicionarServicoAsync(ordem.Id, new AdicionarServicoRequest(servicoCatalogo.Id, 2));

        var item = resposta.Servicos.ShouldHaveSingleItem();
        item.PrecoUnitario.ShouldBe(120m);
        item.Subtotal.ShouldBe(240m);
        item.TempoEstimadoEmMinutos.ShouldBe(90);
    }

    [Fact]
    public async Task AdicionarServicoAsync_ComServicoInativo_Rejeita()
    {
        var ordem = OrdemEmDiagnostico();
        var servicoCatalogo = Servico.Cadastrar("Fora de linha", null, CategoriaServico.Outros, 100m, 30);
        servicoCatalogo.Inativar();

        _ordens.ObterCompletaPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);
        _servicos.ObterPorIdAsync(servicoCatalogo.Id, Arg.Any<CancellationToken>()).Returns(servicoCatalogo);

        await Should.ThrowAsync<Domain.Abstractions.DomainException>(() =>
            _servico.AdicionarServicoAsync(ordem.Id, new AdicionarServicoRequest(servicoCatalogo.Id, 1)));
    }

    // -----------------------------------------------------------------
    // Acompanhamento público
    // -----------------------------------------------------------------

    [Fact]
    public async Task AcompanharAsync_ComNumeroEDocumentoCorretos_DevolveOAcompanhamento()
    {
        var (cliente, veiculo) = ClienteComVeiculo();
        var ordem = AbrirOrdem(cliente, veiculo);

        _ordens.ObterPorNumeroAsync("OS-2026-000001", Arg.Any<CancellationToken>()).Returns(ordem);

        var resposta = await _servico.AcompanharAsync("OS-2026-000001", CpfValido);

        resposta.Numero.ShouldBe("OS-2026-000001");
        resposta.Status.ShouldBe(StatusOrdemServico.Recebida);
        resposta.Veiculo.ShouldBe(veiculo.Descricao);
    }

    [Fact]
    public async Task AcompanharAsync_ComDocumentoDeOutroCliente_NaoRevelaAExistenciaDaOrdem()
    {
        var (cliente, veiculo) = ClienteComVeiculo();
        var ordem = AbrirOrdem(cliente, veiculo);

        _ordens.ObterPorNumeroAsync("OS-2026-000001", Arg.Any<CancellationToken>()).Returns(ordem);

        // A resposta é a mesma de "OS inexistente": diferenciá-las permitiria
        // enumerar números de OS válidos.
        await Should.ThrowAsync<RecursoNaoEncontradoException>(() =>
            _servico.AcompanharAsync("OS-2026-000001", "16899535009"));
    }

    [Fact]
    public async Task AcompanharAsync_ComOrdemInexistente_NaoRevelaNada()
    {
        _ordens.ObterPorNumeroAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((OrdemServico?)null);

        await Should.ThrowAsync<RecursoNaoEncontradoException>(() =>
            _servico.AcompanharAsync("OS-2026-999999", CpfValido));
    }

    [Fact]
    public async Task AcompanharAsync_ComOrcamentoEmElaboracao_NaoExpoeOValor()
    {
        var (ordem, _) = OrdemComOrcamentoGerado();

        _ordens.ObterPorNumeroAsync(ordem.Numero.Valor, Arg.Any<CancellationToken>()).Returns(ordem);

        var resposta = await _servico.AcompanharAsync(ordem.Numero.Valor, CpfValido);

        // Rascunho interno da oficina: o cliente só vê o orçamento depois de enviado.
        resposta.ValorOrcamento.ShouldBeNull();
        resposta.SituacaoOrcamento.ShouldBeNull();
    }

    [Fact]
    public async Task AcompanharAsync_ComDocumentoInvalido_LancaValidacao() =>
        await Should.ThrowAsync<ValidacaoException>(() =>
            _servico.AcompanharAsync("OS-2026-000001", "00000000000"));

    // -----------------------------------------------------------------
    // Expiração
    // -----------------------------------------------------------------

    [Fact]
    public async Task ExpirarOrcamentosVencidosAsync_SemCandidatas_NaoAbreTransacao()
    {
        _ordens.ListarComOrcamentoVencidoAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns([]);

        (await _servico.ExpirarOrcamentosVencidosAsync()).ShouldBe(0);

        await _unitOfWork.DidNotReceive().ExecutarEmTransacaoAsync(
            Arg.Any<Func<CancellationToken, Task<int>>>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------

    private (Cliente Cliente, Veiculo Veiculo) ClienteComVeiculo()
    {
        var cliente = Cliente.Cadastrar("Maria Souza", CpfValido, "maria@exemplo.com", "11987654321");
        var veiculo = Veiculo.Cadastrar(cliente.Id, "ABC1D23", "Volkswagen", "Gol", 2020, 2021, "Branco", 50_000);

        _clientes.ObterPorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _clientes.ObterPorDocumentoAsync(cliente.Documento, Arg.Any<CancellationToken>()).Returns(cliente);
        _veiculos.ObterPorIdAsync(veiculo.Id, Arg.Any<CancellationToken>()).Returns(veiculo);
        _veiculos.ObterPorPlacaAsync(veiculo.Placa, Arg.Any<CancellationToken>()).Returns(veiculo);

        return (cliente, veiculo);
    }

    private OrdemServico AbrirOrdem(Cliente cliente, Veiculo veiculo)
    {
        var ordem = OrdemServico.Abrir(
            Domain.OrdensServico.ValueObjects.NumeroOrdemServico.Gerar(2026, 1),
            cliente.Id,
            veiculo.Id,
            "Barulho ao frear",
            50_000);

        _ordens.ObterCompletaPorIdAsync(ordem.Id, Arg.Any<CancellationToken>()).Returns(ordem);

        return ordem;
    }

    private OrdemServico OrdemEmDiagnostico()
    {
        var (cliente, veiculo) = ClienteComVeiculo();
        var ordem = AbrirOrdem(cliente, veiculo);

        ordem.IniciarDiagnostico();

        return ordem;
    }

    private (OrdemServico Ordem, Peca Peca) OrdemComOrcamentoGerado()
    {
        var ordem = OrdemEmDiagnostico();
        var peca = Peca.Cadastrar("OL-5W30", "Óleo 5W30", null, UnidadeMedida.Litro, 48.90m, 10, 0);

        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);
        _pecas.ObterPorIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([peca]);

        peca.Reservar(3, ordem.Id);
        var item = ordem.AdicionarPeca(peca.Id, peca.Codigo, peca.Nome, peca.PrecoUnitario.Valor, 3);
        ordem.ConfirmarReservaDePeca(item.Id);
        ordem.GerarOrcamento();

        return (ordem, peca);
    }

    private (OrdemServico Ordem, Peca Peca) OrdemComOrcamentoEnviado()
    {
        var (ordem, peca) = OrdemComOrcamentoGerado();

        ordem.EnviarOrcamentoParaAprovacao();

        return (ordem, peca);
    }
}
