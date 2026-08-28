using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Indicadores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>Indicadores gerenciais da operação da oficina.</summary>
[ApiController]
[Route("api/v1/indicadores")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Consultar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class IndicadoresController(IServicoDeIndicadores servico) : ControllerBase
{
    /// <summary>Tempo médio de execução dos serviços no período.</summary>
    /// <param name="de">Início do período. Quando omitido, considera os últimos 30 dias.</param>
    /// <param name="ate">Fim do período. Quando omitido, considera o instante atual.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Média, mediana, extremos e aderência à estimativa.</response>
    /// <remarks>
    /// <para>
    /// O tempo de execução é medido entre a aprovação do orçamento (início da execução) e a
    /// finalização dos serviços. Só entram no cálculo as Ordens de Serviço finalizadas dentro
    /// do período.
    /// </para>
    /// <para>
    /// A <b>mediana</b> acompanha a média porque uma única OS excepcionalmente longa distorce
    /// a média sem que a operação tenha piorado. A <b>aderência à estimativa</b> compara o
    /// tempo real com o tempo previsto no catálogo: acima de 1, a oficina leva mais tempo do
    /// que promete ao cliente.
    /// </para>
    /// </remarks>
    [HttpGet("tempo-medio-execucao")]
    [ProducesResponseType<TempoMedioDeExecucaoResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TempoMedioDeExecucaoResponse>> ObterTempoMedioDeExecucao(
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        CancellationToken cancellationToken) =>
        Ok(await servico.ObterTempoMedioDeExecucaoAsync(de, ate, cancellationToken));

    /// <summary>Fotografia atual da operação.</summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Ordens por situação, total em aberto, aguardando aprovação e peças críticas.</response>
    [HttpGet("painel")]
    [ProducesResponseType<PainelOperacionalResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PainelOperacionalResponse>> ObterPainel(CancellationToken cancellationToken) =>
        Ok(await servico.ObterPainelOperacionalAsync(cancellationToken));
}
