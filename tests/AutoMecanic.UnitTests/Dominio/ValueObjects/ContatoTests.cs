using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Clientes.ValueObjects;

namespace AutoMecanic.UnitTests.Dominio.ValueObjects;

/// <summary>E-mail: canal pelo qual o orçamento chega ao cliente.</summary>
public sealed class EmailTests
{
    [Theory]
    [InlineData("cliente@exemplo.com.br")]
    [InlineData("nome.sobrenome@empresa.com")]
    [InlineData("usuario+marcador@dominio.io")]
    [InlineData("a@b.co")]
    public void Criar_ComEnderecoValido_Aceita(string entrada) =>
        Email.Criar(entrada).Endereco.ShouldBe(entrada.ToLowerInvariant());

    [Fact]
    public void Criar_NormalizaParaMinusculasESemEspacos() =>
        Email.Criar("  Cliente@Exemplo.COM.BR  ").Endereco.ShouldBe("cliente@exemplo.com.br");

    [Theory]
    [InlineData("sem-arroba.com")]
    [InlineData("@sem-usuario.com")]
    [InlineData("sem-dominio@")]
    [InlineData("espaco no@meio.com")]
    [InlineData("duplo@@arroba.com")]
    [InlineData("sem@tld")]
    public void Criar_ComFormatoInvalido_Rejeita(string entrada) =>
        Should.Throw<DomainException>(() => Email.Criar(entrada))
            .Codigo.ShouldBe("EMAIL_INVALIDO");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComEntradaVazia_Rejeita(string? entrada) =>
        Should.Throw<DomainException>(() => Email.Criar(entrada))
            .Codigo.ShouldBe("EMAIL_OBRIGATORIO");

    [Fact]
    public void Criar_AcimaDoLimiteDaRfc_Rejeita()
    {
        var longo = new string('a', 250) + "@exemplo.com";

        Should.Throw<DomainException>(() => Email.Criar(longo))
            .Codigo.ShouldBe("EMAIL_INVALIDO");
    }

    [Fact]
    public void Igualdade_IgnoraCaixa() =>
        Email.Criar("A@B.com").ShouldBe(Email.Criar("a@b.com"));
}

/// <summary>Telefone brasileiro com DDD, fixo ou celular.</summary>
public sealed class TelefoneTests
{
    [Theory]
    [InlineData("11987654321", true)]
    [InlineData("(11) 98765-4321", true)]
    [InlineData("1133224455", false)]
    [InlineData("(11) 3322-4455", false)]
    public void Criar_ComNumeroValido_ClassificaFixoOuCelular(string entrada, bool ehCelular)
    {
        var telefone = Telefone.Criar(entrada);

        telefone.EhCelular.ShouldBe(ehCelular);
        telefone.Ddd.ShouldBe(11);
    }

    [Theory]
    [InlineData("+55 11 98765-4321")]
    [InlineData("5511987654321")]
    public void Criar_ComPrefixoInternacional_RemoveOCodigoDoPais(string entrada) =>
        Telefone.Criar(entrada).Numero.ShouldBe("11987654321");

    [Theory]
    [InlineData("987654321")]      // sem DDD
    [InlineData("119876543210")]   // dígitos demais
    public void Criar_ComComprimentoInvalido_Rejeita(string entrada) =>
        Should.Throw<DomainException>(() => Telefone.Criar(entrada))
            .Codigo.ShouldBe("TELEFONE_INVALIDO");

    [Theory]
    [InlineData("10987654321")] // DDD 10 não existe
    [InlineData("2087654321")]  // DDD 20 não existe
    [InlineData("2387654321")]  // DDD 23 não existe
    public void Criar_ComDddInexistente_Rejeita(string entrada) =>
        Should.Throw<DomainException>(() => Telefone.Criar(entrada))
            .Codigo.ShouldBe("TELEFONE_INVALIDO");

    [Fact]
    public void Criar_ComCelularSemNonoDigito_Rejeita() =>
        // 11 dígitos obriga o nono dígito 9 após o DDD.
        Should.Throw<DomainException>(() => Telefone.Criar("11887654321"))
            .Codigo.ShouldBe("TELEFONE_INVALIDO");

    [Theory]
    [InlineData("1103224455")]
    [InlineData("1113224455")]
    public void Criar_ComFixoIniciadoEmZeroOuUm_Rejeita(string entrada) =>
        Should.Throw<DomainException>(() => Telefone.Criar(entrada))
            .Codigo.ShouldBe("TELEFONE_INVALIDO");

    [Fact]
    public void Formatado_AplicaMascaraConformeOTipo()
    {
        Telefone.Criar("11987654321").Formatado.ShouldBe("(11) 98765-4321");
        Telefone.Criar("1133224455").Formatado.ShouldBe("(11) 3322-4455");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Criar_ComEntradaVazia_Rejeita(string? entrada) =>
        Should.Throw<DomainException>(() => Telefone.Criar(entrada))
            .Codigo.ShouldBe("TELEFONE_OBRIGATORIO");
}

/// <summary>Endereço opcional do cliente, exigido completo quando informado.</summary>
public sealed class EnderecoTests
{
    [Fact]
    public void Criar_ComDadosCompletos_NormalizaUfECep()
    {
        var endereco = Endereco.Criar("Rua das Oficinas", "1500", "Sala 2", "Centro", "São Paulo", "sp", "01310-100");

        endereco.Uf.ShouldBe("SP");
        endereco.Cep.ShouldBe("01310100");
        endereco.CepFormatado.ShouldBe("01310-100");
        endereco.Complemento.ShouldBe("Sala 2");
    }

    [Fact]
    public void Criar_SemComplemento_AceitaNulo() =>
        Endereco.Criar("Rua A", "10", "  ", "Centro", "Santos", "SP", "11010000")
            .Complemento.ShouldBeNull();

    [Theory]
    [InlineData("XX")]
    [InlineData("São Paulo")]
    [InlineData("")]
    public void Criar_ComUfInvalida_Rejeita(string uf) =>
        Should.Throw<DomainException>(() =>
            Endereco.Criar("Rua A", "10", null, "Centro", "Santos", uf, "11010000"))
            .Codigo.ShouldBe("ENDERECO_INVALIDO");

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("abcdefgh")]
    public void Criar_ComCepInvalido_Rejeita(string cep) =>
        Should.Throw<DomainException>(() =>
            Endereco.Criar("Rua A", "10", null, "Centro", "Santos", "SP", cep))
            .Codigo.ShouldBe("ENDERECO_INVALIDO");

    [Fact]
    public void Criar_SemLogradouro_Rejeita() =>
        Should.Throw<DomainException>(() =>
            Endereco.Criar(" ", "10", null, "Centro", "Santos", "SP", "11010000"))
            .Message.ShouldContain("logradouro");

    [Fact]
    public void Igualdade_EhEstruturalEmTodosOsCampos()
    {
        var primeiro = Endereco.Criar("Rua A", "10", null, "Centro", "Santos", "SP", "11010000");
        var segundo = Endereco.Criar("Rua A", "10", null, "Centro", "Santos", "SP", "11010000");
        var diferente = Endereco.Criar("Rua A", "11", null, "Centro", "Santos", "SP", "11010000");

        primeiro.ShouldBe(segundo);
        primeiro.ShouldNotBe(diferente);
    }
}
