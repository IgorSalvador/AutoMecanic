using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Estoque;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Application.Servicos;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Application.Veiculos;
using AutoMecanic.Application.Veiculos.Dtos;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.Servicos;
using AutoMecanic.Domain.Veiculos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AutoMecanic.UnitTests.Aplicacao;

public sealed class ServicoDeCatalogoTests
{
    private readonly IRepositorioDeServicos _repositorio = Substitute.For<IRepositorioDeServicos>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeCatalogo _servico;

    public ServicoDeCatalogoTests() =>
        _servico = new ServicoDeCatalogo(_repositorio, _unitOfWork, NullLogger<ServicoDeCatalogo>.Instance);

    [Fact]
    public async Task CadastrarAsync_ComDadosValidos_Persiste()
    {
        _repositorio.ExisteComNomeAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

        var resposta = await _servico.CadastrarAsync(
            new CriarServicoRequest("Troca de óleo", "Óleo e filtro", CategoriaServico.ManutencaoPreventiva, 120m, 45));

        resposta.Nome.ShouldBe("Troca de óleo");
        resposta.Preco.ShouldBe(120m);
        resposta.Ativo.ShouldBeTrue();

        await _repositorio.Received(1).AdicionarAsync(Arg.Any<Servico>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CadastrarAsync_ComNomeJaExistente_LancaConflito()
    {
        _repositorio.ExisteComNomeAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        (await Should.ThrowAsync<ConflitoException>(() => _servico.CadastrarAsync(
            new CriarServicoRequest("Troca de óleo", null, CategoriaServico.Outros, 100m, 30))))
            .Codigo.ShouldBe("SERVICO_DUPLICADO");
    }

    [Fact]
    public async Task AtualizarAsync_ComNomeDeOutroServico_LancaConflito()
    {
        var servico = CriarServico();
        _repositorio.ObterPorIdAsync(servico.Id, Arg.Any<CancellationToken>()).Returns(servico);
        _repositorio.ExisteComNomeAsync(Arg.Any<string>(), servico.Id, Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<ConflitoException>(() => _servico.AtualizarAsync(servico.Id,
            new AtualizarServicoRequest("Outro nome", null, CategoriaServico.Outros, 60)));
    }

    [Fact]
    public async Task AtualizarAsync_ComDadosValidos_AlteraOServico()
    {
        var servico = CriarServico();
        _repositorio.ObterPorIdAsync(servico.Id, Arg.Any<CancellationToken>()).Returns(servico);
        _repositorio.ExisteComNomeAsync(Arg.Any<string>(), servico.Id, Arg.Any<CancellationToken>()).Returns(false);

        var resposta = await _servico.AtualizarAsync(servico.Id,
            new AtualizarServicoRequest("Troca de óleo premium", "Sintético", CategoriaServico.ManutencaoPreventiva, 60));

        resposta.Nome.ShouldBe("Troca de óleo premium");
        resposta.TempoEstimadoEmMinutos.ShouldBe(60);
    }

    [Fact]
    public async Task ReajustarPrecoAsync_AtualizaOPrecoDeTabela()
    {
        var servico = CriarServico();
        _repositorio.ObterPorIdAsync(servico.Id, Arg.Any<CancellationToken>()).Returns(servico);

        (await _servico.ReajustarPrecoAsync(servico.Id, new ReajustarPrecoRequest(150m)))
            .Preco.ShouldBe(150m);
    }

    [Fact]
    public async Task ObterPorIdAsync_ComServicoInexistente_LancaNaoEncontrado()
    {
        _repositorio.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Servico?)null);

        await Should.ThrowAsync<RecursoNaoEncontradoException>(() => _servico.ObterPorIdAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task ListarAsync_ProjetaAPagina()
    {
        var paginacao = new ParametrosDePaginacao();
        _repositorio.ListarAsync(null, null, null, paginacao, Arg.Any<CancellationToken>())
            .Returns(ResultadoPaginado<Servico>.Criar([CriarServico()], 1, paginacao));

        (await _servico.ListarAsync(null, null, null, paginacao)).Itens.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InativarEReativar_AlternamASituacao()
    {
        var servico = CriarServico();
        _repositorio.ObterPorIdAsync(servico.Id, Arg.Any<CancellationToken>()).Returns(servico);

        await _servico.InativarAsync(servico.Id);
        servico.Ativo.ShouldBeFalse();

        await _servico.ReativarAsync(servico.Id);
        servico.Ativo.ShouldBeTrue();
    }

    private static Servico CriarServico() =>
        Servico.Cadastrar("Troca de óleo", "Óleo e filtro", CategoriaServico.ManutencaoPreventiva, 120m, 45);
}

public sealed class ServicoDeUsuariosTests
{
    private const string SenhaValida = "Senha@Forte1";

    private readonly IRepositorioDeUsuarios _repositorio = Substitute.For<IRepositorioDeUsuarios>();
    private readonly IServicoDeHashDeSenha _hasher = Substitute.For<IServicoDeHashDeSenha>();
    private readonly IUsuarioAtual _usuarioAtual = Substitute.For<IUsuarioAtual>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeUsuarios _servico;

    public ServicoDeUsuariosTests()
    {
        _hasher.GerarHash(Arg.Any<string>()).Returns(c => $"hash::{c.Arg<string>()}");
        _hasher.Verificar(Arg.Any<string>(), Arg.Any<string>())
            .Returns(c => c.ArgAt<string>(1) == $"hash::{c.ArgAt<string>(0)}");

        _servico = new ServicoDeUsuarios(
            _repositorio, _hasher, _usuarioAtual, _unitOfWork, NullLogger<ServicoDeUsuarios>.Instance);
    }

    [Fact]
    public async Task CriarAsync_ComEmailDisponivel_Persiste()
    {
        _repositorio.ExisteComEmailAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);

        var resposta = await _servico.CriarAsync(
            new CriarUsuarioRequest("Fulano de Tal", "Fulano@AutoMecanic.com.BR", SenhaValida, PerfilUsuario.Atendente));

        resposta.Email.ShouldBe("fulano@automecanic.com.br");
        resposta.Perfil.ShouldBe(PerfilUsuario.Atendente);

        await _repositorio.Received(1).AdicionarAsync(Arg.Any<Usuario>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CriarAsync_ComEmailJaCadastrado_LancaConflito()
    {
        _repositorio.ExisteComEmailAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);

        (await Should.ThrowAsync<ConflitoException>(() => _servico.CriarAsync(
            new CriarUsuarioRequest("Fulano", "f@e.com", SenhaValida, PerfilUsuario.Atendente))))
            .Codigo.ShouldBe("EMAIL_DUPLICADO");
    }

    [Fact]
    public async Task AtualizarAsync_AdministradorRebaixandoASiProprio_LancaConflito()
    {
        var administrador = CriarUsuario(PerfilUsuario.Administrador);
        _repositorio.ObterPorIdAsync(administrador.Id, Arg.Any<CancellationToken>()).Returns(administrador);
        _usuarioAtual.Id.Returns(administrador.Id);

        // Sem essa guarda, o último administrador poderia se rebaixar e deixar o
        // sistema sem ninguém capaz de gerenciar usuários.
        (await Should.ThrowAsync<ConflitoException>(() => _servico.AtualizarAsync(administrador.Id,
            new AtualizarUsuarioRequest("Fulano", PerfilUsuario.Atendente))))
            .Codigo.ShouldBe("AUTO_REBAIXAMENTO");
    }

    [Fact]
    public async Task AtualizarAsync_AlterandoOutroUsuario_EhPermitido()
    {
        var usuario = CriarUsuario(PerfilUsuario.Atendente);
        _repositorio.ObterPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);
        _usuarioAtual.Id.Returns(Guid.CreateVersion7());

        (await _servico.AtualizarAsync(usuario.Id, new AtualizarUsuarioRequest("Novo Nome", PerfilUsuario.Mecanico)))
            .Perfil.ShouldBe(PerfilUsuario.Mecanico);
    }

    [Fact]
    public async Task InativarAsync_APropriaConta_LancaConflito()
    {
        var usuario = CriarUsuario(PerfilUsuario.Administrador);
        _usuarioAtual.Id.Returns(usuario.Id);

        (await Should.ThrowAsync<ConflitoException>(() => _servico.InativarAsync(usuario.Id)))
            .Codigo.ShouldBe("AUTO_INATIVACAO");
    }

    [Fact]
    public async Task InativarEReativar_OutroUsuario_AlternamASituacao()
    {
        var usuario = CriarUsuario(PerfilUsuario.Atendente);
        _repositorio.ObterPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);
        _usuarioAtual.Id.Returns(Guid.CreateVersion7());

        await _servico.InativarAsync(usuario.Id);
        usuario.Ativo.ShouldBeFalse();

        await _servico.ReativarAsync(usuario.Id);
        usuario.Ativo.ShouldBeTrue();
    }

    [Fact]
    public async Task AlterarSenhaAsync_ComSenhaAtualCorreta_TrocaOHash()
    {
        var usuario = CriarUsuario(PerfilUsuario.Atendente);
        _repositorio.ObterPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);

        await _servico.AlterarSenhaAsync(usuario.Id, new AlterarSenhaRequest(SenhaValida, "Nova@Senha9"));

        usuario.SenhaHash.ShouldBe("hash::Nova@Senha9");
    }

    [Fact]
    public async Task RedefinirSenhaAsync_NaoExigeASenhaAnterior()
    {
        var usuario = CriarUsuario(PerfilUsuario.Atendente);
        _repositorio.ObterPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);

        await _servico.RedefinirSenhaAsync(usuario.Id, new RedefinirSenhaRequest("Redefinida@9"));

        usuario.SenhaHash.ShouldBe("hash::Redefinida@9");
    }

