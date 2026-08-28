using System.Text.RegularExpressions;
using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Veiculos.ValueObjects;

/// <summary>Padrão de emplacamento reconhecido pelo domínio.</summary>
public enum PadraoPlaca
{
    /// <summary>Padrão brasileiro anterior: 3 letras + 4 dígitos (ABC1234).</summary>
    Brasileiro = 1,

    /// <summary>Padrão Mercosul: 3 letras + dígito + letra + 2 dígitos (ABC1D23).</summary>
    Mercosul = 2
}

/// <summary>
/// Placa do veículo — chave natural de identificação do veículo dentro da oficina.
/// Aceita tanto o padrão brasileiro antigo quanto o Mercosul, ambos com 7 caracteres,
/// e normaliza a entrada (remove hífen/espaços e converte para caixa alta) para que
/// "abc-1234" e "ABC1234" sejam reconhecidas como a mesma placa.
/// </summary>
public sealed partial class Placa : ValueObject
{
    private Placa(string valor, PadraoPlaca padrao)
    {
        Valor = valor;
        Padrao = padrao;
    }

    /// <summary>Placa normalizada, sem separadores e em caixa alta.</summary>
    public string Valor { get; }

    public PadraoPlaca Padrao { get; }

    /// <exception cref="DomainException">Placa em branco ou fora dos padrões aceitos.</exception>
    public static Placa Criar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            throw new DomainException("PLACA_OBRIGATORIA", "A placa do veículo é obrigatória.");
        }

        var normalizada = new string([.. entrada.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);

        if (FormatoMercosul().IsMatch(normalizada))
        {
            return new Placa(normalizada, PadraoPlaca.Mercosul);
        }

        if (FormatoBrasileiroAntigo().IsMatch(normalizada))
        {
            return new Placa(normalizada, PadraoPlaca.Brasileiro);
        }

        throw new DomainException(
            "PLACA_INVALIDA",
            $"Placa '{entrada}' é inválida. Formatos aceitos: ABC1234 (brasileiro) ou ABC1D23 (Mercosul).");
    }

    public static bool TentarCriar(string? entrada, out Placa? placa)
    {
        try
        {
            placa = Criar(entrada);
            return true;
        }
        catch (DomainException)
        {
            placa = null;
            return false;
        }
    }

    /// <summary>Placa com hífen, para exibição (ABC-1234). O padrão Mercosul não usa separador.</summary>
    public string Formatada => Padrao == PadraoPlaca.Brasileiro
        ? $"{Valor[..3]}-{Valor[3..]}"
        : Valor;

    protected override IEnumerable<object?> ObterComponentesDeIgualdade()
    {
        yield return Valor;
    }

    public override string ToString() => Formatada;

    [GeneratedRegex("^[A-Z]{3}[0-9]{4}$")]
    private static partial Regex FormatoBrasileiroAntigo();

    [GeneratedRegex("^[A-Z]{3}[0-9][A-Z][0-9]{2}$")]
    private static partial Regex FormatoMercosul();
}
