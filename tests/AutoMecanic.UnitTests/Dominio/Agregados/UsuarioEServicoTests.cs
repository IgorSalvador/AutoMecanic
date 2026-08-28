using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.Identidade.Events;
using AutoMecanic.Domain.OrdensServico.ValueObjects;
using AutoMecanic.Domain.Servicos;
using AutoMecanic.Domain.Servicos.Events;

namespace AutoMecanic.UnitTests.Dominio.Agregados;

/// <summary>
/// O agregado Usuário concentra as regras de segurança que o requisito pede: política de
/// senha e proteção contra força bruta. O hash é injetado como função, mantendo o domínio
/// livre de dependência de biblioteca de criptografia.
/// </summary>
public sealed class UsuarioTests
{
    // Hash falso e determinístico: os testes verificam a lógica do agregado,
    // não a implementação criptográfica (essa é testada em BCrypt à parte).
    private static readonly Func<string, string> GerarHash = senha => $"hash::{senha}";
    private static readonly Func<string, string, bool> Verificar = (senha, hash) => hash == $"hash::{senha}";

    private const string SenhaValida = "Senha@Forte1";

    [Fact]
    public void Criar_ComDadosValidos_NasceAtivoSemTentativasEPublicaEvento()
    {
        var usuario = CriarUsuario();

        usuario.Ativo.ShouldBeTrue();
        usuario.TentativasFalhas.ShouldBe(0);
        usuario.BloqueadoAte.ShouldBeNull();
        usuario.Perfil.ShouldBe(PerfilUsuario.Atendente);

        // A senha em claro nunca é armazenada: o agregado guarda apenas o hash
        // produzido pela função injetada pela infraestrutura.
        usuario.SenhaHash.ShouldBe($"hash::{SenhaValida}");

        usuario.EventosDeDominio.OfType<UsuarioCriado>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("curta1!", "menos de 8 caracteres")]
    [InlineData("semmaiuscula1!", "sem maiúscula")]
    [InlineData("SEMMINUSCULA1!", "sem minúscula")]
    [InlineData("SemDigito!!", "sem dígito")]
    [InlineData("SemSimbolo123", "sem caractere especial")]
    public void Criar_ComSenhaForaDaPolitica_Rejeita(string senha, string _) =>
        Should.Throw<DomainException>(() =>
            Usuario.Criar("Fulano de Tal", "f@e.com", senha, PerfilUsuario.Atendente, GerarHash))
            .Codigo.ShouldBe("SENHA_FRACA");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Criar_SemSenha_Rejeita(string? senha) =>
        Should.Throw<DomainException>(() =>
            Usuario.Criar("Fulano de Tal", "f@e.com", senha, PerfilUsuario.Atendente, GerarHash))
            .Codigo.ShouldBe("SENHA_OBRIGATORIA");

    [Fact]
    public void TentarAutenticar_ComSenhaCorreta_AutorizaEZeraContador()
    {
        var usuario = CriarUsuario();
        var agora = DateTimeOffset.UtcNow;
        usuario.LimparEventos();

        usuario.TentarAutenticar(SenhaValida, Verificar, agora).ShouldBeTrue();

        usuario.TentativasFalhas.ShouldBe(0);
        usuario.UltimoAcessoEm.ShouldBe(agora);
        usuario.EventosDeDominio.OfType<UsuarioAutenticado>().ShouldHaveSingleItem();
    }

    [Fact]
    public void TentarAutenticar_ComSenhaErrada_IncrementaOContador()
    {
        var usuario = CriarUsuario();

        usuario.TentarAutenticar("Errada@123", Verificar, DateTimeOffset.UtcNow).ShouldBeFalse();

        usuario.TentativasFalhas.ShouldBe(1);
        usuario.BloqueadoAte.ShouldBeNull();
    }

    [Fact]
    public void TentarAutenticar_AposCincoFalhas_BloqueiaAConta()
    {
        var usuario = CriarUsuario();
        var agora = DateTimeOffset.UtcNow;

        for (var tentativa = 0; tentativa < Usuario.MaximoTentativasFalhas; tentativa++)
        {
            usuario.TentarAutenticar("Errada@123", Verificar, agora);
        }

        usuario.TentativasFalhas.ShouldBe(Usuario.MaximoTentativasFalhas);
        usuario.EstaBloqueado(agora).ShouldBeTrue();
        usuario.EventosDeDominio.OfType<UsuarioBloqueado>().ShouldHaveSingleItem();
    }

    [Fact]
    public void TentarAutenticar_ComContaBloqueada_RecusaAteComSenhaCorreta()
    {
        var usuario = CriarUsuario();
        var agora = DateTimeOffset.UtcNow;

        for (var tentativa = 0; tentativa < Usuario.MaximoTentativasFalhas; tentativa++)
        {
            usuario.TentarAutenticar("Errada@123", Verificar, agora);
        }

        // Enquanto o bloqueio vale, nem a senha correta entra: é isso que torna a
        // força bruta inviável em vez de apenas mais lenta.
        Should.Throw<DomainException>(() => usuario.TentarAutenticar(SenhaValida, Verificar, agora))
            .Codigo.ShouldBe("USUARIO_BLOQUEADO");
    }

