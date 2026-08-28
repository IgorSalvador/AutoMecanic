using AutoMecanic.Api.Configuracao;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.OrdensServico;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoMecanic.Api.Controllers;

/// <summary>
/// Ciclo de vida da Ordem de Serviço.
/// <para>
/// Cada operação abaixo corresponde a uma ação real na oficina e provoca — quando é o caso —
/// a mudança automática de status exigida pelo requisito. Nenhum endpoint permite atribuir um
/// status arbitrariamente.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/ordens-servico")]
[Produces("application/json")]
[Authorize(Policy = PoliticasDeAutorizacao.Consultar)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class OrdensServicoController(IServicoDeOrdensServico servico) : ControllerBase
{
    // -----------------------------------------------------------------
    // Abertura
    // -----------------------------------------------------------------

    /// <summary>Abre uma OS para cliente e veículo já cadastrados.</summary>
    /// <param name="requisicao">Cliente, veículo e relato do problema.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">OS aberta no status <c>Recebida</c>.</response>
    /// <response code="404">Cliente ou veículo inexistente.</response>
    /// <response code="409">O veículo não pertence ao cliente informado.</response>
    /// <response code="422">Cliente ou veículo inativo.</response>
    [HttpPost]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> Abrir(
        [FromBody] AbrirOrdemServicoRequest requisicao,
        CancellationToken cancellationToken)
    {
        var ordem = await servico.AbrirAsync(requisicao, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = ordem.Id }, ordem);
    }

    /// <summary>
    /// Recepção do veículo no balcão: identifica o cliente pelo CPF/CNPJ, localiza ou cadastra
    /// o veículo pela placa e abre a OS — tudo em uma única transação.
    /// </summary>
    /// <param name="requisicao">Documento do cliente, placa e relato do problema.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">OS aberta no status <c>Recebida</c>.</response>
    /// <response code="400">Documento, placa ou dados obrigatórios inválidos.</response>
    /// <response code="422">Cliente ou veículo inativo.</response>
    /// <remarks>
    /// Cliente e veículo são cadastrados automaticamente quando ainda não existem — nesse caso,
    /// os dados de contato e as informações do veículo passam a ser obrigatórios.
    /// </remarks>
    [HttpPost("recepcao")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> ReceberVeiculo(
        [FromBody] ReceberVeiculoRequest requisicao,
        CancellationToken cancellationToken)
    {
        var ordem = await servico.ReceberVeiculoAsync(requisicao, cancellationToken);

        return CreatedAtAction(nameof(ObterPorId), new { id = ordem.Id }, ordem);
    }

    // -----------------------------------------------------------------
    // Diagnóstico
    // -----------------------------------------------------------------

    /// <summary>Inicia o diagnóstico. Move a OS de <c>Recebida</c> para <c>Em diagnóstico</c>.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Diagnóstico iniciado.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não está em <c>Recebida</c>.</response>
    [HttpPost("{id:guid}/diagnostico/iniciar")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> IniciarDiagnostico(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await servico.IniciarDiagnosticoAsync(id, cancellationToken));

    /// <summary>Registra o laudo técnico do mecânico.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Texto do laudo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Laudo registrado.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não está em diagnóstico nem em execução.</response>
    [HttpPost("{id:guid}/diagnostico")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> RegistrarDiagnostico(
        Guid id,
        [FromBody] RegistrarDiagnosticoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.RegistrarDiagnosticoAsync(id, requisicao, cancellationToken));

    // -----------------------------------------------------------------
    // Composição de itens
    // -----------------------------------------------------------------

    /// <summary>Inclui um serviço do catálogo na OS.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Serviço e quantidade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Serviço incluído; o orçamento em elaboração é recalculado.</response>
    /// <response code="404">OS ou serviço inexistente.</response>
    /// <response code="422">Serviço inativo, ou itens congelados após o envio do orçamento.</response>
    /// <remarks>Preço e tempo estimado são copiados do catálogo e congelados no item.</remarks>
    [HttpPost("{id:guid}/servicos")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> AdicionarServico(
        Guid id,
        [FromBody] AdicionarServicoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AdicionarServicoAsync(id, requisicao, cancellationToken));

    /// <summary>Altera a quantidade de um item de serviço.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="itemId">Identificador do item.</param>
    /// <param name="requisicao">Nova quantidade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Quantidade alterada.</response>
    /// <response code="404">OS ou item inexistente.</response>
    /// <response code="422">Itens congelados após o envio do orçamento.</response>
    [HttpPatch("{id:guid}/servicos/{itemId:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> AlterarQuantidadeDeServico(
        Guid id,
        Guid itemId,
        [FromBody] AlterarQuantidadeRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AlterarQuantidadeDeServicoAsync(id, itemId, requisicao, cancellationToken));

    /// <summary>Remove um item de serviço da OS.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="itemId">Identificador do item.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Item removido.</response>
    /// <response code="404">OS ou item inexistente.</response>
    /// <response code="422">Itens congelados após o envio do orçamento.</response>
    [HttpDelete("{id:guid}/servicos/{itemId:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> RemoverServico(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken) =>
        Ok(await servico.RemoverServicoAsync(id, itemId, cancellationToken));

    /// <summary>Inclui uma peça na OS e reserva a quantidade no estoque.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Peça e quantidade.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Peça incluída e quantidade reservada.</response>
    /// <response code="404">OS ou peça inexistente.</response>
    /// <response code="422">Saldo disponível insuficiente, peça inativa, ou itens congelados.</response>
    /// <remarks>
    /// A reserva impede que duas Ordens de Serviço prometam a mesma última peça ao cliente.
    /// A baixa efetiva só ocorre na aprovação do orçamento.
    /// </remarks>
    [HttpPost("{id:guid}/pecas")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> AdicionarPeca(
        Guid id,
        [FromBody] AdicionarPecaRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AdicionarPecaAsync(id, requisicao, cancellationToken));

    /// <summary>Remove uma peça da OS e devolve a quantidade reservada ao estoque.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="itemId">Identificador do item de peça.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Peça removida e reserva liberada.</response>
    /// <response code="404">OS ou item inexistente.</response>
    /// <response code="422">A peça já foi aplicada no veículo, ou itens congelados.</response>
    [HttpDelete("{id:guid}/pecas/{itemId:guid}")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> RemoverPeca(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken) =>
        Ok(await servico.RemoverPecaAsync(id, itemId, cancellationToken));

    // -----------------------------------------------------------------
    // Orçamento
    // -----------------------------------------------------------------

    /// <summary>Gera o orçamento automaticamente a partir dos itens da OS.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Percentual de desconto comercial, de 0 a 100.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Orçamento gerado.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não tem itens, ou não está em situação que permita gerar orçamento.</response>
    /// <remarks>O valor nunca é informado: é sempre a soma dos itens com o desconto aplicado.</remarks>
    [HttpPost("{id:guid}/orcamento")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> GerarOrcamento(
        Guid id,
        [FromBody] GerarOrcamentoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.GerarOrcamentoAsync(id, requisicao, cancellationToken));

    /// <summary>Envia o orçamento ao cliente. Move a OS para <c>Aguardando aprovação</c>.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Prazo de validade em dias.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Orçamento enviado; os itens ficam congelados.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">Não há orçamento gerado, ou a OS não está em situação que permita o envio.</response>
    [HttpPost("{id:guid}/orcamento/enviar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> EnviarOrcamento(
        Guid id,
        [FromBody] EnviarOrcamentoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.EnviarOrcamentoAsync(id, requisicao, cancellationToken));

    /// <summary>Registra a aprovação do orçamento pelo cliente.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Orçamento aprovado; a OS passa a <c>Em execução</c> e as peças são baixadas.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não está aguardando aprovação.</response>
    [HttpPost("{id:guid}/orcamento/aprovar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> AprovarOrcamento(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await servico.AprovarOrcamentoAsync(id, cancellationToken));

    /// <summary>Registra a reprovação do orçamento pelo cliente.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Motivo informado pelo cliente.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Orçamento reprovado; a OS é cancelada e as reservas são devolvidas.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não está aguardando aprovação.</response>
    [HttpPost("{id:guid}/orcamento/reprovar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> ReprovarOrcamento(
        Guid id,
        [FromBody] ReprovarOrcamentoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ReprovarOrcamentoAsync(id, requisicao, cancellationToken));

    /// <summary>Devolve a OS ao diagnóstico para revisão do escopo.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="motivo">Justificativa da revisão.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">OS devolvida ao diagnóstico; os itens voltam a ser editáveis.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não está aguardando aprovação.</response>
    [HttpPost("{id:guid}/orcamento/revisar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> RetornarParaDiagnostico(
        Guid id,
        [FromQuery] string? motivo,
        CancellationToken cancellationToken) =>
        Ok(await servico.RetornarParaDiagnosticoAsync(id, motivo, cancellationToken));

    // -----------------------------------------------------------------
    // Execução e entrega
    // -----------------------------------------------------------------

    /// <summary>Conclui os serviços. Move a OS para <c>Finalizada</c>.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Observação do mecânico.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Serviços finalizados; a duração real passa a alimentar o indicador de tempo médio.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não está em execução.</response>
    [HttpPost("{id:guid}/finalizar")]
    [Authorize(Policy = PoliticasDeAutorizacao.ExecutarServico)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> Finalizar(
        Guid id,
        [FromBody] FinalizarServicoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.FinalizarServicoAsync(id, requisicao, cancellationToken));

    /// <summary>Registra a entrega do veículo ao cliente.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Observação da entrega.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Veículo entregue; a OS chega ao estado terminal <c>Entregue</c>.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS não está finalizada.</response>
    [HttpPost("{id:guid}/entregar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> Entregar(
        Guid id,
        [FromBody] EntregarVeiculoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.EntregarVeiculoAsync(id, requisicao, cancellationToken));

    /// <summary>Cancela a OS antes da execução.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Motivo obrigatório do cancelamento.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">OS cancelada e reservas de peça devolvidas.</response>
    /// <response code="404">OS inexistente.</response>
    /// <response code="422">A OS já está em execução ou em estado terminal.</response>
    /// <remarks>
    /// Não é possível cancelar após a aprovação do orçamento: a partir daí peças já saíram do
    /// estoque e horas já foram trabalhadas.
    /// </remarks>
    [HttpPost("{id:guid}/cancelar")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrdemServicoResponse>> Cancelar(
        Guid id,
        [FromBody] CancelarOrdemServicoRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.CancelarAsync(id, requisicao, cancellationToken));

    /// <summary>Atribui o responsável técnico pela OS.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="requisicao">Usuário responsável.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Responsável atribuído.</response>
    /// <response code="404">OS inexistente.</response>
    [HttpPatch("{id:guid}/responsavel")]
    [Authorize(Policy = PoliticasDeAutorizacao.Atender)]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdemServicoResponse>> AtribuirResponsavel(
        Guid id,
        [FromBody] AtribuirResponsavelRequest requisicao,
        CancellationToken cancellationToken) =>
        Ok(await servico.AtribuirResponsavelAsync(id, requisicao, cancellationToken));

    // -----------------------------------------------------------------
    // Consultas
    // -----------------------------------------------------------------

    /// <summary>Detalha uma OS, com itens, orçamento e linha do tempo.</summary>
    /// <param name="id">Identificador da OS.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">OS encontrada.</response>
    /// <response code="404">OS inexistente.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdemServicoResponse>> ObterPorId(Guid id, CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(id, cancellationToken));

    /// <summary>Detalha uma OS pelo número legível.</summary>
    /// <param name="numero">Número no formato OS-AAAA-NNNNNN.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">OS encontrada.</response>
    /// <response code="404">OS inexistente.</response>
    [HttpGet("numero/{numero}")]
    [ProducesResponseType<OrdemServicoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdemServicoResponse>> ObterPorNumero(
        string numero,
        CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorNumeroAsync(numero, cancellationToken));

    /// <summary>Lista Ordens de Serviço com filtros e paginação.</summary>
    /// <param name="status">Restringe a uma situação.</param>
    /// <param name="clienteId">Restringe a um cliente.</param>
    /// <param name="veiculoId">Restringe a um veículo.</param>
    /// <param name="de">Data inicial de abertura.</param>
    /// <param name="ate">Data final de abertura.</param>
    /// <param name="paginacao">Página e tamanho de página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Página de Ordens de Serviço, da mais recente à mais antiga.</response>
    [HttpGet]
    [ProducesResponseType<ResultadoPaginado<OrdemServicoResumoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoPaginado<OrdemServicoResumoResponse>>> Listar(
        [FromQuery] StatusOrdemServico? status,
        [FromQuery] Guid? clienteId,
        [FromQuery] Guid? veiculoId,
        [FromQuery] DateTimeOffset? de,
        [FromQuery] DateTimeOffset? ate,
        [FromQuery] ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken) =>
        Ok(await servico.ListarAsync(status, clienteId, veiculoId, de, ate, paginacao, cancellationToken));

    /// <summary>Expira orçamentos vencidos e cancela as respectivas Ordens de Serviço.</summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Quantidade de orçamentos expirados.</response>
    /// <remarks>
    /// Rotina de manutenção, destinada a ser acionada por um agendador. Libera as reservas
    /// de peça presas em orçamentos que o cliente nunca respondeu.
    /// </remarks>
    [HttpPost("manutencao/expirar-orcamentos")]
    [Authorize(Policy = PoliticasDeAutorizacao.Administrar)]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> ExpirarOrcamentosVencidos(CancellationToken cancellationToken) =>
        Ok(await servico.ExpirarOrcamentosVencidosAsync(cancellationToken));
}
