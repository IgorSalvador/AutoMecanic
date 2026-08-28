using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Domain.Identidade;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>Gestão dos usuários administrativos do sistema.</summary>
[ApiController]
[Route("api/v1/usuarios")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Administrar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class UsuariosController(IServicoDeUsuarios servico) : ControllerBase
{
    /// <summary>Cria um usuário administrativo.</summary>
    /// <param name="requisicao">Nome, e-mail, senha inicial e perfil.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Usuário criado.</response>
    /// <response code="400">Dados inválidos ou senha fora da política.</response>
    /// <response code="409">Já existe usuário com o mesmo e-mail.</response>
    [HttpPost]
    [ProducesResponseType<UsuarioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioResponse>> Criar(
        [FromBody] CriarUsuarioRequest requisicao,
        CancellationToken cancellationToken)
    {
        var usuario = await servico.CriarAsync(requisicao, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = usuario.Id }, usuario);
    }

    /// <summary>Obtém um usuário pelo identificador.</summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Usuário encontrado.</response>
    /// <response code="404">Usuário inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<UsuarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResponse>> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(id, cancellationToken));

    /// <summary>Lista usuários com filtro e paginação.</summary>
    /// <param name="termoDeBusca">Texto livre aplicado a nome e e-mail.</param>
    /// <param name="perfil">Restringe a um perfil.</param>
    /// <param name="apenasAtivos">Filtra pela situação da conta.</param>
    /// <param name="paginacao">Página e tamanho de página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Página de usuários. O hash da senha nunca é retornado.</response>
    [HttpGet]
    [ProducesResponseType<ResultadoPaginado<UsuarioResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoPaginado<UsuarioResponse>>> Listar(
        [FromQuery] string? termoDeBusca,
        [FromQuery] PerfilUsuario? perfil,
        [FromQuery] bool? apenasAtivos,
        [FromQuery] ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarAsync(termoDeBusca, perfil, apenasAtivos, paginacao, cancellationToken));

    /// <summary>Atualiza nome e perfil de um usuário.</summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="requisicao">Novos dados.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Usuário atualizado.</response>
    /// <response code="404">Usuário inexistente.</response>
    /// <response code="409">Um administrador não pode remover o próprio perfil de administrador.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<UsuarioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarUsuarioRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AtualizarAsync(id, requisicao, cancellationToken));

    /// <summary>Redefine a senha de um usuário.</summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="requisicao">Nova senha.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Senha redefinida e bloqueios removidos.</response>
    /// <response code="400">Senha fora da política.</response>
    /// <response code="404">Usuário inexistente.</response>
    [HttpPost("{id:guid}/senha/redefinir")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RedefinirSenha(
        Guid id,
        [FromBody] RedefinirSenhaRequest requisicao,
        CancellationToken cancellationToken)
    {
        await servico.RedefinirSenhaAsync(id, requisicao, cancellationToken);

        return NoContent();
    }

    /// <summary>Desbloqueia uma conta travada por tentativas malsucedidas.</summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Conta desbloqueada.</response>
    /// <response code="404">Usuário inexistente.</response>
    [HttpPost("{id:guid}/desbloquear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desbloquear(Guid id, CancellationToken cancellationToken)
    {
        await servico.DesbloquearAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Inativa um usuário, revogando seu acesso.</summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Usuário inativado.</response>
    /// <response code="404">Usuário inexistente.</response>
    /// <response code="409">Um usuário não pode inativar a própria conta.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inativar(Guid id, CancellationToken cancellationToken)
    {
        await servico.InativarAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Reativa um usuário inativo.</summary>
    /// <param name="id">Identificador do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Usuário reativado.</response>
    /// <response code="404">Usuário inexistente.</response>
    [HttpPost("{id:guid}/reativar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await servico.ReativarAsync(id, cancellationToken);

        return NoContent();
    }
}
