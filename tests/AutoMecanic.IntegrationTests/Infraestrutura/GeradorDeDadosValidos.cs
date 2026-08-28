namespace AutoMecanic.IntegrationTests.Infraestrutura;

/// <summary>
/// Gera CPFs e placas <b>sintaticamente válidos</b> e distintos a cada chamada.
/// <para>
/// Os testes de integração compartilham um único banco, então cada um precisa de chaves
/// naturais próprias para não colidir com os demais. Como o domínio recusa documento com
/// dígito verificador incorreto, não basta um número aleatório: ele precisa ser calculado.
/// </para>
/// </summary>
internal static class GeradorDeDadosValidos
{
    private static int _contador;

    /// <summary>Devolve um CPF válido e inédito nesta execução.</summary>
    public static string ProximoCpf()
    {
        // Base derivada de um contador: garante unicidade sem depender de sorte.
        var sequencial = Interlocked.Increment(ref _contador);
        var baseCpf = (100_000_000 + (sequencial * 7_919)).ToString()[..9];

        var primeiro = DigitoVerificador(baseCpf, 10);
        var segundo = DigitoVerificador(baseCpf + primeiro, 11);

        return baseCpf + primeiro + segundo;
    }

    /// <summary>Devolve uma placa no padrão Mercosul, inédita nesta execução.</summary>
    public static string ProximaPlaca()
    {
        var sequencial = Interlocked.Increment(ref _contador);
        const string letras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        var primeira = letras[sequencial % 26];
        var segunda = letras[(sequencial / 26) % 26];
        var terceira = letras[(sequencial / 676) % 26];
        var quarta = letras[(sequencial / 7) % 26];

        return $"{primeira}{segunda}{terceira}{sequencial % 10}{quarta}{sequencial % 100:D2}";
    }

    /// <summary>Devolve um código de peça inédito nesta execução.</summary>
    public static string ProximoCodigoDePeca() =>
        $"TESTE-{Interlocked.Increment(ref _contador):D6}";

    /// <summary>Devolve um e-mail inédito nesta execução.</summary>
    public static string ProximoEmail() =>
        $"teste{Interlocked.Increment(ref _contador)}@automecanic.com.br";

    private static char DigitoVerificador(string baseCalculo, int pesoInicial)
    {
        var soma = 0;
        var peso = pesoInicial;

        foreach (var caractere in baseCalculo)
        {
            soma += (caractere - '0') * peso--;
        }

        var resto = soma % 11;

        return (char)('0' + (resto < 2 ? 0 : 11 - resto));
    }
}
