using AutoMecanic.Application.OrdensServico.CasosDeUso;
using AutoMecanic.Application.OrdensServico.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutoMecanic.Api.Controllers;

/// <summary>
/// Autoatendimento do cliente, sem autenticação: acompanhar a Ordem de Serviço e responder ao
/// orçamento recebido.
/// <para>
/// Atende aos requisitos de consulta do cliente via API e de aprovação ou recusa do orçamento
/// por um canal externo, sem exigir que a oficina crie login para cada cliente. Toda operação
/// exige o número da OS e o documento do titular juntos.
/// </para>
/// <para>
/// As rotas têm um limite de taxa próprio, mais apertado que o global: como o documento é a
/// prova de posse, o limite é o que impede tentar documentos em sequência contra um número.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/acompanhamento")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting(PoliticaDeTaxaDoCliente)]
public sealed class AcompanhamentoController : ControllerBase
{
    /// <summary>Política de limite de taxa das rotas de autoatendimento.</summary>
    public const string PoliticaDeTaxaDoCliente = "cliente";

    /// <summary>Consulta a situação de uma Ordem de Serviço.</summary>
    /// <param name="numero">Número da OS no formato OS-AAAA-NNNNNN, informado no comprovante.</param>
    /// <param name="documento">CPF ou CNPJ do cliente titular da OS.</param>
    /// <param name="casoDeUso">Caso de uso executado pela ação.</param>
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
        [FromServices] AcompanharOrdemServico casoDeUso,
        [FromQuery] string numero,
        [FromQuery] string documento,
        CancellationToken cancellationToken) =>
        Ok(await casoDeUso.ExecutarAsync(numero, documento, cancellationToken));

    /// <summary>O cliente aprova o orçamento recebido.</summary>
    /// <param name="requisicao">Número da OS e CPF/CNPJ do titular.</param>
    /// <param name="casoDeUso">Caso de uso executado pela ação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Visão pública da OS, já em execução.</returns>
    /// <response code="200">Orçamento aprovado: a OS entra em execução e as peças são baixadas do estoque.</response>
    /// <response code="400">Número ou documento em formato inválido.</response>
    /// <response code="404">Nenhuma OS corresponde ao número e documento informados.</response>
    /// <response code="422">A OS não está aguardando aprovação (já respondida, expirada ou encerrada).</response>
    /// <remarks>
    /// Mesmo efeito da aprovação registrada pelo atendente. Responder de novo a um orçamento já
    /// respondido é recusado com <c>422</c>, sem alterar nada.
    /// </remarks>
    [HttpPost("orcamento/aprovar")]
    [ProducesResponseType<AcompanhamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AcompanhamentoResponse>> AprovarOrcamento(
        [FromServices] AprovarOrcamentoPeloCliente casoDeUso,
        [FromBody] RespostaAoOrcamentoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await casoDeUso.ExecutarAsync(requisicao, cancellationToken));

    /// <summary>O cliente recusa o orçamento recebido.</summary>
    /// <param name="requisicao">Número da OS, CPF/CNPJ do titular e, opcionalmente, o motivo.</param>
    /// <param name="casoDeUso">Caso de uso executado pela ação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Visão pública da OS, já cancelada.</returns>
    /// <response code="200">Orçamento recusado: a OS é cancelada e as reservas de peça voltam ao estoque.</response>
    /// <response code="400">Número ou documento em formato inválido.</response>
    /// <response code="404">Nenhuma OS corresponde ao número e documento informados.</response>
    /// <response code="422">A OS não está aguardando aprovação (já respondida, expirada ou encerrada).</response>
    [HttpPost("orcamento/reprovar")]
    [ProducesResponseType<AcompanhamentoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AcompanhamentoResponse>> ReprovarOrcamento(
        [FromServices] ReprovarOrcamentoPeloCliente casoDeUso,
        [FromBody] RecusaDoOrcamentoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await casoDeUso.ExecutarAsync(requisicao, cancellationToken));
}
