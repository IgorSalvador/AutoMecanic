namespace AutoMecanic.Api.Middlewares;

/// <summary>
/// Aplica cabeçalhos de segurança em todas as respostas e remove cabeçalhos que revelam a
/// tecnologia do servidor.
/// <para>
/// Para uma API JSON, a maior parte destes cabeçalhos é defesa em profundidade: eles importam
/// principalmente quando uma resposta é aberta diretamente no navegador — como acontece com a
/// interface do Swagger — ou quando um erro devolve conteúdo interpretável.
/// </para>
/// </summary>
public sealed class MiddlewareDeCabecalhosDeSeguranca(RequestDelegate proximo)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        contexto.Response.OnStarting(() =>
        {
            var cabecalhos = contexto.Response.Headers;

            // Impede que o navegador adivinhe o tipo do conteúdo e execute como script
            // algo que a API declarou como JSON.
            cabecalhos["X-Content-Type-Options"] = "nosniff";

            // Bloqueia a exibição da resposta dentro de um iframe de terceiros (clickjacking).
            cabecalhos["X-Frame-Options"] = "DENY";

            // Não vaza a URL completa da API (que pode conter identificadores) ao navegar
            // para outro site.
            cabecalhos["Referrer-Policy"] = "no-referrer";

            // Desliga APIs sensíveis do navegador que esta aplicação nunca usa.
            cabecalhos["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";

            // Remove a impressão digital do servidor: saber a versão exata facilita a
            // seleção de exploits conhecidos.
            cabecalhos.Remove("Server");
            cabecalhos.Remove("X-Powered-By");
            cabecalhos.Remove("X-AspNet-Version");

            return Task.CompletedTask;
        });

        await proximo(contexto);
    }
}
