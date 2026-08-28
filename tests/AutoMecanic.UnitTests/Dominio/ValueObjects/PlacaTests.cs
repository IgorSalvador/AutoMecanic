using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Veiculos.ValueObjects;

namespace AutoMecanic.UnitTests.Dominio.ValueObjects;

/// <summary>
/// A placa é o outro dado sensível cuja validação o requisito exige explicitamente.
/// A oficina convive com os dois padrões em circulação, e a normalização precisa fazer
/// "abc-1234" e "ABC1234" serem reconhecidas como o mesmo veículo.
/// </summary>
public sealed class PlacaTests
{
    [Theory]
    [InlineData("ABC1234")]
    [InlineData("abc1234")]
    [InlineData("ABC-1234")]
    [InlineData(" abc 1234 ")]
    public void Criar_ComPadraoBrasileiro_NormalizaEClassifica(string entrada)
    {
        var placa = Placa.Criar(entrada);

        placa.Valor.ShouldBe("ABC1234");
        placa.Padrao.ShouldBe(PadraoPlaca.Brasileiro);
        placa.Formatada.ShouldBe("ABC-1234");
    }

    [Theory]
    [InlineData("ABC1D23")]
    [InlineData("abc1d23")]
    [InlineData("BRA2E19")]
    public void Criar_ComPadraoMercosul_NormalizaEClassifica(string entrada)
    {
        var placa = Placa.Criar(entrada);

        placa.Padrao.ShouldBe(PadraoPlaca.Mercosul);

        // O padrão Mercosul não usa separador: formatar não deve inventar um hífen.
        placa.Formatada.ShouldBe(placa.Valor);
    }

    [Theory]
    [InlineData("AB1234")]     // letras de menos
    [InlineData("ABCD123")]    // letra a mais na posição do dígito
    [InlineData("ABC12345")]   // dígitos demais
    [InlineData("1234ABC")]    // ordem invertida
    [InlineData("ABC12D3")]    // letra na posição errada do padrão Mercosul
    [InlineData("AAAAAAA")]
    public void Criar_ComFormatoInvalido_Rejeita(string entrada) =>
        Should.Throw<DomainException>(() => Placa.Criar(entrada))
            .Codigo.ShouldBe("PLACA_INVALIDA");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Criar_ComEntradaVazia_Rejeita(string? entrada) =>
        Should.Throw<DomainException>(() => Placa.Criar(entrada))
            .Codigo.ShouldBe("PLACA_OBRIGATORIA");

    [Fact]
    public void Igualdade_IgnoraMascaraECaixa()
    {
        var comHifen = Placa.Criar("ABC-1234");
        var minuscula = Placa.Criar("abc1234");

        comHifen.ShouldBe(minuscula);
        comHifen.GetHashCode().ShouldBe(minuscula.GetHashCode());
    }

    [Fact]
    public void Igualdade_ComPlacasDiferentes_NaoSaoIguais() =>
        Placa.Criar("ABC1234").ShouldNotBe(Placa.Criar("XYZ4567"));

    [Fact]
    public void TentarCriar_ComPlacaInvalida_DevolveFalsoSemLancar()
    {
        Placa.TentarCriar("INVALIDA", out var placa).ShouldBeFalse();

        placa.ShouldBeNull();
    }

    [Fact]
    public void TentarCriar_ComPlacaValida_DevolveVerdadeiro()
    {
        Placa.TentarCriar("ABC1D23", out var placa).ShouldBeTrue();

        placa!.Valor.ShouldBe("ABC1D23");
    }
}
