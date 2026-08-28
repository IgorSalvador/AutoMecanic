using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Clientes.ValueObjects;

/// <summary>
/// Telefone de contato brasileiro, com DDD. Aceita fixo (10 dígitos) e celular (11 dígitos,
/// com o nono dígito). O DDD é validado contra a faixa oficial (11 a 99, sem os códigos
/// não atribuídos), evitando cadastros com números impossíveis de contatar.
/// </summary>
public sealed class Telefone : ValueObject
{
    private static readonly HashSet<int> DddsValidos =
    [
        11, 12, 13, 14, 15, 16, 17, 18, 19,
        21, 22, 24, 27, 28,
        31, 32, 33, 34, 35, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48, 49,
        51, 53, 54, 55,
        61, 62, 63, 64, 65, 66, 67, 68, 69,
        71, 73, 74, 75, 77, 79,
        81, 82, 83, 84, 85, 86, 87, 88, 89,
        91, 92, 93, 94, 95, 96, 97, 98, 99
    ];

    private Telefone(string numero, bool ehCelular)
    {
        Numero = numero;
        EhCelular = ehCelular;
    }

    /// <summary>Somente dígitos, incluindo o DDD.</summary>
    public string Numero { get; }

    public bool EhCelular { get; }

    public int Ddd => int.Parse(Numero[..2]);

    /// <exception cref="DomainException">Telefone em branco, com tamanho inválido, DDD inexistente ou celular sem nono dígito.</exception>
    public static Telefone Criar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            throw new DomainException("TELEFONE_OBRIGATORIO", "O telefone é obrigatório.");
        }

        var digitos = new string([.. entrada.Where(char.IsDigit)]);

        // Tolera o prefixo internacional do Brasil, comum em dados vindos de aplicativos.
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55", StringComparison.Ordinal))
        {
            digitos = digitos[2..];
        }

        if (digitos.Length is not (10 or 11))
        {
            throw new DomainException(
                "TELEFONE_INVALIDO",
                $"Telefone '{entrada}' é inválido: informe DDD + número (10 ou 11 dígitos).");
        }

        if (!DddsValidos.Contains(int.Parse(digitos[..2])))
        {
            throw new DomainException("TELEFONE_INVALIDO", $"DDD '{digitos[..2]}' não existe.");
        }

        var ehCelular = digitos.Length == 11;

        if (ehCelular && digitos[2] != '9')
        {
            throw new DomainException("TELEFONE_INVALIDO", "Telefone celular com 11 dígitos deve iniciar com 9 após o DDD.");
        }

        if (!ehCelular && digitos[2] is '0' or '1')
        {
            throw new DomainException("TELEFONE_INVALIDO", "Telefone fixo não pode iniciar com 0 ou 1 após o DDD.");
        }

        return new Telefone(digitos, ehCelular);
    }

    /// <summary>Telefone com máscara, para exibição: (11) 91234-5678.</summary>
    public string Formatado => EhCelular
        ? $"({Numero[..2]}) {Numero[2..7]}-{Numero[7..]}"
        : $"({Numero[..2]}) {Numero[2..6]}-{Numero[6..]}";

    protected override IEnumerable<object?> ObterComponentesDeIgualdade()
    {
        yield return Numero;
    }

    public override string ToString() => Formatado;
}
