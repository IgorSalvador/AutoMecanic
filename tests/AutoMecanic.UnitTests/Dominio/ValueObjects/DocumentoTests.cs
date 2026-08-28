using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Clientes.ValueObjects;

namespace AutoMecanic.UnitTests.Dominio.ValueObjects;

/// <summary>
/// O documento é a chave natural do cliente e o principal dado sensível validado pelo
/// requisito. Estes testes cobrem o cálculo do dígito verificador, a normalização de
/// máscaras e a rejeição das entradas que o algoritmo ingênuo deixaria passar.
/// </summary>
public sealed class DocumentoTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    [InlineData(" 529 982 247 25 ")]
    [InlineData("16899535009")]
    [InlineData("168.995.350-09")]
    public void Criar_ComCpfValido_ReconhecePessoaFisica(string entrada)
    {
        var documento = Documento.Criar(entrada);

        documento.Tipo.ShouldBe(TipoPessoa.Fisica);
        documento.EhCpf.ShouldBeTrue();
        documento.EhCnpj.ShouldBeFalse();
        documento.Numero.Length.ShouldBe(11);
    }

    [Theory]
    [InlineData("34028316000103")]
    [InlineData("34.028.316/0001-03")]
    [InlineData("11222333000181")]
    public void Criar_ComCnpjValido_ReconhecePessoaJuridica(string entrada)
    {
        var documento = Documento.Criar(entrada);

        documento.Tipo.ShouldBe(TipoPessoa.Juridica);
        documento.EhCnpj.ShouldBeTrue();
        documento.Numero.Length.ShouldBe(14);
    }

    [Theory]
    [InlineData("52998224726")] // último dígito verificador trocado
    [InlineData("52998224715")] // primeiro dígito verificador trocado
    [InlineData("12345678900")]
    public void Criar_ComDigitoVerificadorIncorreto_Rejeita(string entrada)
    {
        var excecao = Should.Throw<DomainException>(() => Documento.Criar(entrada));

        excecao.Codigo.ShouldBe("DOCUMENTO_INVALIDO");
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void Criar_ComCpfDeDigitosRepetidos_Rejeita(string entrada)
    {
        // Sequências repetidas passam no cálculo do módulo 11 — o resto é sempre 0 — mas
        // não são CPFs válidos. Sem a rejeição explícita, 111.111.111-11 seria aceito.
        Should.Throw<DomainException>(() => Documento.Criar(entrada))
            .Codigo.ShouldBe("DOCUMENTO_INVALIDO");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789012")] // 12 dígitos: nem CPF nem CNPJ
    [InlineData("1234567890123456")]
    public void Criar_ComComprimentoInvalido_Rejeita(string entrada) =>
        Should.Throw<DomainException>(() => Documento.Criar(entrada))
            .Codigo.ShouldBe("DOCUMENTO_INVALIDO");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComEntradaVazia_Rejeita(string? entrada) =>
        Should.Throw<DomainException>(() => Documento.Criar(entrada))
            .Codigo.ShouldBe("DOCUMENTO_OBRIGATORIO");

    [Fact]
    public void Formatado_ComCpf_AplicaMascaraDePessoaFisica() =>
        Documento.Criar("52998224725").Formatado.ShouldBe("529.982.247-25");

    [Fact]
    public void Formatado_ComCnpj_AplicaMascaraDePessoaJuridica() =>
        Documento.Criar("34028316000103").Formatado.ShouldBe("34.028.316/0001-03");

    [Fact]
    public void Igualdade_ComMesmoNumeroEmFormatosDiferentes_SaoIguais()
    {
        // Igualdade estrutural: é isso que faz a busca por documento funcionar
        // independentemente de o atendente digitar com ou sem máscara.
        var comMascara = Documento.Criar("529.982.247-25");
        var semMascara = Documento.Criar("52998224725");

        comMascara.ShouldBe(semMascara);
        (comMascara == semMascara).ShouldBeTrue();
        comMascara.GetHashCode().ShouldBe(semMascara.GetHashCode());
    }

    [Fact]
    public void Igualdade_ComDocumentosDiferentes_NaoSaoIguais() =>
        Documento.Criar("52998224725").ShouldNotBe(Documento.Criar("16899535009"));

    [Fact]
    public void TentarCriar_ComDocumentoValido_DevolveVerdadeiroEInstancia()
    {
        Documento.TentarCriar("52998224725", out var documento).ShouldBeTrue();

        documento.ShouldNotBeNull();
        documento.Numero.ShouldBe("52998224725");
    }

    [Fact]
    public void TentarCriar_ComDocumentoInvalido_DevolveFalsoSemLancar()
    {
        Documento.TentarCriar("00000000000", out var documento).ShouldBeFalse();

        documento.ShouldBeNull();
    }

    [Fact]
    public void Criar_ComCnpjAlfanumerico_AceitaOFormatoVigente()
    {
        // O CNPJ alfanumérico usa 12 posições alfanuméricas seguidas de 2 dígitos
        // verificadores, calculados com ASCII-48. O algoritmo precisa aceitar letras
        // sem quebrar a compatibilidade com o CNPJ puramente numérico.
        var raiz = "12ABC34501DE";
        var digitos = CalcularDigitosDeCnpj(raiz);

        var documento = Documento.Criar(raiz + digitos);

        documento.Tipo.ShouldBe(TipoPessoa.Juridica);
        documento.Numero.ShouldBe(raiz + digitos);
    }

    [Fact]
    public void Criar_ComCnpjAlfanumericoEmMinusculas_NormalizaParaCaixaAlta()
    {
        var raiz = "12ABC34501DE";
        var completo = raiz + CalcularDigitosDeCnpj(raiz);

        Documento.Criar(completo.ToLowerInvariant()).Numero.ShouldBe(completo);
    }

    /// <summary>Reimplementa o módulo 11 do CNPJ para produzir casos de teste válidos.</summary>
    private static string CalcularDigitosDeCnpj(string raiz)
    {
        int[] pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        var primeiro = Digito(raiz, pesos1);
        var segundo = Digito(raiz + primeiro, pesos2);

        return $"{primeiro}{segundo}";

        static char Digito(string baseCalculo, int[] pesos)
        {
            var soma = 0;

            for (var i = 0; i < baseCalculo.Length; i++)
            {
                soma += (baseCalculo[i] - '0') * pesos[i];
            }

            var resto = soma % 11;

            return (char)('0' + (resto < 2 ? 0 : 11 - resto));
        }
    }
}
