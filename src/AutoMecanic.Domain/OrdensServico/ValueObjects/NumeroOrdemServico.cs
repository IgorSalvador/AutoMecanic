using System.Text.RegularExpressions;
using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.OrdensServico.ValueObjects;

/// <summary>
/// Número da Ordem de Serviço no formato <c>OS-AAAA-NNNNNN</c> (ex.: <c>OS-2026-000042</c>).
/// <para>
/// É o identificador que o cliente enxerga e informa ao ligar para a oficina. Diferente do
/// <see cref="Entity.Id"/> técnico (um GUID), ele é curto, legível e reinicia a cada ano.
/// </para>
/// </summary>
public sealed partial class NumeroOrdemServico : ValueObject
{
    private const string Prefixo = "OS";
    private const int TamanhoSequencial = 6;

    private NumeroOrdemServico(int ano, int sequencial, string valor)
    {
        Ano = ano;
        Sequencial = sequencial;
        Valor = valor;
    }

    public int Ano { get; }

    public int Sequencial { get; }

    /// <summary>Representação completa, ex.: <c>OS-2026-000042</c>.</summary>
    public string Valor { get; }

    /// <summary>Gera o número a partir do ano e do próximo sequencial daquele ano.</summary>
    public static NumeroOrdemServico Gerar(int ano, int sequencial)
    {
        if (ano is < 2000 or > 2999)
        {
            throw new DomainException("NUMERO_OS_INVALIDO", $"Ano '{ano}' inválido para numeração de Ordem de Serviço.");
        }

        if (sequencial is < 1 or > 999_999)
        {
            throw new DomainException(
                "NUMERO_OS_INVALIDO",
                $"Sequencial '{sequencial}' fora da faixa permitida (1 a 999.999).");
        }

        return new NumeroOrdemServico(ano, sequencial, $"{Prefixo}-{ano}-{sequencial.ToString().PadLeft(TamanhoSequencial, '0')}");
    }

    /// <summary>Reconstrói o objeto a partir da representação textual (materialização e consultas).</summary>
    public static NumeroOrdemServico Analisar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new DomainException("NUMERO_OS_OBRIGATORIO", "O número da Ordem de Serviço é obrigatório.");
        }

        var correspondencia = FormatoNumero().Match(valor.Trim().ToUpperInvariant());

        if (!correspondencia.Success)
        {
            throw new DomainException(
                "NUMERO_OS_INVALIDO",
                $"Número '{valor}' é inválido. Formato esperado: OS-AAAA-NNNNNN.");
        }

        return Gerar(
            int.Parse(correspondencia.Groups["ano"].Value),
            int.Parse(correspondencia.Groups["sequencial"].Value));
    }

    protected override IEnumerable<object?> ObterComponentesDeIgualdade()
    {
        yield return Valor;
    }

    public override string ToString() => Valor;

    [GeneratedRegex(@"^OS-(?<ano>\d{4})-(?<sequencial>\d{6})$")]
    private static partial Regex FormatoNumero();
}
