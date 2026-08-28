using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Clientes;
using AutoMecanic.Application.Clientes.Dtos;
using AutoMecanic.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>Gestão administrativa de clientes (CRUD).</summary>
[ApiController]
[Route("api/v1/clientes")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Consultar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ClientesController(IServicoDeClientes servico) : ControllerBase
{
    /// <summary>Cadastra um novo cliente.</summary>
    /// <param name="requisicao">Dados do cliente. O CPF/CNPJ é validado por dígito verificador.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Cliente cadastrado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="409">Já existe cliente com o mesmo CPF/CNPJ.</response>
    /// <response code="422">Regra de negócio violada.</response>
    [HttpPost]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<ClienteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClienteResponse>> Cadastrar(
        [FromBody] CriarClienteRequest requisicao,
        CancellationToken cancellationToken)
    {
        var cliente = await servico.CadastrarAsync(requisicao, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = cliente.Id }, cliente);
    }

    /// <summary>Obtém um cliente pelo identificador.</summary>
    /// <param name="id">Identificador do cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Cliente encontrado.</response>
    /// <response code="404">Cliente inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClienteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteResponse>> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(id, cancellationToken));

    /// <summary>Obtém um cliente pelo CPF ou CNPJ.</summary>
    /// <param name="documento">CPF ou CNPJ, com ou sem máscara.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Cliente encontrado.</response>
    /// <response code="400">Documento inválido.</response>
    /// <response code="404">Cliente inexistente.</response>
    /// <remarks>É por este caminho que a recepção identifica o cliente na chegada do veículo.</remarks>
    [HttpGet("documento/{documento}")]
    [ProducesResponseType<ClienteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteResponse>> ObterPorDocumento(
        string documento,
        CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorDocumentoAsync(documento, cancellationToken));

    /// <summary>Lista clientes com filtro e paginação.</summary>
    /// <param name="termoDeBusca">Texto livre aplicado a nome, documento e e-mail.</param>
    /// <param name="apenasAtivos">Quando informado, filtra pela situação do cadastro.</param>
    /// <param name="paginacao">Página e tamanho de página (máximo 100).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Página de clientes.</response>
    [HttpGet]
    [ProducesResponseType<ResultadoPaginado<ClienteResumoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoPaginado<ClienteResumoResponse>>> Listar(
        [FromQuery] string? termoDeBusca,
        [FromQuery] bool? apenasAtivos,
        [FromQuery] ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarAsync(termoDeBusca, apenasAtivos, paginacao, cancellationToken));

    /// <summary>Atualiza os dados cadastrais de um cliente.</summary>
    /// <param name="id">Identificador do cliente.</param>
    /// <param name="requisicao">Novos dados. O CPF/CNPJ não pode ser alterado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Cliente atualizado.</response>
    /// <response code="404">Cliente inexistente.</response>
    /// <response code="422">Cliente inativo ou dados que violam regra de negócio.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<ClienteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ClienteResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarClienteRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AtualizarAsync(id, requisicao, cancellationToken));

    /// <summary>Inativa um cliente.</summary>
    /// <param name="id">Identificador do cliente.</param>
    /// <param name="motivo">Justificativa da inativação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Cliente inativado.</response>
    /// <response code="404">Cliente inexistente.</response>
    /// <remarks>
    /// O cadastro é inativado, nunca excluído: as Ordens de Serviço já emitidas precisam
    /// continuar referenciando um cliente existente.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Inativar(
        Guid id,
        [FromQuery] string? motivo,
        CancellationToken cancellationToken)
    {
        await servico.InativarAsync(id, motivo, cancellationToken);

        return NoContent();
    }

    /// <summary>Reativa um cliente inativo.</summary>
    /// <param name="id">Identificador do cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Cliente reativado.</response>
    /// <response code="404">Cliente inexistente.</response>
    [HttpPost("{id:guid}/reativar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await servico.ReativarAsync(id, cancellationToken);

        return NoContent();
    }
}
