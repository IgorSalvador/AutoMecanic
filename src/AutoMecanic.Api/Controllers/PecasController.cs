using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Estoque;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Domain.Estoque;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>Gestão de peças e insumos, com controle de estoque.</summary>
[ApiController]
[Route("api/v1/pecas")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Consultar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PecasController(IServicoDeEstoque servico) : ControllerBase
{
    /// <summary>Cadastra uma peça ou insumo.</summary>
    /// <param name="requisicao">Código, nome, unidade, preço, saldo inicial e ponto de ressuprimento.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Peça cadastrada.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="409">Já existe peça com o mesmo código.</response>
    [HttpPost]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PecaResponse>> Cadastrar(
        [FromBody] CriarPecaRequest requisicao,
        CancellationToken cancellationToken)
    {
        var peca = await servico.CadastrarAsync(requisicao, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = peca.Id }, peca);
    }

    /// <summary>Obtém uma peça pelo identificador.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Peça encontrada, com saldo físico, reservado e disponível.</response>
    /// <response code="404">Peça inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PecaResponse>> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(id, cancellationToken));

    /// <summary>Obtém uma peça pelo código interno (SKU).</summary>
    /// <param name="codigo">Código da peça.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Peça encontrada.</response>
    /// <response code="404">Peça inexistente.</response>
    [HttpGet("codigo/{codigo}")]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PecaResponse>> ObterPorCodigo(string codigo, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorCodigoAsync(codigo, cancellationToken));

    /// <summary>Lista peças com filtro e paginação.</summary>
    /// <param name="termoDeBusca">Texto livre aplicado a código, nome e descrição.</param>
    /// <param name="apenasAtivas">Filtra pela situação da peça.</param>
    /// <param name="apenasAbaixoDoMinimo">Quando verdadeiro, traz somente o que precisa de reposição.</param>
    /// <param name="paginacao">Página e tamanho de página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Página de peças.</response>
    [HttpGet]
    [ProducesResponseType<ResultadoPaginado<PecaResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoPaginado<PecaResponse>>> Listar(
        [FromQuery] string? termoDeBusca,
        [FromQuery] bool? apenasAtivas,
        [FromQuery] bool? apenasAbaixoDoMinimo,
        [FromQuery] ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarAsync(termoDeBusca, apenasAtivas, apenasAbaixoDoMinimo, paginacao, cancellationToken));

    /// <summary>Atualiza os dados descritivos e o ponto de ressuprimento.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="requisicao">Novos dados. Preço e saldo têm operações próprias.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Peça atualizada.</response>
    /// <response code="404">Peça inexistente.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PecaResponse>> Atualizar(
        Guid id,
        [FromBody] AtualizarPecaRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AtualizarAsync(id, requisicao, cancellationToken));

    /// <summary>Reajusta o preço de venda da peça.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="requisicao">Novo preço unitário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Preço reajustado.</response>
    /// <response code="404">Peça inexistente.</response>
    [HttpPatch("{id:guid}/preco")]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PecaResponse>> ReajustarPreco(
        Guid id,
        [FromBody] ReajustarPrecoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ReajustarPrecoAsync(id, requisicao.NovoPreco, cancellationToken));

    /// <summary>Registra entrada de mercadoria no estoque.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="requisicao">Quantidade recebida e justificativa.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Entrada registrada; o razão de estoque recebe o lançamento.</response>
    /// <response code="404">Peça inexistente.</response>
    /// <response code="422">Peça inativa ou quantidade inválida.</response>
    [HttpPost("{id:guid}/entradas")]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PecaResponse>> RegistrarEntrada(
        Guid id,
        [FromBody] RegistrarEntradaRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.RegistrarEntradaAsync(id, requisicao, cancellationToken));

    /// <summary>Registra baixa por perda, avaria ou vencimento.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="requisicao">Quantidade baixada e justificativa obrigatória.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Perda registrada.</response>
    /// <response code="404">Peça inexistente.</response>
    /// <response code="422">Saldo disponível insuficiente.</response>
    [HttpPost("{id:guid}/perdas")]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PecaResponse>> RegistrarPerda(
        Guid id,
        [FromBody] RegistrarPerdaRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.RegistrarPerdaAsync(id, requisicao, cancellationToken));

    /// <summary>Ajusta o saldo para a quantidade apurada em contagem física.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="requisicao">Saldo real contado e justificativa.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Saldo ajustado.</response>
    /// <response code="404">Peça inexistente.</response>
    /// <response code="422">O saldo apurado é menor que a quantidade já reservada.</response>
    [HttpPost("{id:guid}/ajustes")]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType<PecaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PecaResponse>> AjustarSaldo(
        Guid id,
        [FromBody] AjustarEstoqueRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AjustarSaldoAsync(id, requisicao, cancellationToken));

    /// <summary>Lista as peças que precisam de reposição.</summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Peças no ponto de ressuprimento ou abaixo dele, com sugestão de compra.</response>
    [HttpGet("alertas")]
    [ProducesResponseType<IReadOnlyList<AlertaDeEstoqueResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AlertaDeEstoqueResponse>>> ListarAlertas(
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarAlertasDeEstoqueAsync(cancellationToken));

    /// <summary>Consulta o extrato de movimentações do estoque.</summary>
    /// <param name="pecaId">Restringe a uma peça.</param>
    /// <param name="ordemServicoId">Restringe às movimentações de uma OS.</param>
    /// <param name="tipo">Restringe a um tipo de movimento.</param>
    /// <param name="de">Início do período.</param>
    /// <param name="ate">Fim do período.</param>
    /// <param name="paginacao">Página e tamanho de página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Página de lançamentos, do mais recente ao mais antigo.</response>
    /// <remarks>O razão é append-only: nenhum lançamento é alterado ou removido.</remarks>
    [HttpGet("movimentos")]
    [ProducesResponseType<ResultadoPaginado<MovimentoEstoqueResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoPaginado<MovimentoEstoqueResponse>>> ListarMovimentos(
        [FromQuery] Guid? pecaId,
        [FromQuery] Guid? ordemServicoId,
        [FromQuery] TipoMovimentoEstoque? tipo,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery] ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarMovimentosAsync(pecaId, ordemServicoId, tipo, de, ate, paginacao, cancellationToken));

    /// <summary>Inativa uma peça.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Peça inativada.</response>
    /// <response code="404">Peça inexistente.</response>
    /// <response code="422">A peça possui quantidade reservada em orçamentos pendentes.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Inativar(Guid id, CancellationToken cancellationToken)
    {
        await servico.InativarAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Reativa uma peça inativa.</summary>
    /// <param name="id">Identificador da peça.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Peça reativada.</response>
    /// <response code="404">Peça inexistente.</response>
    [HttpPost("{id:guid}/reativar")]
    [Authorize(Policy = PoliticasDeAutorizacao.GerenciarEstoque)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken cancellationToken)
    {
        await servico.ReativarAsync(id, cancellationToken);

        return NoContent();
    }
}
