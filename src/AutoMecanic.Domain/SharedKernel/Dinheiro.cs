using System.Globalization;
using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.SharedKernel;

/// <summary>
/// Objeto de Valor monetário em Real (BRL). Existe para que valores de orçamento nunca
/// trafeguem como <c>decimal</c> solto: centraliza o arredondamento em 2 casas (padrão
/// bancário), proíbe valores negativos e oferece as operações que o negócio realmente usa.
/// </summary>
public sealed class Dinheiro : ValueObject, IComparable<Dinheiro>
{
    private static readonly CultureInfo CulturaBrasileira = CultureInfo.GetCultureInfo("pt-BR");

    private Dinheiro(decimal valor) => Valor = valor;

    /// <summary>Instância neutra para acumuladores (R$ 0,00).</summary>
    public static Dinheiro Zero { get; } = new(0m);

    public decimal Valor { get; }

    /// <summary>
    /// Cria um valor monetário. O arredondamento é <see cref="MidpointRounding.ToEven"/>
    /// para evitar viés sistemático de arredondamento na soma de muitos itens de orçamento.
    /// </summary>
    public static Dinheiro De(decimal valor)
    {
        if (valor < 0)
        {
            throw new DomainException("Valor monetário não pode ser negativo.");
        }

        if (valor > 9_999_999.99m)
        {
            throw new DomainException("Valor monetário excede o limite permitido de R$ 9.999.999,99.");
        }

        return new Dinheiro(Math.Round(valor, 2, MidpointRounding.ToEven));
    }

    public Dinheiro Somar(Dinheiro outro) => De(Valor + outro.Valor);

    public Dinheiro Subtrair(Dinheiro outro) => De(Valor - outro.Valor);

    public Dinheiro Multiplicar(int quantidade)
    {
        if (quantidade < 0)
        {
            throw new DomainException("Quantidade multiplicadora não pode ser negativa.");
        }

        return De(Valor * quantidade);
    }

    /// <summary>Aplica um desconto percentual (0 a 100).</summary>
    public Dinheiro AplicarDescontoPercentual(decimal percentual)
    {
        if (percentual is < 0 or > 100)
        {
            throw new DomainException("Percentual de desconto deve estar entre 0 e 100.");
        }

        return De(Valor * (1 - (percentual / 100m)));
    }

    public bool EhZero => Valor == 0m;

    public static Dinheiro operator +(Dinheiro esquerda, Dinheiro direita) => esquerda.Somar(direita);

    public static Dinheiro operator -(Dinheiro esquerda, Dinheiro direita) => esquerda.Subtrair(direita);

    public static Dinheiro operator *(Dinheiro valor, int quantidade) => valor.Multiplicar(quantidade);

    public static bool operator >(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor > direita.Valor;

    public static bool operator <(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor < direita.Valor;

    public static bool operator >=(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor >= direita.Valor;

    public static bool operator <=(Dinheiro esquerda, Dinheiro direita) => esquerda.Valor <= direita.Valor;

    public int CompareTo(Dinheiro? other) => other is null ? 1 : Valor.CompareTo(other.Valor);

    protected override IEnumerable<object?> ObterComponentesDeIgualdade()
    {
        yield return Valor;
    }

    public override string ToString() => Valor.ToString("C2", CulturaBrasileira);
}
