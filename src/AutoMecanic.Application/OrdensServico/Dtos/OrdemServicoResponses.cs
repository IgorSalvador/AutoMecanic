using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.Application.OrdensServico.Dtos;

/// <summary>Item de serviço da OS, com o preço congelado no momento da inclusão.</summary>
/// <param name="Id">Identificador do item.</param>
/// <param name="ServicoId">Serviço de origem no catálogo.</param>
/// <param name="Descricao">Nome do serviço no momento da inclusão.</param>
/// <param name="PrecoUnitario">Preço congelado.</param>
/// <param name="Quantidade">Quantidade contratada.</param>
/// <param name="Subtotal">Preço unitário × quantidade.</param>
/// <param name="TempoEstimadoEmMinutos">Tempo estimado total do item.</param>
public sealed record ItemServicoResponse(
    Guid Id,
    Guid ServicoId,
    string Descricao,
    decimal PrecoUnitario,
    int Quantidade,
    decimal Subtotal,
    int TempoEstimadoEmMinutos)
{
    public static ItemServicoResponse De(ItemServico item) => new(
        item.Id,
        item.ServicoId,
        item.Descricao,
        item.PrecoUnitario.Valor,
        item.Quantidade,
        item.Subtotal.Valor,
        item.TempoTotalEstimadoEmMinutos);
}

/// <summary>Item de peça da OS, com o preço congelado e a situação no estoque.</summary>
/// <param name="Id">Identificador do item.</param>
/// <param name="PecaId">Peça de origem no estoque.</param>
/// <param name="Codigo">Código congelado.</param>
/// <param name="Nome">Nome congelado.</param>
/// <param name="PrecoUnitario">Preço congelado.</param>
/// <param name="Quantidade">Quantidade prevista.</param>
/// <param name="Subtotal">Preço unitário × quantidade.</param>
/// <param name="Reservada">Quantidade separada no estoque, ainda não baixada.</param>
/// <param name="Consumida">Peça já aplicada no veículo e baixada do estoque.</param>
public sealed record ItemPecaResponse(
    Guid Id,
    Guid PecaId,
    string Codigo,
    string Nome,
    decimal PrecoUnitario,
    int Quantidade,
    decimal Subtotal,
    bool Reservada,
    bool Consumida)
{
    public static ItemPecaResponse De(ItemPeca item) => new(
        item.Id,
        item.PecaId,
        item.CodigoPeca,
        item.NomePeca,
        item.PrecoUnitario.Valor,
        item.Quantidade,
        item.Subtotal.Valor,
        item.Reservada,
        item.Consumida);
}

/// <summary>Orçamento da OS, com a composição do valor apresentada ao cliente.</summary>
public sealed record OrcamentoResponse
{
    public required Guid Id { get; init; }

    public required decimal ValorServicos { get; init; }

    public required decimal ValorPecas { get; init; }

    public required decimal ValorBruto { get; init; }

    public required decimal PercentualDesconto { get; init; }

    public required decimal ValorDesconto { get; init; }

    /// <summary>Valor final que o cliente aprova.</summary>
    public required decimal ValorTotal { get; init; }

    public required StatusOrcamento Status { get; init; }

    public required DateTimeOffset GeradoEm { get; init; }

    public DateTimeOffset? EnviadoEm { get; init; }

    public DateTimeOffset? ValidoAte { get; init; }

    public DateTimeOffset? RespondidoEm { get; init; }

    public string? MotivoReprovacao { get; init; }

    public static OrcamentoResponse De(Orcamento orcamento) => new()
    {
        Id = orcamento.Id,
        ValorServicos = orcamento.ValorServicos.Valor,
        ValorPecas = orcamento.ValorPecas.Valor,
        ValorBruto = orcamento.ValorBruto.Valor,
        PercentualDesconto = orcamento.PercentualDesconto,
        ValorDesconto = orcamento.ValorDesconto.Valor,
        ValorTotal = orcamento.ValorTotal.Valor,
        Status = orcamento.Status,
        GeradoEm = orcamento.GeradoEm,
        EnviadoEm = orcamento.EnviadoEm,
        ValidoAte = orcamento.ValidoAte,
        RespondidoEm = orcamento.RespondidoEm,
        MotivoReprovacao = orcamento.MotivoReprovacao
    };
}

