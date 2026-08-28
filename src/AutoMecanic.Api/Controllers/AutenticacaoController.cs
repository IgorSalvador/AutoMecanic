using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Identidade.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoMecanic.Api.Controllers;

/// <summary>Emissão de tokens de acesso às APIs administrativas.</summary>
[ApiController]
[Route("api/v1/autenticacao")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class AutenticacaoController(IServicoDeAutenticacao servico) : ControllerBase
{
    /// <summary>Autentica um usuário e devolve o token JWT.</summary>
    /// <param name="requisicao">E-mail e senha cadastrados.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Token JWT, prazo de expiração e dados do usuário autenticado.</returns>
    /// <response code="200">Autenticação bem-sucedida.</response>
    /// <response code="400">Requisição malformada.</response>
    /// <response code="401">Credenciais inválidas, conta inativa ou bloqueada.</response>
    /// <response code="429">Excesso de tentativas a partir do mesmo endereço de origem.</response>
    /// <remarks>
    /// Após 5 tentativas malsucedidas consecutivas, a conta é bloqueada por 15 minutos.
    /// A resposta é a mesma para e-mail inexistente e senha incorreta, de propósito: distingui-las
    /// permitiria descobrir quais e-mails estão cadastrados no sistema.
    /// </remarks>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AutenticarAsync(requisicao, cancellationToken));
}
