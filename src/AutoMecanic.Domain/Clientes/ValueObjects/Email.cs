using System.Text.RegularExpressions;
using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Clientes.ValueObjects;

/// <summary>
/// Endereço de e-mail do cliente. É o canal pelo qual o orçamento é enviado para aprovação,
/// por isso o domínio não aceita um valor sintaticamente inválido.
/// </summary>
public sealed partial class Email : ValueObject
{
    private const int ComprimentoMaximo = 254; // RFC 5321

    private Email(string endereco) => Endereco = endereco;

    public string Endereco { get; }

    /// <exception cref="DomainException">E-mail em branco, longo demais ou com formato inválido.</exception>
    public static Email Criar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            throw new DomainException("EMAIL_OBRIGATORIO", "O e-mail é obrigatório.");
        }

        var normalizado = entrada.Trim().ToLowerInvariant();

        if (normalizado.Length > ComprimentoMaximo)
        {
            throw new DomainException("EMAIL_INVALIDO", $"O e-mail excede {ComprimentoMaximo} caracteres.");
        }

        if (!FormatoEmail().IsMatch(normalizado))
        {
            throw new DomainException("EMAIL_INVALIDO", $"E-mail '{entrada}' é inválido.");
        }

        return new Email(normalizado);
    }

    protected override IEnumerable<object?> ObterComponentesDeIgualdade()
    {
        yield return Endereco;
    }

    public override string ToString() => Endereco;

    [GeneratedRegex(@"^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}$")]
    private static partial Regex FormatoEmail();
}