/// <summary>Uma transição de status na linha do tempo da OS.</summary>
/// <param name="StatusAnterior">Situação de origem. Nulo na abertura.</param>
/// <param name="StatusAtual">Situação de destino.</param>
/// <param name="Descricao">Nome legível da situação de destino.</param>
/// <param name="Observacao">Comentário registrado na transição.</param>
/// <param name="OcorridoEm">Momento da transição.</param>
public sealed record HistoricoStatusResponse(
    StatusOrdemServico? StatusAnterior,
    StatusOrdemServico StatusAtual,
    string Descricao,
    string? Observacao,
    DateTimeOffset OcorridoEm)
{
    public static HistoricoStatusResponse De(HistoricoStatus historico) => new(
        historico.StatusAnterior,
        historico.StatusAtual,
        historico.StatusAtual.Descricao(),
        historico.Observacao,
        historico.OcorridoEm);
}

/// <summary>Representação completa da Ordem de Serviço para as APIs administrativas.</summary>
public sealed record OrdemServicoResponse
{
    public required Guid Id { get; init; }

    /// <summary>Número legível informado ao cliente (OS-AAAA-NNNNNN).</summary>
    public required string Numero { get; init; }

    public required StatusOrdemServico Status { get; init; }

    public required string StatusDescricao { get; init; }

    public required Guid ClienteId { get; init; }

    public string? NomeCliente { get; init; }

    public string? DocumentoCliente { get; init; }

    public required Guid VeiculoId { get; init; }

    public string? DescricaoVeiculo { get; init; }

    public required string DescricaoProblema { get; init; }

    public string? DiagnosticoTecnico { get; init; }

    public int? QuilometragemEntrada { get; init; }

    public Guid? ResponsavelId { get; init; }

    public required IReadOnlyList<ItemServicoResponse> Servicos { get; init; }

    public required IReadOnlyList<ItemPecaResponse> Pecas { get; init; }

    public OrcamentoResponse? Orcamento { get; init; }

    public required IReadOnlyList<HistoricoStatusResponse> Historico { get; init; }

    public required decimal ValorTotalServicos { get; init; }

    public required decimal ValorTotalPecas { get; init; }

    public required int TempoEstimadoTotalEmMinutos { get; init; }

    /// <summary>Duração real da execução, em minutos. Disponível a partir da finalização.</summary>
    public int? DuracaoDaExecucaoEmMinutos { get; init; }

    public string? MotivoCancelamento { get; init; }

    public required DateTimeOffset CriadaEm { get; init; }

    public DateTimeOffset? AtualizadaEm { get; init; }

    public DateTimeOffset? ExecucaoIniciadaEm { get; init; }

    public DateTimeOffset? FinalizadaEm { get; init; }

    public DateTimeOffset? EntregueEm { get; init; }

    public static OrdemServicoResponse De(
        OrdemServico ordem,
        string? nomeCliente = null,
        string? documentoCliente = null,
        string? descricaoVeiculo = null) => new()
    {
        Id = ordem.Id,
        Numero = ordem.Numero.Valor,
        Status = ordem.Status,
        StatusDescricao = ordem.Status.Descricao(),
        ClienteId = ordem.ClienteId,
        NomeCliente = nomeCliente,
        DocumentoCliente = documentoCliente,
        VeiculoId = ordem.VeiculoId,
        DescricaoVeiculo = descricaoVeiculo,
        DescricaoProblema = ordem.DescricaoProblema,
        DiagnosticoTecnico = ordem.DiagnosticoTecnico,
        QuilometragemEntrada = ordem.QuilometragemEntrada,
        ResponsavelId = ordem.ResponsavelId,
        Servicos = [.. ordem.ItensServico.Select(ItemServicoResponse.De)],
        Pecas = [.. ordem.ItensPeca.Select(ItemPecaResponse.De)],
        Orcamento = ordem.Orcamento is null ? null : OrcamentoResponse.De(ordem.Orcamento),
        Historico = [.. ordem.Historico.OrderBy(h => h.OcorridoEm).Select(HistoricoStatusResponse.De)],
        ValorTotalServicos = ordem.ValorTotalServicos.Valor,
        ValorTotalPecas = ordem.ValorTotalPecas.Valor,
        TempoEstimadoTotalEmMinutos = ordem.TempoEstimadoTotalEmMinutos,
        DuracaoDaExecucaoEmMinutos = ordem.DuracaoDaExecucao is null
            ? null
            : (int)Math.Round(ordem.DuracaoDaExecucao.Value.TotalMinutes),
        MotivoCancelamento = ordem.MotivoCancelamento,
        CriadaEm = ordem.CriadaEm,
        AtualizadaEm = ordem.AtualizadaEm,
        ExecucaoIniciadaEm = ordem.ExecucaoIniciadaEm,
        FinalizadaEm = ordem.FinalizadaEm,
        EntregueEm = ordem.EntregueEm
    };
}