    [Fact]
    public async Task DesbloquearAsync_LimpaOBloqueio()
    {
        var usuario = CriarUsuario(PerfilUsuario.Atendente);
        var agora = DateTimeOffset.UtcNow;

        for (var i = 0; i < Usuario.MaximoTentativasFalhas; i++)
        {
            usuario.TentarAutenticar("Errada@1", _hasher.Verificar, agora);
        }

        _repositorio.ObterPorIdAsync(usuario.Id, Arg.Any<CancellationToken>()).Returns(usuario);

        await _servico.DesbloquearAsync(usuario.Id);

        usuario.EstaBloqueado(agora).ShouldBeFalse();
    }

    [Fact]
    public async Task ListarAsync_ProjetaSemExporOHashDaSenha()
    {
        var paginacao = new ParametrosDePaginacao();

        // O usuário é construído ANTES do Returns: criá-lo dentro da expressão faria o
        // NSubstitute interpretar a chamada a _hasher.GerarHash como parte da configuração.
        var pagina = ResultadoPaginado<Usuario>.Criar([CriarUsuario(PerfilUsuario.Atendente)], 1, paginacao);

        _repositorio.ListarAsync(null, null, null, paginacao, Arg.Any<CancellationToken>()).Returns(pagina);

        var resultado = await _servico.ListarAsync(null, null, null, paginacao);

        // O DTO existe justamente para tornar impossível vazar o hash por acidente.
        resultado.Itens.ShouldHaveSingleItem().GetType()
            .GetProperty("SenhaHash").ShouldBeNull();
    }

