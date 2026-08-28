using System.Text.RegularExpressions;
using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Clientes.ValueObjects;

/// <summary>Natureza jurídica do cliente, derivada do documento informado.</summary>
public enum TipoPessoa
{
    /// <summary>Pessoa Física — identificada por CPF.</summary>
    Fisica = 1,

    /// <summary>Pessoa Jurídica — identificada por CNPJ.</summary>
    Juridica = 2
}

/// <summary>
/// Documento de identificação do cliente (CPF ou CNPJ). É a chave natural de identificação
/// exigida no fluxo de abertura da Ordem de Serviço.
/// <para>
/// A validação vai além do formato: os dígitos verificadores são recalculados. O CNPJ aceita
/// o formato alfanumérico (12 posições alfanuméricas + 2 dígitos verificadores) adotado pela
/// Receita Federal, cujo algoritmo é retrocompatível com o CNPJ puramente numérico.
/// </para>
/// </summary>
public sealed partial class Documento : ValueObject
{
    private static readonly int[] PesosCpfPrimeiroDigito = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCpfSegundoDigito = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpjPrimeiroDigito = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpjSegundoDigito = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private Documento(string numero, TipoPessoa tipo)
    {
        Numero = numero;
        Tipo = tipo;
    }

    /// <summary>Documento normalizado, somente com caracteres significativos e em caixa alta.</summary>
    public string Numero { get; }

    public TipoPessoa Tipo { get; }

    public bool EhCpf => Tipo == TipoPessoa.Fisica;

    public bool EhCnpj => Tipo == TipoPessoa.Juridica;

    /// <summary>
    /// Cria o documento a partir de uma entrada crua (com ou sem máscara), decidindo entre
    /// CPF e CNPJ pelo comprimento após a normalização.
    /// </summary>
    /// <exception cref="DomainException">Documento em branco, com tamanho inválido ou com dígito verificador incorreto.</exception>
    public static Documento Criar(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
        {
            throw new DomainException("DOCUMENTO_OBRIGATORIO", "CPF ou CNPJ é obrigatório.");
        }

        var normalizado = Normalizar(entrada);

        return normalizado.Length switch
        {
            11 => CriarCpf(normalizado),
            14 => CriarCnpj(normalizado),
            _ => throw new DomainException(
                "DOCUMENTO_INVALIDO",
                $"Documento '{entrada}' é inválido: deve ter 11 dígitos (CPF) ou 14 caracteres (CNPJ).")
        };
    }

    /// <summary>Tentativa não-excepcional de criação, útil em filtros de consulta.</summary>
    public static bool TentarCriar(string? entrada, out Documento? documento)
    {
        try
        {
            documento = Criar(entrada);
            return true;
        }
        catch (DomainException)
        {
            documento = null;
            return false;
        }
    }

    private static Documento CriarCpf(string numero)
    {
        if (!CaracteresNumericos().IsMatch(numero))
        {
            throw new DomainException("DOCUMENTO_INVALIDO", "CPF deve conter apenas dígitos.");
        }

        // Sequências repetidas (00000000000, 11111111111, ...) passam no cálculo do dígito
        // verificador, mas não são CPFs válidos. Precisam de rejeição explícita.
        if (numero.All(c => c == numero[0]))
        {
            throw new DomainException("DOCUMENTO_INVALIDO", $"CPF '{Formatar(numero, TipoPessoa.Fisica)}' é inválido.");
        }

        var digito1 = CalcularDigitoModulo11(numero[..9], PesosCpfPrimeiroDigito);
        var digito2 = CalcularDigitoModulo11(numero[..10], PesosCpfSegundoDigito);

        if (numero[9] != digito1 || numero[10] != digito2)
        {
            throw new DomainException("DOCUMENTO_INVALIDO", $"CPF '{Formatar(numero, TipoPessoa.Fisica)}' é inválido.");
        }

        return new Documento(numero, TipoPessoa.Fisica);
    }

    private static Documento CriarCnpj(string numero)
    {
        // Formato vigente: 12 posições alfanuméricas seguidas de 2 dígitos verificadores numéricos.
        if (!RaizCnpjAlfanumerica().IsMatch(numero))
        {
            throw new DomainException(
                "DOCUMENTO_INVALIDO",
                "CNPJ deve ter 12 caracteres alfanuméricos seguidos de 2 dígitos verificadores.");
        }

        if (numero.All(c => c == numero[0]))
        {
            throw new DomainException("DOCUMENTO_INVALIDO", $"CNPJ '{Formatar(numero, TipoPessoa.Juridica)}' é inválido.");
        }

        var digito1 = CalcularDigitoModulo11(numero[..12], PesosCnpjPrimeiroDigito);
        var digito2 = CalcularDigitoModulo11(numero[..13], PesosCnpjSegundoDigito);

        if (numero[12] != digito1 || numero[13] != digito2)
        {
            throw new DomainException("DOCUMENTO_INVALIDO", $"CNPJ '{Formatar(numero, TipoPessoa.Juridica)}' é inválido.");
        }

        return new Documento(numero, TipoPessoa.Juridica);
    }

    /// <summary>
    /// Módulo 11 comum a CPF e CNPJ. O valor de cada posição é <c>ASCII - 48</c>, o que faz
    /// dígitos '0'-'9' valerem 0-9 e letras 'A'-'Z' valerem 17-42, conforme a especificação
    /// do CNPJ alfanumérico — e mantém o cálculo idêntico para documentos numéricos.
    /// </summary>
    private static char CalcularDigitoModulo11(string baseCalculo, int[] pesos)
    {
        var soma = 0;

        for (var i = 0; i < baseCalculo.Length; i++)
        {
            soma += (baseCalculo[i] - '0') * pesos[i];
        }

        var resto = soma % 11;

        return (char)('0' + (resto < 2 ? 0 : 11 - resto));
    }

    private static string Normalizar(string entrada) =>
        new([.. entrada.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant)]);

    /// <summary>Documento com máscara, para exibição (000.000.000-00 ou 00.000.000/0000-00).</summary>
    public string Formatado => Formatar(Numero, Tipo);

    private static string Formatar(string numero, TipoPessoa tipo) => tipo switch
    {
        TipoPessoa.Fisica when numero.Length == 11 =>
            $"{numero[..3]}.{numero[3..6]}.{numero[6..9]}-{numero[9..]}",
        TipoPessoa.Juridica when numero.Length == 14 =>
            $"{numero[..2]}.{numero[2..5]}.{numero[5..8]}/{numero[8..12]}-{numero[12..]}",
        _ => numero
    };

    protected override IEnumerable<object?> ObterComponentesDeIgualdade()
    {
        yield return Numero;
    }

    public override string ToString() => Formatado;

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex CaracteresNumericos();

    [GeneratedRegex(@"^[A-Z0-9]{12}\d{2}$")]
    private static partial Regex RaizCnpjAlfanumerica();
}
