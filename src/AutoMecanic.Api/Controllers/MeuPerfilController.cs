using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Identidade.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>
/// Autoatendimento do usuário autenticado: consultar o próprio perfil e trocar a própria senha.
/// <para>
/// Estas operações vivem em um controlador separado de <see cref="UsuariosController"/> por
/// um motivo concreto: o ASP.NET Core <b>combina</b> os atributos <c>[Authorize]</c> da classe
/// e da ação, exigindo que <b>ambas</b> as políticas passem. Um atendente barrado pela política
/// de administração no nível da classe jamais alcançaria a ação, por mais permissiva que
/// fosse a política declarada nela — e ficaria sem conseguir trocar a própria senha.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/usuarios/eu")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Consultar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class MeuPerfilController(IServicoDeUsuarios servico, IUsuarioAtual usuarioAtual) : ControllerBase
{
    /// <summary>Dados do usuário autenticado na requisição corrente.</summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Perfil do usuário autenticado.</response>
    /// <response code="401">Requisição sem token válido.</response>
    /// <remarks>Disponível a qualquer usuário autenticado, independentemente do perfil.</remarks>
    [HttpGet]
    [ProducesResponseType<UsuarioResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UsuarioResponse>> ObterMeuPerfil(CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(ExigirIdentidade(), cancellationToken));

    /// <summary>Troca a própria senha.</summary>
    /// <param name="requisicao">Senha atual e nova senha.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Senha alterada.</response>
    /// <response code="400">Nova senha fora da política.</response>
    /// <response code="422">Senha atual incorreta ou nova senha igual à anterior.</response>
    /// <remarks>
    /// Exige a senha atual como prova de identidade: sem isso, um token roubado permitiria
    /// tomar a conta permanentemente.
    /// </remarks>
    [HttpPost("senha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AlterarMinhaSenha(
        [FromBody] AlterarSenhaRequest requisicao,
        CancellationToken cancellationToken)
    {
        await servico.AlterarSenhaAsync(ExigirIdentidade(), requisicao, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Extrai o identificador do token. Um token válido sem <c>sub</c> indica emissor
    /// adulterado — tratado como não autorizado, nunca como erro do servidor.
    /// </summary>
    private Guid ExigirIdentidade() =>
        usuarioAtual.Id ?? throw new NaoAutorizadoException("Token sem identificação de usuário.");
}