    private Usuario CriarUsuario(PerfilUsuario perfil) =>
        Usuario.Criar("Fulano de Tal", "fulano@automecanic.com.br", SenhaValida, perfil, _hasher.GerarHash);
}

public sealed class ServicoDeEstoqueComplementarTests
{
    private readonly IRepositorioDePecas _pecas = Substitute.For<IRepositorioDePecas>();
    private readonly IRepositorioDeMovimentosDeEstoque _movimentos = Substitute.For<IRepositorioDeMovimentosDeEstoque>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeEstoque _servico;

    public ServicoDeEstoqueComplementarTests() =>
        _servico = new ServicoDeEstoque(_pecas, _movimentos, _unitOfWork, NullLogger<ServicoDeEstoque>.Instance);

    [Fact]
    public async Task AtualizarAsync_AlteraNomeEPontoDeRessuprimento()
    {
        var peca = CriarPeca();
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        var resposta = await _servico.AtualizarAsync(peca.Id,
            new AtualizarPecaRequest("Óleo sintético premium", "5W30", UnidadeMedida.Litro, 30));

        resposta.Nome.ShouldBe("Óleo sintético premium");
        resposta.EstoqueMinimo.ShouldBe(30);
    }

    [Fact]
    public async Task ReajustarPrecoAsync_AtualizaOPrecoUnitario()
    {
        var peca = CriarPeca();
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        (await _servico.ReajustarPrecoAsync(peca.Id, 59.90m)).PrecoUnitario.ShouldBe(59.90m);
    }

