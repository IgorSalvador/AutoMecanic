using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Infrastructure.Seguranca;
using Microsoft.Extensions.Options;

namespace AutoMecanic.UnitTests.Infraestrutura;

/// <summary>
/// Verifica o hash de senha. Estes testes exercitam a biblioteca real (BCrypt) e não um
/// dublê: é a única forma de garantir as propriedades que importam — salt por hash e
/// verificação correta.
/// </summary>
public sealed class ServicoDeHashDeSenhaBCryptTests
{
    private readonly ServicoDeHashDeSenhaBCrypt _hasher = new();

    [Fact]
    public void GerarHash_NuncaDevolveASenhaEmClaro()
    {
        const string senha = "Senha@Forte1";

        var hash = _hasher.GerarHash(senha);

        hash.ShouldNotBe(senha);
        hash.ShouldNotContain(senha);
        hash.ShouldStartWith("$2");
    }

    [Fact]
    public void GerarHash_ParaAMesmaSenha_ProduzHashesDiferentes()
    {
        const string senha = "Senha@Forte1";

        var primeiro = _hasher.GerarHash(senha);
        var segundo = _hasher.GerarHash(senha);

        // Salt aleatório por hash: dois usuários com a mesma senha têm hashes distintos,
        // e uma tabela arco-íris não serve para nada.
        primeiro.ShouldNotBe(segundo);

        _hasher.Verificar(senha, primeiro).ShouldBeTrue();
        _hasher.Verificar(senha, segundo).ShouldBeTrue();
    }

    [Fact]
    public void Verificar_ComSenhaCorreta_DevolveVerdadeiro()
    {
        var hash = _hasher.GerarHash("Senha@Forte1");

        _hasher.Verificar("Senha@Forte1", hash).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Senha@Forte2")]
    [InlineData("senha@forte1")]
    [InlineData("")]
    public void Verificar_ComSenhaIncorreta_DevolveFalso(string senha)
    {
        var hash = _hasher.GerarHash("Senha@Forte1");

        _hasher.Verificar(senha, hash).ShouldBeFalse();
    }

    [Theory]
    [InlineData("nao-e-um-hash")]
    [InlineData("")]
    [InlineData("$2a$corrompido")]
    public void Verificar_ComHashCorrompido_DevolveFalsoSemLancar(string hash) =>
        // Hash inválido no banco é tratado como falha de autenticação, nunca como erro
        // do servidor: um 500 aqui revelaria detalhe do armazenamento.
        _hasher.Verificar("Senha@Forte1", hash).ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GerarHash_ComSenhaVazia_Rejeita(string? senha) =>
        Should.Throw<ArgumentException>(() => _hasher.GerarHash(senha!));

    [Fact]
    public void GerarHash_UsaFatorDeCustoAdequado()
    {
        var hash = _hasher.GerarHash("Senha@Forte1");

        // O fator de custo aparece no próprio hash: "$2a$12$...". Abaixo de 12, a
        // verificação fica rápida demais e a força bruta volta a ser viável.
        hash.Split('$')[2].ShouldBe("12");
    }
}

/// <summary>Verifica a emissão de tokens JWT.</summary>
public sealed class GeradorDeTokenJwtTests
{
    private const string ChaveDeTeste = "chave-de-teste-com-mais-de-32-caracteres-para-hmac-sha256";

    private readonly GeradorDeTokenJwt _gerador = new(Options.Create(new OpcoesDeJwt
    {
        Emissor = "AutoMecanic.Api",
        Audiencia = "AutoMecanic.Clientes",
        ChaveDeAssinatura = ChaveDeTeste,
        ValidadeEmMinutos = 60
    }));

    [Fact]
    public void Gerar_ProduzTokenComEmissorAudienciaEExpiracao()
    {
        var usuario = CriarUsuario(PerfilUsuario.Administrador);

        var token = _gerador.Gerar(usuario);

        token.TipoToken.ShouldBe("Bearer");
        token.ExpiraEm.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        var lido = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        lido.Issuer.ShouldBe("AutoMecanic.Api");
        lido.Audiences.ShouldContain("AutoMecanic.Clientes");
        lido.SignatureAlgorithm.ShouldBe("HS256");
    }

    [Fact]
    public void Gerar_IncluiIdentidadeEPerfilNasReivindicacoes()
    {
        var usuario = CriarUsuario(PerfilUsuario.Mecanico);

        var lido = new JwtSecurityTokenHandler().ReadJwtToken(_gerador.Gerar(usuario).Token);

        lido.Claims.ShouldContain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == usuario.Id.ToString());
        lido.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == nameof(PerfilUsuario.Mecanico));
    }

    [Fact]
    public void Gerar_NuncaIncluiOHashDaSenhaNoToken()
    {
        var usuario = CriarUsuario(PerfilUsuario.Atendente);

        var token = _gerador.Gerar(usuario).Token;
        var lido = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // O conteúdo de um JWT é assinado, mas não é criptografado: qualquer pessoa que
        // intercepte o token consegue lê-lo. Nada sensível pode entrar nas reivindicações.
        lido.Claims.ShouldNotContain(c => c.Value == usuario.SenhaHash);
        token.ShouldNotContain(usuario.SenhaHash);
    }

    [Fact]
    public void Gerar_DoisTokensParaOMesmoUsuario_TemIdentificadoresDistintos()
    {
        var usuario = CriarUsuario(PerfilUsuario.Atendente);
        var manipulador = new JwtSecurityTokenHandler();

        var primeiro = manipulador.ReadJwtToken(_gerador.Gerar(usuario).Token);
        var segundo = manipulador.ReadJwtToken(_gerador.Gerar(usuario).Token);

        // O "jti" permite revogar e auditar uma sessão específica.
        primeiro.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value
            .ShouldNotBe(segundo.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void Gerar_ComUsuarioNulo_Rejeita() =>
        Should.Throw<ArgumentNullException>(() => _gerador.Gerar(null!));

    private static Usuario CriarUsuario(PerfilUsuario perfil) =>
        Usuario.Criar("Fulano de Tal", "fulano@automecanic.com.br", "Senha@Forte1", perfil, s => $"hash::{s}");
}

/// <summary>Verifica as opções de configuração de JWT e o relógio do sistema.</summary>
public sealed class ConfiguracaoDeSegurancaTests
{
    [Fact]
    public void OpcoesDeJwt_TemValidadePadraoConservadora() =>
        new OpcoesDeJwt().ValidadeEmMinutos.ShouldBe(60);

    [Fact]
    public void OpcoesDeJwt_NaoTemChaveDeAssinaturaPadrao() =>
        // Uma chave embutida no código seria pública no repositório e permitiria a
        // qualquer pessoa forjar tokens de administrador.
        new OpcoesDeJwt().ChaveDeAssinatura.ShouldBeEmpty();

    [Fact]
    public void ProvedorDeDataHora_DevolveHorarioEmUtc()
    {
        var agora = new Infrastructure.Servicos.ProvedorDeDataHora().Agora;

        agora.Offset.ShouldBe(TimeSpan.Zero);
        agora.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}
