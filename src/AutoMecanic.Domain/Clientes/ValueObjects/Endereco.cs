using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Clientes.ValueObjects;

/// <summary>
/// Endereço do cliente. Opcional no cadastro — a oficina consegue abrir uma OS sem ele —
/// mas, quando informado, precisa estar completo o bastante para uso em nota fiscal.
/// </summary>
public sealed class Endereco : ValueObject
{
    private static readonly HashSet<string> UnidadesFederativas =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG",
        "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    private Endereco(string logradouro, string numero, string? complemento, string bairro, string cidade, string uf, string cep)
    {
        Logradouro = logradouro;
        Numero = numero;
        Complemento = complemento;
        Bairro = bairro;
        Cidade = cidade;
        Uf = uf;
        Cep = cep;
    }

    public string Logradouro { get; }

    public string Numero { get; }

    public string? Complemento { get; }

    public string Bairro { get; }

    public string Cidade { get; }

    /// <summary>Sigla da unidade federativa, em caixa alta.</summary>
    public string Uf { get; }

    /// <summary>CEP somente com dígitos (8 posições).</summary>
    public string Cep { get; }

    public static Endereco Criar(
        string? logradouro,
        string? numero,
        string? complemento,
        string? bairro,
        string? cidade,
        string? uf,
        string? cep)
    {
        var logradouroLimpo = ExigirTexto(logradouro, "logradouro", 200);
        var numeroLimpo = ExigirTexto(numero, "número", 20);
        var bairroLimpo = ExigirTexto(bairro, "bairro", 100);
        var cidadeLimpa = ExigirTexto(cidade, "cidade", 100);

        var ufNormalizada = (uf ?? string.Empty).Trim().ToUpperInvariant();

        if (!UnidadesFederativas.Contains(ufNormalizada))
        {
            throw new DomainException("ENDERECO_INVALIDO", $"UF '{uf}' é inválida.");
        }

        var cepDigitos = new string([.. (cep ?? string.Empty).Where(char.IsDigit)]);

        if (cepDigitos.Length != 8)
        {
            throw new DomainException("ENDERECO_INVALIDO", $"CEP '{cep}' é inválido: informe 8 dígitos.");
        }

        var complementoLimpo = string.IsNullOrWhiteSpace(complemento) ? null : complemento.Trim();

        return new Endereco(logradouroLimpo, numeroLimpo, complementoLimpo, bairroLimpo, cidadeLimpa, ufNormalizada, cepDigitos);
    }

    private static string ExigirTexto(string? valor, string campo, int comprimentoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException("ENDERECO_INVALIDO", $"O campo '{campo}' do endereço é obrigatório.");
        }

        var limpo = valor.Trim();

        if (limpo.Length > comprimentoMaximo)
        {
            throw new DomainException("ENDERECO_INVALIDO", $"O campo '{campo}' excede {comprimentoMaximo} caracteres.");
        }

        return limpo;
    }

    public string CepFormatado => $"{Cep[..5]}-{Cep[5..]}";

    protected override IEnumerable<object?> ObterComponentesDeIgualdade()
    {
        yield return Logradouro;
        yield return Numero;
        yield return Complemento;
        yield return Bairro;
        yield return Cidade;
        yield return Uf;
        yield return Cep;
    }

    public override string ToString() =>
        $"{Logradouro}, {Numero}{(Complemento is null ? string.Empty : $" - {Complemento}")} - {Bairro}, {Cidade}/{Uf} - {CepFormatado}";
}
