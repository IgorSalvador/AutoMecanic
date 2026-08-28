namespace AutoMecanic.Api;

/// <summary>
/// Sonda de saúde executada <b>de dentro do próprio contêiner</b> pelo <c>HEALTHCHECK</c>.
/// <para>
/// Existe para que a imagem final possa ser <i>chiseled</i> — sem shell, sem gerenciador de
/// pacotes e sem cliente HTTP. A alternativa seria instalar <c>curl</c> apenas para sondar a
/// aplicação, o que traria de volta dezenas de pacotes do sistema operacional e, com eles,
/// as vulnerabilidades que a imagem mínima justamente elimina.
/// </para>
/// <para>
/// Uso: <c>dotnet AutoMecanic.Api.dll --health-check</c>. Sai com 0 quando a aplicação
/// responde saudável e 1 em qualquer outro caso.
/// </para>
/// </summary>
internal static class VerificacaoDeSaudeDoContainer
{
    /// <summary>Argumento que ativa o modo de sonda em vez de iniciar o servidor.</summary>
    public const string Argumento = "--health-check";

    private static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(4);

    /// <summary>Indica se o processo foi iniciado apenas para verificar a saúde.</summary>
    public static bool FoiSolicitada(string[] args) =>
        args.Contains(Argumento, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Consulta o endpoint de prontidão na porta local e devolve o código de saída do processo.
    /// </summary>
    public static async Task<int> ExecutarAsync()
    {
        var porta = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080";

        using var http = new HttpClient { Timeout = TempoLimite };

        try
        {
            var resposta = await http.GetAsync($"http://localhost:{porta}/health/pronto");

            return resposta.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception excecao) when (excecao is HttpRequestException or TaskCanceledException)
        {
            // Aplicação ainda subindo, porta fechada ou banco inacessível: para o
            // orquestrador, todos significam a mesma coisa — ainda não está pronta.
            return 1;
        }
    }
}