    [Fact]
    public void TentarAutenticar_AposExpirarOBloqueio_VoltaAAceitar()
    {
        var usuario = CriarUsuario();
        var agora = DateTimeOffset.UtcNow;

        for (var tentativa = 0; tentativa < Usuario.MaximoTentativasFalhas; tentativa++)
        {
            usuario.TentarAutenticar("Errada@123", Verificar, agora);
        }

        var depoisDoBloqueio = agora.Add(Usuario.DuracaoDoBloqueio).AddMinutes(1);

        usuario.TentarAutenticar(SenhaValida, Verificar, depoisDoBloqueio).ShouldBeTrue();
        usuario.TentativasFalhas.ShouldBe(0);
    }

    [Fact]
    public void TentarAutenticar_ComUsuarioInativo_Rejeita()
    {
        var usuario = CriarUsuario();
        usuario.Inativar();

        Should.Throw<DomainException>(() => usuario.TentarAutenticar(SenhaValida, Verificar, DateTimeOffset.UtcNow))
            .Codigo.ShouldBe("USUARIO_INATIVO");
    }

    [Fact]
    public void AlterarSenha_ComSenhaAtualCorreta_TrocaOHash()
    {
        var usuario = CriarUsuario();
        usuario.LimparEventos();

        usuario.AlterarSenha(SenhaValida, "NovaSenha@2", Verificar, GerarHash);

        usuario.SenhaHash.ShouldBe("hash::NovaSenha@2");
        usuario.EventosDeDominio.OfType<SenhaAlterada>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AlterarSenha_ComSenhaAtualIncorreta_Rejeita() =>
        Should.Throw<DomainException>(() =>
            CriarUsuario().AlterarSenha("Errada@123", "NovaSenha@2", Verificar, GerarHash))
            .Codigo.ShouldBe("SENHA_ATUAL_INVALIDA");

    [Fact]
    public void AlterarSenha_RepetindoASenhaAtual_Rejeita() =>
        Should.Throw<DomainException>(() =>
            CriarUsuario().AlterarSenha(SenhaValida, SenhaValida, Verificar, GerarHash))
            .Codigo.ShouldBe("SENHA_REPETIDA");

    [Fact]
    public void RedefinirSenha_LiberaBloqueiosSemExigirASenhaAnterior()
    {
        var usuario = CriarUsuario();
        var agora = DateTimeOffset.UtcNow;

        for (var tentativa = 0; tentativa < Usuario.MaximoTentativasFalhas; tentativa++)
        {
            usuario.TentarAutenticar("Errada@123", Verificar, agora);
        }

        usuario.RedefinirSenha("Redefinida@9", GerarHash);

        usuario.TentativasFalhas.ShouldBe(0);
        usuario.BloqueadoAte.ShouldBeNull();
        usuario.SenhaHash.ShouldBe("hash::Redefinida@9");
    }

    [Fact]
    public void Desbloquear_LimpaOContadorEOBloqueio()
    {
        var usuario = CriarUsuario();
        var agora = DateTimeOffset.UtcNow;

        for (var tentativa = 0; tentativa < Usuario.MaximoTentativasFalhas; tentativa++)
        {
            usuario.TentarAutenticar("Errada@123", Verificar, agora);
        }

        usuario.Desbloquear();

        usuario.EstaBloqueado(agora).ShouldBeFalse();
        usuario.TentativasFalhas.ShouldBe(0);
    }

    [Fact]
    public void AlterarPerfil_TrocaOPerfil()
    {
        var usuario = CriarUsuario();

        usuario.AlterarPerfil(PerfilUsuario.Administrador);

        usuario.Perfil.ShouldBe(PerfilUsuario.Administrador);
    }

    private static Usuario CriarUsuario() =>
        Usuario.Criar("Fulano de Tal", "fulano@automecanic.com.br", SenhaValida, PerfilUsuario.Atendente, GerarHash);
}

public sealed class ServicoTests
{
    [Fact]
    public void Cadastrar_ComDadosValidos_NasceAtivoEPublicaEvento()
    {
        var servico = Servico.Cadastrar("Troca de óleo", "Óleo e filtro", CategoriaServico.ManutencaoPreventiva, 120m, 45);

        servico.Ativo.ShouldBeTrue();
        servico.Preco.Valor.ShouldBe(120m);
        servico.TempoEstimado.ShouldBe(TimeSpan.FromMinutes(45));

        servico.EventosDeDominio.OfType<ServicoCadastrado>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Cadastrar_ComPrecoNaoPositivo_Rejeita(decimal preco) =>
        Should.Throw<DomainException>(() =>
            Servico.Cadastrar("Serviço", null, CategoriaServico.Outros, preco, 30))
            .Codigo.ShouldBe("PRECO_INVALIDO");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(43_201)]
    public void Cadastrar_ComTempoEstimadoInvalido_Rejeita(int minutos) =>
        Should.Throw<DomainException>(() =>
            Servico.Cadastrar("Serviço", null, CategoriaServico.Outros, 100m, minutos))
            .Codigo.ShouldBe("TEMPO_ESTIMADO_INVALIDO");

