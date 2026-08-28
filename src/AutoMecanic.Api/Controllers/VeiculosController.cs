using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Veiculos;
using AutoMecanic.Application.Veiculos.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>Gestão administrativa de veículos (CRUD).</summary>
[ApiController]
[Route("api/v1/veiculos")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Consultar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class VeiculosController(IServicoDeVeiculos servico) : ControllerBase
{
    /// <summary>Cadastra um veículo para um cliente.</summary>
    /// <param name="requisicao">Dados do veículo. A placa é validada nos padrões brasileiro e Mercosul.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Veículo cadastrado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="404">Cliente inexistente.</response>
    /// <response code="409">Já existe veículo com a mesma placa.</response>
    [HttpPost]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<VeiculoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VeiculoResponse>> Cadastrar(
        [FromBody] CriarVeiculoRequest requisicao,
        CancellationToken cancellationToken)
    {
        var veiculo = await servico.CadastrarAsync(requisicao, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = veiculo.Id }, veiculo);
    }

    /// <summary>Obtém um veículo pelo identificador.</summary>
    /// <param name="id">Identificador do veículo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Veículo encontrado.</response>
    /// <response code="404">Veículo inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<VeiculoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VeiculoResponse>> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(id, cancellationToken));

    /// <summary>Obtém um veículo pela placa.</summary>
    /// <param name="placa">Placa no padrão ABC1234 ou ABC1D23, com ou sem hífen.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Veículo encontrado.</response>
    /// <response code="400">Placa inválida.</response>
    /// <response code="404">Veículo inexistente.</response>
    [HttpGet("placa/{placa}")]
    [ProducesResponseType<VeiculoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VeiculoResponse>> ObterPorPlaca(string placa, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorPlacaAsync(placa, cancellationToken));

    /// <summary>Lista os veículos de um cliente.</summary>
    /// <param name="clienteId">Identificador do cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Veículos do cliente.</response>
    [HttpGet("cliente/{clienteId:guid}")]
    [ProducesResponseType<IReadOnlyList<VeiculoResumoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VeiculoResumoResponse>>> ListarPorCliente(
        Guid clienteId,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarPorClienteAsync(clienteId, cancellationToken));

    /// <summary>Lista veículos com filtro e paginação.</summary>
    /// <param name="termoDeBusca">Texto livre aplicado a placa, marca e modelo.</param>
    /// <param name="clienteId">Restringe a um proprietário.</param>
    /// <param name="apenasAtivos">Filtra pela situação do cadastro.</param>
    /// <param name="paginacao">Página e tamanho de página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Página de veículos.</response>
    [HttpGet]
    [ProducesResponseType<ResultadoPaginado<VeiculoResumoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoPaginado<VeiculoResumoResponse>>> Listar(
        [FromQuery] string? termoDeBusca,
        [FromQuery] Guid? clienteId,
        [FromQuery] bool? apenasAtivos,
        [FromQuery] ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarAsync(termoDeBusca, clienteId, apenasAtivos, paginacao, cancellationToken));

    /// <summary>Atualiza os dados descritivos do veículo.</summary>
    /// <param name="id">Identificador do veículo.</param>
    /// <param name="requisicao">Novos dados. A placa não pode ser alterada.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Veículo atualizado.</response>
    /// <response code="404">Veículo inexistente.</response>
    /// <response code="422">Veículo inativo ou dados que violam regra de negócio.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<VeiculoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VeiculoResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarVeiculoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AtualizarAsync(id, requisicao, cancellationToken));

    /// <summary>Registra nova leitura do odômetro.</summary>
    /// <param name="id">Identificador do veículo.</param>
    /// <param name="requisicao">Quilometragem atual.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Quilometragem registrada.</response>
    /// <response code="404">Veículo inexistente.</response>
    /// <response code="422">A quilometragem informada é menor que a última registrada.</response>
    [HttpPatch("{id:guid}/quilometragem")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<VeiculoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VeiculoResponse>> RegistrarQuilometragem(
        Guid id,
        [FromBody] RegistrarQuilometragemRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.RegistrarQuilometragemAsync(id, requisicao, cancellationToken));

    /// <summary>Transfere o veículo para outro cliente.</summary>
    /// <param name="id">Identificador do veículo.</param>
    /// <param name="requisicao">Novo proprietário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Veículo transferido.</response>
    /// <response code="404">Veículo ou novo cliente inexistente.</response>
    [HttpPost("{id:guid}/transferir")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<VeiculoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VeiculoResponse>> Transferir(
        Guid id,
        [FromBody] TransferirVeiculoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.TransferirAsync(id, requisicao, cancellationToken));

    /// <summary>Inativa um veículo.</summary>
    /// <param name="id">Identificador do veículo.</param>
    /// <param name="motivo">Justificativa da inativação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Veículo inativado.</response>
    /// <response code="404">Veículo inexistente.</response>
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

    /// <summary>Reativa um veículo inativo.</summary>
    /// <param name="id">Identificador do veículo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Veículo reativado.</response>
    /// <response code="404">Veículo inexistente.</response>
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