    [Fact]
    public async Task ObterPorCodigoAsync_ComPecaExistente_NormalizaOCodigo()
    {
        var peca = CriarPeca();
        _pecas.ObterPorCodigoAsync("OL-5W30", Arg.Any<CancellationToken>()).Returns(peca);

        (await _servico.ObterPorCodigoAsync(" ol-5w30 ")).Codigo.ShouldBe("OL-5W30");
    }

    [Fact]
    public async Task ObterPorCodigoAsync_ComPecaInexistente_LancaNaoEncontrado()
    {
        _pecas.ObterPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Peca?)null);

        await Should.ThrowAsync<RecursoNaoEncontradoException>(() => _servico.ObterPorCodigoAsync("INEXISTENTE"));
    }

    [Fact]
    public async Task RegistrarPerdaAsync_ReduzOSaldo()
    {
        var peca = CriarPeca();
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        (await _servico.RegistrarPerdaAsync(peca.Id, new RegistrarPerdaRequest(3, "Avaria")))
            .QuantidadeEmEstoque.ShouldBe(7);
    }

    [Fact]
    public async Task ListarMovimentosAsync_ProjetaOExtrato()
    {
        var paginacao = new ParametrosDePaginacao();
        var movimento = MovimentoEstoque.Registrar(
            Guid.CreateVersion7(), TipoMovimentoEstoque.Entrada, 10, 0, 10, "NF 123", null, DateTimeOffset.UtcNow);

        _movimentos.ListarAsync(null, null, null, null, null, paginacao, Arg.Any<CancellationToken>())
            .Returns(ResultadoPaginado<MovimentoEstoque>.Criar([movimento], 1, paginacao));

        var pagina = await _servico.ListarMovimentosAsync(null, null, null, null, null, paginacao);

        pagina.Itens.ShouldHaveSingleItem().Motivo.ShouldBe("NF 123");
    }

    [Fact]
    public async Task ListarAsync_ProjetaAPagina()
    {
        var paginacao = new ParametrosDePaginacao();
        _pecas.ListarAsync(null, null, null, paginacao, Arg.Any<CancellationToken>())
            .Returns(ResultadoPaginado<Peca>.Criar([CriarPeca()], 1, paginacao));

        (await _servico.ListarAsync(null, null, null, paginacao)).Itens.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InativarEReativar_AlternamASituacao()
    {
        var peca = CriarPeca();
        _pecas.ObterPorIdAsync(peca.Id, Arg.Any<CancellationToken>()).Returns(peca);

        await _servico.InativarAsync(peca.Id);
        peca.Ativo.ShouldBeFalse();

        await _servico.ReativarAsync(peca.Id);
        peca.Ativo.ShouldBeTrue();
    }

    private static Peca CriarPeca() =>
        Peca.Cadastrar("OL-5W30", "Óleo 5W30", null, UnidadeMedida.Litro, 48.90m, 10, 2);
}

public sealed class ServicoDeVeiculosComplementarTests
{
    private const string CpfValido = "52998224725";

    private readonly IRepositorioDeVeiculos _veiculos = Substitute.For<IRepositorioDeVeiculos>();
    private readonly IRepositorioDeClientes _clientes = Substitute.For<IRepositorioDeClientes>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ServicoDeVeiculos _servico;

    public ServicoDeVeiculosComplementarTests() =>
        _servico = new ServicoDeVeiculos(_veiculos, _clientes, _unitOfWork, NullLogger<ServicoDeVeiculos>.Instance);