    [Fact]
    public void Cadastrar_ComNomeMuitoCurto_Rejeita() =>
        Should.Throw<DomainException>(() =>
            Servico.Cadastrar("ab", null, CategoriaServico.Outros, 100m, 30))
            .Codigo.ShouldBe("NOME_INVALIDO");

    [Fact]
    public void ReajustarPreco_ComValorDiferente_PublicaEventoComOAnterior()
    {
        var servico = CriarServico();
        servico.LimparEventos();

        servico.ReajustarPreco(150m);

        var evento = servico.EventosDeDominio.OfType<PrecoDoServicoReajustado>().ShouldHaveSingleItem();
        evento.PrecoAnterior.ShouldBe(120m);
        evento.PrecoNovo.ShouldBe(150m);
    }

    [Fact]
    public void ReajustarPreco_ComOMesmoValor_NaoPublicaEvento()
    {
        var servico = CriarServico();
        servico.LimparEventos();

        servico.ReajustarPreco(120m);

        servico.EventosDeDominio.ShouldBeEmpty();
    }

    [Fact]
    public void GarantirServicoAtivo_ComServicoInativo_Lanca()
    {
        var servico = CriarServico();
        servico.Inativar();

        Should.Throw<DomainException>(servico.GarantirServicoAtivo)
            .Codigo.ShouldBe("SERVICO_INATIVO");
    }

    [Fact]
    public void AtualizarDados_ComServicoInativo_Rejeita()
    {
        var servico = CriarServico();
        servico.Inativar();

        Should.Throw<DomainException>(() =>
            servico.AtualizarDados("Novo nome", null, CategoriaServico.Outros, 60))
            .Codigo.ShouldBe("SERVICO_INATIVO");
    }

    private static Servico CriarServico() =>
        Servico.Cadastrar("Troca de óleo", "Óleo e filtro", CategoriaServico.ManutencaoPreventiva, 120m, 45);
}

public sealed class NumeroOrdemServicoTests
{
    [Fact]
    public void Gerar_FormataComAnoESequencialPreenchido() =>
        NumeroOrdemServico.Gerar(2026, 42).Valor.ShouldBe("OS-2026-000042");

    [Fact]
    public void Gerar_ComSequencialNoLimite_Aceita() =>
        NumeroOrdemServico.Gerar(2026, 999_999).Valor.ShouldBe("OS-2026-999999");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void Gerar_ComSequencialForaDaFaixa_Rejeita(int sequencial) =>
        Should.Throw<DomainException>(() => NumeroOrdemServico.Gerar(2026, sequencial))
            .Codigo.ShouldBe("NUMERO_OS_INVALIDO");

    [Theory]
    [InlineData(1999)]
    [InlineData(3000)]
    public void Gerar_ComAnoForaDaFaixa_Rejeita(int ano) =>
        Should.Throw<DomainException>(() => NumeroOrdemServico.Gerar(ano, 1))
            .Codigo.ShouldBe("NUMERO_OS_INVALIDO");

    [Theory]
    [InlineData("OS-2026-000042")]
    [InlineData("os-2026-000042")]
    [InlineData("  OS-2026-000042  ")]
    public void Analisar_ReconstroiONumero(string entrada)
    {
        var numero = NumeroOrdemServico.Analisar(entrada);

        numero.Ano.ShouldBe(2026);
        numero.Sequencial.ShouldBe(42);
    }

    [Theory]
    [InlineData("2026-000042")]
    [InlineData("OS-26-42")]
    [InlineData("OS-2026-42")]
    [InlineData("qualquer-coisa")]
    public void Analisar_ComFormatoInvalido_Rejeita(string entrada) =>
        Should.Throw<DomainException>(() => NumeroOrdemServico.Analisar(entrada))
            .Codigo.ShouldBe("NUMERO_OS_INVALIDO");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Analisar_ComEntradaVazia_Rejeita(string? entrada) =>
        Should.Throw<DomainException>(() => NumeroOrdemServico.Analisar(entrada))
            .Codigo.ShouldBe("NUMERO_OS_OBRIGATORIO");

    [Fact]
    public void Igualdade_EhEstrutural() =>
        NumeroOrdemServico.Gerar(2026, 1).ShouldBe(NumeroOrdemServico.Analisar("OS-2026-000001"));
}
