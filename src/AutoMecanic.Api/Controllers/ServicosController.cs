using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Servicos;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Domain.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>Catálogo de serviços prestados pela oficina (CRUD).</summary>
[ApiController]
[Route("api/v1/servicos")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Consultar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ServicosController(IServicoDeCatalogo servico) : ControllerBase
{
    /// <summary>Inclui um serviço no catálogo.</summary>
    /// <param name="requisicao">Nome, categoria, preço de tabela e tempo estimado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Serviço cadastrado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="409">Já existe serviço com o mesmo nome.</response>
    [HttpPost]
    [Authorize(Policy = PoliticasDeAutorizacao.Administrar)]
    [ProducesResponseType<ServicoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServicoResponse>> Cadastrar(
        [FromBody] CriarServicoRequest requisicao,
        CancellationToken cancellationToken)
    {
        var criado = await servico.CadastrarAsync(requisicao, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Obtém um serviço do catálogo.</summary>
    /// <param name="id">Identificador do serviço.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Serviço encontrado.</response>
    /// <response code="404">Serviço inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServicoResponse>> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(id, cancellationToken));

    /// <summary>Lista serviços com filtro e paginação.</summary>
    /// <param name="termoDeBusca">Texto livre aplicado a nome e descrição.</param>
    /// <param name="categoria">Restringe a uma categoria.</param>
    /// <param name="apenasAtivos">Filtra pela situação do serviço.</param>
    /// <param name="paginacao">Página e tamanho de página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Página de serviços.</response>
    [HttpGet]
    [ProducesResponseType<ResultadoPaginado<ServicoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoPaginado<ServicoResponse>>> Listar(
        [FromQuery] string? termoDeBusca,
        [FromQuery] CategoriaServico? categoria,
        [FromQuery] bool? apenasAtivos,
        [FromQuery] ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarAsync(termoDeBusca, categoria, apenasAtivos, paginacao, cancellationToken));

    /// <summary>Atualiza um serviço do catálogo.</summary>
    /// <param name="id">Identificador do serviço.</param>
    /// <param name="requisicao">Novos dados. O preço tem endpoint próprio.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Serviço atualizado.</response>
    /// <response code="404">Serviço inexistente.</response>
    /// <response code="409">Já existe outro serviço com o mesmo nome.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.Administrar)]
    [ProducesResponseType<ServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ServicoResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarServicoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AtualizarAsync(id, requisicao, cancellationToken));

    /// <summary>Reajusta o preço de tabela.</summary>
    /// <param name="id">Identificador do serviço.</param>
    /// <param name="requisicao">Novo preço.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Preço reajustado.</response>
    /// <response code="404">Serviço inexistente.</response>
    /// <remarks>
    /// O reajuste vale apenas para novas Ordens de Serviço: os itens já incluídos guardam
    /// uma cópia do preço vigente na data da inclusão.
    /// </remarks>
    [HttpPatch("{id:guid}/preco")]
    [Authorize(Policy = PoliticasDeAutorizacao.Administrar)]
    [ProducesResponseType<ServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServicoResponse>> ReajustarPreco(
        Guid id,
        [FromBody] ReajustarPrecoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ReajustarPrecoAsync(id, requisicao, cancellationToken));

    /// <summary>Inativa um serviço, retirando-o do catálogo.</summary>
    /// <param name="id">Identificador do serviço.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Serviço inativado.</response>
    /// <response code="404">Serviço inexistente.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.Administrar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Inativar(Guid id, CancellationToken cancellationToken)
    {
        await servico.InativarAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Reativa um serviço inativo.</summary>
    /// <param name="id">Identificador do serviço.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Serviço reativado.</response>
    /// <response code="404">Serviço inexistente.</response>
    [HttpPost("{id:guid}/reativar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Administrar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await servico.ReativarAsync(id, cancellationToken);

        return NoContent();
    }
}
