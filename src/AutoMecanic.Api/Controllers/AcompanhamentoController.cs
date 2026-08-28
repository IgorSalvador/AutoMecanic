using AutoMecanic.Application.OrdensServico;
using AutoMecanic.Application.OrdensServico.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>
/// Consulta pública de acompanhamento da Ordem de Serviço, usada pelo cliente.
/// <para>
/// Atende ao requisito "permitir consulta por parte do cliente via API para acompanhar o
/// progresso" sem exigir que a oficina crie login para cada cliente.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/acompanhamento")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class AcompanhamentoController(IServicoDeOrdensServico servico) : ControllerBase
{
    /// <summary>Consulta a situação de uma Ordem de Serviço.</summary>
    /// <param name="numero">Número da OS no formato OS-AAAA-NNNNNN, informado no comprovante.</param>
    /// <param name="documento">CPF ou CNPJ do cliente titular da OS.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Situação atual, serviços contratados, valor do orçamento e linha do tempo.</returns>
    /// <response code="200">Ordem de Serviço localizada.</response>
    /// <response code="400">Número ou documento em formato inválido.</response>
    /// <response code="404">Nenhuma OS corresponde ao número e documento informados.</response>
    /// <remarks>
    /// <para>
    /// O endpoint é anônimo, mas exige <b>número da OS e documento do cliente juntos</b>: os dois
    /// funcionam como prova de posse. Sem essa combinação, seria possível percorrer números
    /// sequenciais de OS e ler dados de outros clientes.
    /// </para>
    /// <para>
    /// A resposta é idêntica para "OS inexistente" e "documento não confere" — distingui-las
    /// permitiria descobrir quais números de OS existem.
    /// </para>
    /// <para>
    /// A visão é reduzida de propósito: não expõe responsável técnico, custo individual de peça
    /// nem identificadores internos. O orçamento só aparece depois de enviado ao cliente.
    /// </para>
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<AcompanhamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AcompanhamentoResponse>> Acompanhar(
        [FromQuery] string numero,
        [FromQuery] string documento,
        CancellationToken cancellationToken) =>
        Ok(await servico.AcompanharAsync(numero, documento, cancellationToken));
}
