namespace AutoMecanic.Domain.Abstractions;

/// <summary>
/// Fonte de tempo do domínio. Todo instante registrado por uma entidade (abertura da OS,
/// início e fim da execução, envio do orçamento) vem daqui, e nunca de
/// <c>DateTimeOffset.UtcNow</c> — o uso direto do relógio do sistema é proibido no código de
/// produção por analisador (<c>src/BannedSymbols.txt</c>).
/// <para>
/// Usa o <see cref="TimeProvider"/> da biblioteca-base, que não acrescenta dependência ao
/// domínio. A aplicação configura o provedor na composição, compartilhando com a camada de
/// aplicação a mesma fonte de tempo; os testes congelam o relógio com
/// <see cref="Congelar"/> para verificar durações e validades de forma determinística.
/// </para>
/// <para>
/// A alternativa — receber <c>agora</c> como parâmetro em cada comportamento — já é usada onde
/// o instante é a própria regra (bloqueio de usuário, expiração de orçamento). Estendê-la a
/// todo carimbo de data espalharia o parâmetro por dezenas de assinaturas sem ganho de
/// clareza.
/// </para>
/// </summary>
public static class RelogioDoDominio
{
    private static readonly AsyncLocal<TimeProvider?> Congelado = new();

    private static TimeProvider _provedor = TimeProvider.System;

    /// <summary>Instante atual, em UTC.</summary>
    public static DateTimeOffset Agora => (Congelado.Value ?? _provedor).GetUtcNow();

    /// <summary>
    /// Define o provedor de tempo usado pelo domínio. Chamado uma vez, na composição da
    /// aplicação, com o mesmo provedor registrado no contêiner de injeção de dependência.
    /// </summary>
    public static void Configurar(TimeProvider provedor)
    {
        ArgumentNullException.ThrowIfNull(provedor);

        _provedor = provedor;
    }

    /// <summary>
    /// Substitui o relógio apenas no fluxo assíncrono corrente — o que mantém isolados os
    /// testes executados em paralelo — até que o objeto devolvido seja descartado.
    /// </summary>
    public static IDisposable Congelar(TimeProvider provedor)
    {
        ArgumentNullException.ThrowIfNull(provedor);

        var anterior = Congelado.Value;
        Congelado.Value = provedor;

        return new Restauracao(anterior);
    }

    private sealed class Restauracao(TimeProvider? anterior) : IDisposable
    {
        public void Dispose() => Congelado.Value = anterior;
    }
}
