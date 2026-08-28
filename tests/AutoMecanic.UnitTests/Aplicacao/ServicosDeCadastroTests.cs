using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Clientes;
using AutoMecanic.Application.Clientes.Dtos;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Estoque;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Application.Indicadores;
using AutoMecanic.Application.Veiculos;
using AutoMecanic.Application.Veiculos.Dtos;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.ValueObjects;
using AutoMecanic.Domain.Veiculos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AutoMecanic.UnitTests.Aplicacao;

public sealed class ServicoDeClientesTests
{
    private const string CpfValido = "52998224725";

    private readonly IRepositorioDeClientes _repositorio = Substitute.For<IRepositorioDeClientes>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeClientes _servico;

    public ServicoDeClientesTests() =>
        _servico = new ServicoDeClientes(_repositorio, _unitOfWork, NullLogger<ServicoDeClientes>.Instance);

    [Fact]
    public async Task CadastrarAsync_ComDadosValidos_PersisteECommita()
    {
        _repositorio.ExisteComDocumentoAsync(Arg.Any<Documento>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var resposta = await _servico.CadastrarAsync(
            new CriarClienteRequest("Maria Souza", CpfValido, "maria@exemplo.com", "11987654321"));

        resposta.DocumentoFormatado.ShouldBe("529.982.247-25");
        resposta.Ativo.ShouldBeTrue();

        await _repositorio.Received(1).AdicionarAsync(Arg.Any<Cliente>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CadastrarAsync_ComDocumentoJaCadastrado_LancaConflito()
    {
        _repositorio.ExisteComDocumentoAsync(Arg.Any<Documento>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Unicidade é regra de conjunto: o agregado sozinho não consegue verificá-la.
        var excecao = await Should.ThrowAsync<ConflitoException>(() =>
            _servico.CadastrarAsync(new CriarClienteRequest("Maria", CpfValido, "m@e.com", "11987654321")));

        excecao.Codigo.ShouldBe("DOCUMENTO_DUPLICADO");
        await _repositorio.DidNotReceive().AdicionarAsync(Arg.Any<Cliente>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ObterPorIdAsync_ComClienteInexistente_LancaNaoEncontrado()
    {
        _repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        await Should.ThrowAsync<RecursoNaoEncontradoException>(() => _servico.ObterPorIdAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task ObterPorDocumentoAsync_ComDocumentoInvalido_LancaValidacao() =>
        await Should.ThrowAsync<ValidacaoException>(() => _servico.ObterPorDocumentoAsync("00000000000"));

    [Fact]
    public async Task ObterPorDocumentoAsync_ComClienteExistente_Projeta()
    {
        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");

        _repositorio.ObterPorDocumentoAsync(Arg.Any<Documento>(), Arg.Any<CancellationToken>()).Returns(cliente);

        (await _servico.ObterPorDocumentoAsync("529.982.247-25")).Id.ShouldBe(cliente.Id);
    }

    [Fact]
    public async Task InativarAsync_MarcaComoInativoESalva()
    {
        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");
        _repositorio.ObterPorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        await _servico.InativarAsync(cliente.Id, "Encerrou relacionamento");

        cliente.Ativo.ShouldBeFalse();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarAsync_ProjetaAPaginaParaResumo()
    {
        var paginacao = new ParametrosDePaginacao { Pagina = 1, TamanhoPagina = 10 };
        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");

        _repositorio.ListarAsync(null, null, paginacao, Arg.Any<CancellationToken>())
            .Returns(ResultadoPaginado<Cliente>.Criar([cliente], 1, paginacao));

        var pagina = await _servico.ListarAsync(null, null, paginacao);

        pagina.TotalDeItens.ShouldBe(1);
        pagina.Itens.ShouldHaveSingleItem().Nome.ShouldBe("Maria");
    }
}

public sealed class ServicoDeVeiculosTests
{
    private const string CpfValido = "52998224725";

    private readonly IRepositorioDeVeiculos _veiculos = Substitute.For<IRepositorioDeVeiculos>();
    private readonly IRepositorioDeClientes _clientes = Substitute.For<IRepositorioDeClientes>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeVeiculos _servico;

    public ServicoDeVeiculosTests() =>
        _servico = new ServicoDeVeiculos(_veiculos, _clientes, _unitOfWork, NullLogger<ServicoDeVeiculos>.Instance);

    [Fact]
    public async Task CadastrarAsync_ComClienteAtivo_Persiste()
    {
        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");
        _clientes.ObterPorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _veiculos.ExisteComPlacaAsync(Arg.Any<Domain.Veiculos.ValueObjects.Placa>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var resposta = await _servico.CadastrarAsync(
            new CriarVeiculoRequest(cliente.Id, "ABC1D23", "Volkswagen", "Gol", 2020, 2021, "Branco", 50_000));

        resposta.PlacaFormatada.ShouldBe("ABC1D23");
        resposta.NomeCliente.ShouldBe("Maria");

        await _veiculos.Received(1).AdicionarAsync(Arg.Any<Veiculo>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CadastrarAsync_ComPlacaDuplicada_LancaConflito()
    {
        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");
        _clientes.ObterPorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);
        _veiculos.ExisteComPlacaAsync(Arg.Any<Domain.Veiculos.ValueObjects.Placa>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        (await Should.ThrowAsync<ConflitoException>(() => _servico.CadastrarAsync(
            new CriarVeiculoRequest(cliente.Id, "ABC1D23", "VW", "Gol", 2020))))
            .Codigo.ShouldBe("PLACA_DUPLICADA");
    }

    [Fact]
    public async Task CadastrarAsync_ComClienteInativo_Rejeita()
    {
        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");
        cliente.Inativar();
        _clientes.ObterPorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        await Should.ThrowAsync<Domain.Abstractions.DomainException>(() => _servico.CadastrarAsync(
            new CriarVeiculoRequest(cliente.Id, "ABC1D23", "VW", "Gol", 2020)));
    }

    [Fact]
    public async Task ObterPorPlacaAsync_ComPlacaInvalida_LancaValidacao() =>
        await Should.ThrowAsync<ValidacaoException>(() => _servico.ObterPorPlacaAsync("PLACA-RUIM"));

    [Fact]
    public async Task TransferirAsync_TrocaOProprietario()
    {
        var antigo = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");
        var novo = Cliente.Cadastrar("João", "16899535009", "j@e.com", "21976543210");
        var veiculo = Veiculo.Cadastrar(antigo.Id, "ABC1D23", "VW", "Gol", 2020);

        _veiculos.ObterPorIdAsync(veiculo.Id, Arg.Any<CancellationToken>()).Returns(veiculo);
        _clientes.ObterPorIdAsync(novo.Id, Arg.Any<CancellationToken>()).Returns(novo);

        var resposta = await _servico.TransferirAsync(veiculo.Id, new TransferirVeiculoRequest(novo.Id));

        resposta.ClienteId.ShouldBe(novo.Id);
    }
}

public sealed class ServicoDeEstoqueTests
{
    private readonly IRepositorioDePecas _pecas = Substitute.For<IRepositorioDePecas>();
    private readonly IRepositorioDeMovimentosDeEstoque _movimentos = Substitute.For<IRepositorioDeMovimentosDeEstoque>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeEstoque _servico;

    public ServicoDeEstoqueTests() =>
        _servico = new ServicoDeEstoque(_pecas, _movimentos, _unitOfWork, NullLogger<ServicoDeEstoque>.Instance);

    [Fact]
    public async Task CadastrarAsync_ComCodigoDuplicado_LancaConflito()
    {
        _pecas.ExisteComCodigoAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        (await Should.ThrowAsync<ConflitoException>(() => _servico.CadastrarAsync(
            new CriarPecaRequest("OL-5W30", "Óleo", null, UnidadeMedida.Litro, 48.90m, 10, 2))))
            .Codigo.ShouldBe("CODIGO_DUPLICADO");
    }

    [Fact]
    public async Task CadastrarAsync_ComDadosValidos_ProjetaOsTresSaldos()
    {
        _pecas.ExisteComCodigoAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

        var resposta = await _servico.CadastrarAsync(
            new CriarPecaRequest("ol-5w30", "Óleo", null, UnidadeMedida.Litro, 48.90m, 10, 2));

        resposta.Codigo.ShouldBe("OL-5W30");
        resposta.QuantidadeEmEstoque.ShouldBe(10);
        resposta.QuantidadeReservada.ShouldBe(0);
        resposta.QuantidadeDisponivel.ShouldBe(10);
        resposta.AbaixoDoEstoqueMinimo.ShouldBeFalse();
    }

    [Fact]
    public async Task RegistrarEntradaAsync_AumentaOSaldo()
    {
        var peca = CriarPeca();
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        var resposta = await _servico.RegistrarEntradaAsync(peca.Id, new RegistrarEntradaRequest(50, "NF 123"));

        resposta.QuantidadeEmEstoque.ShouldBe(60);
    }

    [Fact]
    public async Task AjustarSaldoAsync_AplicaAContagemFisica()
    {
        var peca = CriarPeca();
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        var resposta = await _servico.AjustarSaldoAsync(peca.Id, new AjustarEstoqueRequest(8, "Inventário"));

        resposta.QuantidadeEmEstoque.ShouldBe(8);
    }

    [Fact]
    public async Task ListarAlertasDeEstoqueAsync_SugereReporAteODobroDoMinimo()
    {
        var peca = Peca.Cadastrar("BAT-60AH", "Bateria", null, UnidadeMedida.Unidade, 549m, 2, 5);

        _pecas.ListarAbaixoDoEstoqueMinimoAsync(Arg.Any<CancellationToken>()).Returns([peca]);

        var alerta = (await _servico.ListarAlertasDeEstoqueAsync()).ShouldHaveSingleItem();

        alerta.QuantidadeDisponivel.ShouldBe(2);
        alerta.EstoqueMinimo.ShouldBe(5);
        alerta.QuantidadeSugeridaDeCompra.ShouldBe(8); // (5 * 2) - 2
    }

    [Fact]
    public async Task ObterPorCodigoAsync_SemCodigo_LancaValidacao() =>
        await Should.ThrowAsync<ValidacaoException>(() => _servico.ObterPorCodigoAsync("  "));

    private static Peca CriarPeca() =>
        Peca.Cadastrar("OL-5W30", "Óleo 5W30", null, UnidadeMedida.Litro, 48.90m, 10, 2);
}

public sealed class ServicoDeAutenticacaoTests
{
    private const string SenhaValida = "Senha@Forte1";

    private readonly IRepositorioDeUsuarios _usuarios = Substitute.For<IRepositorioDeUsuarios>();
    private readonly IServicoDeHashDeSenha _hasher = Substitute.For<IServicoDeHashDeSenha>();
    private readonly IGeradorDeToken _gerador = Substitute.For<IGeradorDeToken>();
    private readonly IProvedorDeDataHora _relogio = Substitute.For<IProvedorDeDataHora>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeAutenticacao _servico;

    public ServicoDeAutenticacaoTests()
    {
        _relogio.Agora.Returns(DateTimeOffset.UtcNow);
        _hasher.GerarHash(Arg.Any<string>()).Returns(chamada => $"hash::{chamada.Arg<string>()}");
        _hasher.Verificar(Arg.Any<string>(), Arg.Any<string>())
            .Returns(chamada => chamada.ArgAt<string>(1) == $"hash::{chamada.ArgAt<string>(0)}");

        _gerador.Gerar(Arg.Any<Usuario>())
            .Returns(new TokenDeAcesso("token-jwt", DateTimeOffset.UtcNow.AddHours(1)));

        _servico = new ServicoDeAutenticacao(
            _usuarios, _hasher, _gerador, _relogio, _unitOfWork,
            NullLogger<ServicoDeAutenticacao>.Instance);
    }

    [Fact]
    public async Task AutenticarAsync_ComCredenciaisValidas_EmiteToken()
    {
        var usuario = CriarUsuario();
        _usuarios.ObterPorEmailAsync("fulano@automecanic.com.br", Arg.Any<CancellationToken>()).Returns(usuario);

        var resposta = await _servico.AutenticarAsync(new LoginRequest("Fulano@AutoMecanic.com.br", SenhaValida));

        resposta.Token.ShouldBe("token-jwt");
        resposta.TipoToken.ShouldBe("Bearer");
        resposta.Usuario.Email.ShouldBe("fulano@automecanic.com.br");
    }

    [Fact]
    public async Task AutenticarAsync_ComEmailInexistente_RecusaSemRevelarACausa()
    {
        _usuarios.ObterPorEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Usuario?)null);

        var excecao = await Should.ThrowAsync<NaoAutorizadoException>(() =>
            _servico.AutenticarAsync(new LoginRequest("nao@existe.com", SenhaValida)));

        // A mensagem é genérica: dizer "e-mail não cadastrado" entregaria ao atacante
        // quais contas existem.
        excecao.Message.ShouldBe("Credenciais inválidas.");
    }

    [Fact]
    public async Task AutenticarAsync_ComSenhaIncorreta_RecusaComAMesmaMensagem()
    {
        var usuario = CriarUsuario();
        _usuarios.ObterPorEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(usuario);

        var excecao = await Should.ThrowAsync<NaoAutorizadoException>(() =>
            _servico.AutenticarAsync(new LoginRequest("fulano@automecanic.com.br", "Errada@123")));

        excecao.Message.ShouldBe("Credenciais inválidas.");
        usuario.TentativasFalhas.ShouldBe(1);
    }

    [Fact]
    public async Task AutenticarAsync_ComFalha_PersisteOContadorDeTentativas()
    {
        var usuario = CriarUsuario();
        _usuarios.ObterPorEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(usuario);

        await Should.ThrowAsync<NaoAutorizadoException>(() =>
            _servico.AutenticarAsync(new LoginRequest("fulano@automecanic.com.br", "Errada@123")));

        // Sem persistir, o contador reiniciaria a cada requisição e o bloqueio
        // por força bruta nunca aconteceria.
        await _unitOfWork.Received().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutenticarAsync_ComUsuarioInativo_Recusa()
    {
        var usuario = CriarUsuario();
        usuario.Inativar();
        _usuarios.ObterPorEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(usuario);

        await Should.ThrowAsync<NaoAutorizadoException>(() =>
            _servico.AutenticarAsync(new LoginRequest("fulano@automecanic.com.br", SenhaValida)));
    }

    private Usuario CriarUsuario() =>
        Usuario.Criar("Fulano de Tal", "fulano@automecanic.com.br", SenhaValida, PerfilUsuario.Atendente, _hasher.GerarHash);
}

public sealed class ServicoDeIndicadoresTests
{
    private readonly IRepositorioDeOrdensServico _ordens = Substitute.For<IRepositorioDeOrdensServico>();
    private readonly IRepositorioDePecas _pecas = Substitute.For<IRepositorioDePecas>();
    private readonly IProvedorDeDataHora _relogio = Substitute.For<IProvedorDeDataHora>();
    private readonly ServicoDeIndicadores _servico;

    public ServicoDeIndicadoresTests()
    {
        _relogio.Agora.Returns(new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));
        _servico = new ServicoDeIndicadores(_ordens, _pecas, _relogio);
    }

    [Fact]
    public async Task ObterTempoMedioDeExecucaoAsync_SemOrdensFinalizadas_DevolveZeros()
    {
        _ordens.ListarFinalizadasNoPeriodoAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var indicador = await _servico.ObterTempoMedioDeExecucaoAsync(null, null);

        indicador.OrdensFinalizadas.ShouldBe(0);
        indicador.TempoMedioDeExecucaoEmMinutos.ShouldBe(0);
    }

    [Fact]
    public async Task ObterTempoMedioDeExecucaoAsync_ComOrdens_CalculaMediaEMediana()
    {
        // Durações de 30, 60 e 300 minutos: média 130, mediana 60. A diferença entre
        // as duas é justamente o que revela a OS excepcionalmente longa.
        var ordens = new[]
        {
            OrdemFinalizadaCom(TimeSpan.FromMinutes(30)),
            OrdemFinalizadaCom(TimeSpan.FromMinutes(60)),
            OrdemFinalizadaCom(TimeSpan.FromMinutes(300))
        };

        _ordens.ListarFinalizadasNoPeriodoAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ordens);

        var indicador = await _servico.ObterTempoMedioDeExecucaoAsync(null, null);

        indicador.OrdensFinalizadas.ShouldBe(3);
        indicador.TempoMedioDeExecucaoEmMinutos.ShouldBe(130);
        indicador.TempoMedianoDeExecucaoEmMinutos.ShouldBe(60);
        indicador.MenorTempoEmMinutos.ShouldBe(30);
        indicador.MaiorTempoEmMinutos.ShouldBe(300);
    }

    [Fact]
    public async Task ObterTempoMedioDeExecucaoAsync_ComPeriodoInvertido_NormalizaAOrdem()
    {
        _ordens.ListarFinalizadasNoPeriodoAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var inicio = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var fim = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var indicador = await _servico.ObterTempoMedioDeExecucaoAsync(inicio, fim);

        indicador.PeriodoDe.ShouldBeLessThan(indicador.PeriodoAte);
    }

    [Fact]
    public async Task ObterPainelOperacionalAsync_AgrupaPorSituacaoEContaPecasCriticas()
    {
        _ordens.ContarPorStatusAsync(Arg.Any<CancellationToken>()).Returns(
            new Dictionary<StatusOrdemServico, int>
            {
                [StatusOrdemServico.Recebida] = 3,
                [StatusOrdemServico.AguardandoAprovacao] = 2,
                [StatusOrdemServico.Entregue] = 10
            });

        _pecas.ListarAbaixoDoEstoqueMinimoAsync(Arg.Any<CancellationToken>()).Returns(
            [Peca.Cadastrar("BAT", "Bateria", null, UnidadeMedida.Unidade, 549m, 1, 5)]);

        var painel = await _servico.ObterPainelOperacionalAsync();

        // "Entregue" é terminal e não conta como em aberto.
        painel.OrdensEmAberto.ShouldBe(5);
        painel.OrdensAguardandoAprovacao.ShouldBe(2);
        painel.PecasAbaixoDoEstoqueMinimo.ShouldBe(1);
        painel.OrdensPorStatus["Recebida"].ShouldBe(3);
    }

    /// <summary>
    /// Constrói uma OS finalizada com duração controlada. Como os marcos temporais são
    /// definidos pelo relógio interno do agregado, a duração é obtida por reflexão sobre
    /// os campos de apoio — o alternativo seria injetar um relógio em todo o domínio.
    /// </summary>
    private static OrdemServico OrdemFinalizadaCom(TimeSpan duracao)
    {
        var ordem = OrdemServico.Abrir(
            NumeroOrdemServico.Gerar(2026, Random.Shared.Next(1, 999_999)),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Problema relatado");

        ordem.IniciarDiagnostico();
        ordem.AdicionarServico(Guid.CreateVersion7(), "Serviço", 100m, 1, 60);
        ordem.GerarOrcamento();
        ordem.EnviarOrcamentoParaAprovacao();
        ordem.AprovarOrcamento();
        ordem.FinalizarServico();

        DefinirPropriedade(ordem, nameof(OrdemServico.ExecucaoIniciadaEm), ordem.FinalizadaEm!.Value - duracao);

        return ordem;
    }

    private static void DefinirPropriedade(OrdemServico ordem, string propriedade, DateTimeOffset valor) =>
        typeof(OrdemServico)
            .GetProperty(propriedade)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(ordem, [valor]);
}