/// <summary>Projeção enxuta para listagens administrativas.</summary>
public sealed record OrdemServicoResumoResponse(
    Guid Id,
    string Numero,
    StatusOrdemServico Status,
    string StatusDescricao,
    Guid ClienteId,
    Guid VeiculoId,
    decimal ValorTotal,
    DateTimeOffset CriadaEm,
    DateTimeOffset? EntregueEm)
{
    public static OrdemServicoResumoResponse De(OrdemServico ordem) => new(
        ordem.Id,
        ordem.Numero.Valor,
        ordem.Status,
        ordem.Status.Descricao(),
        ordem.ClienteId,
        ordem.VeiculoId,
        ordem.Orcamento?.ValorTotal.Valor ?? (ordem.ValorTotalServicos.Valor + ordem.ValorTotalPecas.Valor),
        ordem.CriadaEm,
        ordem.EntregueEm);
}

/// <summary>
/// Visão pública de acompanhamento, consumida pelo cliente. Expõe apenas o necessário para
/// responder "em que pé está meu carro?", sem dados internos como responsável, custo de peça
/// ou identificadores de outros agregados.
/// </summary>
public sealed record AcompanhamentoResponse
{
    public required string Numero { get; init; }

    public required StatusOrdemServico Status { get; init; }

    public required string StatusDescricao { get; init; }

    public required string Veiculo { get; init; }

    public required string DescricaoProblema { get; init; }

    public string? DiagnosticoTecnico { get; init; }

    /// <summary>Serviços contratados, sem detalhamento de custo por item.</summary>
    public required IReadOnlyList<string> ServicosContratados { get; init; }

    /// <summary>Valor total do orçamento, quando já enviado ao cliente.</summary>
    public decimal? ValorOrcamento { get; init; }

    public StatusOrcamento? SituacaoOrcamento { get; init; }

    public DateTimeOffset? OrcamentoValidoAte { get; init; }

    /// <summary>Linha do tempo simplificada do atendimento.</summary>
    public required IReadOnlyList<HistoricoStatusResponse> LinhaDoTempo { get; init; }

    public required DateTimeOffset AbertaEm { get; init; }

    public DateTimeOffset? FinalizadaEm { get; init; }

    public DateTimeOffset? EntregueEm { get; init; }

    public static AcompanhamentoResponse De(OrdemServico ordem, string descricaoVeiculo)
    {
        // O orçamento só é revelado depois de enviado: enquanto está em elaboração,
        // é rascunho interno da oficina.
        var orcamentoVisivel = ordem.Orcamento is { Status: not StatusOrcamento.EmElaboracao }
            ? ordem.Orcamento
            : null;

        return new AcompanhamentoResponse
        {
            Numero = ordem.Numero.Valor,
            Status = ordem.Status,
            StatusDescricao = ordem.Status.Descricao(),
            Veiculo = descricaoVeiculo,
            DescricaoProblema = ordem.DescricaoProblema,
            DiagnosticoTecnico = ordem.DiagnosticoTecnico,
            ServicosContratados = [.. ordem.ItensServico.Select(item => item.Descricao)],
            ValorOrcamento = orcamentoVisivel?.ValorTotal.Valor,
            SituacaoOrcamento = orcamentoVisivel?.Status,
            OrcamentoValidoAte = orcamentoVisivel?.ValidoAte,
            LinhaDoTempo = [.. ordem.Historico.OrderBy(h => h.OcorridoEm).Select(HistoricoStatusResponse.De)],
            AbertaEm = ordem.CriadaEm,
            FinalizadaEm = ordem.FinalizadaEm,
            EntregueEm = ordem.EntregueEm
        };
    }
}
