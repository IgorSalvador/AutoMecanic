using System.Security.Claims;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.Identidade;

namespace AutoMecanic.Api.Servicos;

/// <summary>
/// Lê a identidade do usuário a partir das reivindicações do JWT da requisição corrente.
/// <para>
/// É a ponte que permite à camada de aplicação registrar autoria — quem iniciou o
/// diagnóstico, quem finalizou o serviço — sem conhecer <c>HttpContext</c> nem qualquer
/// detalhe de ASP.NET Core.
/// </para>
/// </summary>
public sealed class UsuarioAtual(IHttpContextAccessor acessor) : IUsuarioAtual
{
    private ClaimsPrincipal? Principal => acessor.HttpContext?.User;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public PerfilUsuario? Perfil =>
        Enum.TryParse<PerfilUsuario>(Principal?.FindFirstValue(ClaimTypes.Role), out var perfil) ? perfil : null;

    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated ?? false;
}
