using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.UnitTests.Dominio.ValueObjects;

/// <summary>
/// Dinheiro concentra o arredondamento e a proibição de valores negativos. Como todo
/// orçamento passa por ele, um defeito aqui apareceria em cada valor apresentado ao cliente.
/// </summary>
public sealed class DinheiroTests
{
    [Fact]
    public void De_ComValorValido_PreservaOValor() =>
        Dinheiro.De(123.45m).Valor.ShouldBe(123.45m);

    [Fact]
    public void De_ComZero_EhPermitido()
    {
        var zero = Dinheiro.De(0m);

        zero.EhZero.ShouldBeTrue();
        zero.ShouldBe(Dinheiro.Zero);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void De_ComValorNegativo_Rejeita(decimal valor) =>
        Should.Throw<DomainException>(() => Dinheiro.De(valor));

    [Fact]
    public void De_AcimaDoLimite_Rejeita() =>
        Should.Throw<DomainException>(() => Dinheiro.De(10_000_000m));

    [Theory]
    [InlineData(10.994, 10.99)]
    [InlineData(10.996, 11.00)]
    [InlineData(10.125, 10.12)] // arredondamento bancário: 2 é par, permanece
    [InlineData(10.135, 10.14)] // arredondamento bancário: 3 é ímpar, sobe
    public void De_ArredondaParaDuasCasasComRegraBancaria(decimal entrada, decimal esperado) =>
        Dinheiro.De(entrada).Valor.ShouldBe(esperado);

    [Fact]
    public void Somar_AcumulaOsValores() =>
        Dinheiro.De(100.50m).Somar(Dinheiro.De(49.50m)).Valor.ShouldBe(150.00m);

    [Fact]
    public void Subtrair_QuandoResultadoSeriaNegativo_Rejeita() =>
        Should.Throw<DomainException>(() => Dinheiro.De(10m).Subtrair(Dinheiro.De(20m)));

    [Fact]
    public void Multiplicar_PorQuantidade_EscalaOValor() =>
        Dinheiro.De(48.90m).Multiplicar(4).Valor.ShouldBe(195.60m);

    [Fact]
    public void Multiplicar_PorZero_ResultaEmZero() =>
        Dinheiro.De(99m).Multiplicar(0).EhZero.ShouldBeTrue();

    [Fact]
    public void Multiplicar_PorQuantidadeNegativa_Rejeita() =>
        Should.Throw<DomainException>(() => Dinheiro.De(10m).Multiplicar(-1));

    [Theory]
    [InlineData(100, 0, 100)]
    [InlineData(100, 10, 90)]
    [InlineData(100, 100, 0)]
    [InlineData(199.90, 15, 169.92)]
    public void AplicarDescontoPercentual_CalculaOValorLiquido(decimal bruto, decimal percentual, decimal esperado) =>
        Dinheiro.De(bruto).AplicarDescontoPercentual(percentual).Valor.ShouldBe(esperado);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void AplicarDescontoPercentual_ForaDaFaixa_Rejeita(decimal percentual) =>
        Should.Throw<DomainException>(() => Dinheiro.De(100m).AplicarDescontoPercentual(percentual));

    [Fact]
    public void Operadores_DeComparacao_RefletemOValor()
    {
        var menor = Dinheiro.De(10m);
        var maior = Dinheiro.De(20m);

        (menor < maior).ShouldBeTrue();
        (maior > menor).ShouldBeTrue();
        (menor <= Dinheiro.De(10m)).ShouldBeTrue();
        (maior >= Dinheiro.De(20m)).ShouldBeTrue();
    }

    [Fact]
    public void Igualdade_EhEstrutural()
    {
        Dinheiro.De(50m).ShouldBe(Dinheiro.De(50m));
        Dinheiro.De(50m).GetHashCode().ShouldBe(Dinheiro.De(50m).GetHashCode());
        Dinheiro.De(50m).ShouldNotBe(Dinheiro.De(50.01m));
    }

    [Fact]
    public void ToString_FormataEmRealBrasileiro() =>
        Dinheiro.De(1234.50m).ToString().ShouldContain("1.234,50");

    [Fact]
    public void CompareTo_OrdenaPorValor()
    {
        var valores = new[] { Dinheiro.De(30m), Dinheiro.De(10m), Dinheiro.De(20m) };

        var ordenados = valores.OrderBy(v => v).Select(v => v.Valor).ToArray();

        ordenados.ShouldBe([10m, 20m, 30m]);
    }
}