    [Fact]
    public async Task ObterPorIdAsync_ResolveONomeDoProprietario()
    {
        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321");
        var veiculo = Veiculo.Cadastrar(cliente.Id, "ABC1D23", "VW", "Gol", 2020);

        _veiculos.ObterPorIdAsync(veiculo.Id, Arg.Any<CancellationToken>()).Returns(veiculo);
        _clientes.ObterPorIdAsync(cliente.Id, Arg.Any<CancellationToken>()).Returns(cliente);

        (await _servico.ObterPorIdAsync(veiculo.Id)).NomeCliente.ShouldBe("Maria");
    }

    [Fact]
    public async Task ObterPorIdAsync_ComVeiculoInexistente_LancaNaoEncontrado()
    {
        _veiculos.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Veiculo?)null);

        await Should.ThrowAsync<RecursoNaoEncontradoException>(() => _servico.ObterPorIdAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task ObterPorPlacaAsync_ComVeiculoInexistente_LancaNaoEncontrado()
    {
        _veiculos.ObterPorPlacaAsync(Arg.Any<Domain.Veiculos.ValueObjects.Placa>(), Arg.Any<CancellationToken>())
            .Returns((Veiculo?)null);

        await Should.ThrowAsync<RecursoNaoEncontradoException>(() => _servico.ObterPorPlacaAsync("ABC1D23"));
    }

    [Fact]
    public async Task AtualizarAsync_AlteraOsDadosDescritivos()
    {
        var veiculo = Veiculo.Cadastrar(Guid.CreateVersion7(), "ABC1D23", "VW", "Gol", 2020);
        _veiculos.ObterPorIdAsync(veiculo.Id, Arg.Any<CancellationToken>()).Returns(veiculo);

        var resposta = await _servico.AtualizarAsync(veiculo.Id,
            new AtualizarVeiculoRequest("Volkswagen", "Gol 1.0", 2020, 2021, "Preto"));

        resposta.Marca.ShouldBe("Volkswagen");
        resposta.Cor.ShouldBe("Preto");
    }

    [Fact]
    public async Task RegistrarQuilometragemAsync_AtualizaOOdometro()
    {
        var veiculo = Veiculo.Cadastrar(Guid.CreateVersion7(), "ABC1D23", "VW", "Gol", 2020, null, null, 10_000);
        _veiculos.ObterPorIdAsync(veiculo.Id, Arg.Any<CancellationToken>()).Returns(veiculo);

        (await _servico.RegistrarQuilometragemAsync(veiculo.Id, new RegistrarQuilometragemRequest(15_000)))
            .Quilometragem.ShouldBe(15_000);
    }

    [Fact]
    public async Task ListarPorClienteAsync_ProjetaOsResumos()
    {
        var clienteId = Guid.CreateVersion7();
        _veiculos.ListarPorClienteAsync(clienteId, Arg.Any<CancellationToken>())
            .Returns([Veiculo.Cadastrar(clienteId, "ABC1D23", "VW", "Gol", 2020)]);

        (await _servico.ListarPorClienteAsync(clienteId)).ShouldHaveSingleItem()
            .PlacaFormatada.ShouldBe("ABC1D23");
    }

    [Fact]
    public async Task ListarAsync_ProjetaAPagina()
    {
        var paginacao = new ParametrosDePaginacao();
        var veiculo = Veiculo.Cadastrar(Guid.CreateVersion7(), "ABC1D23", "VW", "Gol", 2020);

        _veiculos.ListarAsync(null, null, null, paginacao, Arg.Any<CancellationToken>())
            .Returns(ResultadoPaginado<Veiculo>.Criar([veiculo], 1, paginacao));

        (await _servico.ListarAsync(null, null, null, paginacao)).Itens.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InativarEReativar_AlternamASituacao()
    {
        var veiculo = Veiculo.Cadastrar(Guid.CreateVersion7(), "ABC1D23", "VW", "Gol", 2020);
        _veiculos.ObterPorIdAsync(veiculo.Id, Arg.Any<CancellationToken>()).Returns(veiculo);

        await _servico.InativarAsync(veiculo.Id, "Vendido");
        veiculo.Ativo.ShouldBeFalse();

        await _servico.ReativarAsync(veiculo.Id);
        veiculo.Ativo.ShouldBeTrue();
    }
}
