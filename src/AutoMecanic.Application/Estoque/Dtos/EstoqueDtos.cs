using AutoMecanic.Domain.Estoque;

namespace AutoMecanic.Application.Estoque.Dtos;

/// <summary>Dados para cadastrar uma peça ou insumo no estoque.</summary>
/// <param name="Codigo">Código interno / SKU, único.</param>
/// <param name="Nome">Nome da peça ou insumo.</param>
/// <param name="Descricao">Detalhamento, aplicação, referência do fabricante. Opcional.</param>
/// <param name="UnidadeMedida">Unidade em que o item é controlado.</param>
/// <param name="PrecoUnitario">Preço de venda, maior que zero.</param>
/// <param name="QuantidadeInicial">Saldo físico inicial.</param>
/// <param name="EstoqueMinimo">Ponto de ressuprimento que dispara o alerta de compra.</param>
public sealed record CriarPecaRequest(
    string Codigo,
    string Nome,
    string? Descricao,
    UnidadeMedida UnidadeMedida,
    decimal PrecoUnitario,
    int QuantidadeInicial,
    int EstoqueMinimo);

/// <summary>
/// Dados atualizáveis da peça. Código, preço e saldo têm operações próprias — cada um é uma
/// decisão de negócio distinta e precisa de trilha de auditoria separada.
/// </summary>
/// <param name="Nome">Nome da peça ou insumo.</param>
/// <param name="Descricao">Detalhamento. Opcional.</param>
/// <param name="UnidadeMedida">Unidade em que o item é controlado.</param>
/// <param name="EstoqueMinimo">Ponto de ressuprimento.</param>
public sealed record AtualizarPecaRequest(
    string Nome,
    string? Descricao,
    UnidadeMedida UnidadeMedida,
    int EstoqueMinimo);

/// <summary>Entrada de mercadoria no estoque.</summary>
/// <param name="Quantidade">Quantidade recebida, maior que zero.</param>
/// <param name="Motivo">Justificativa (nota fiscal, fornecedor, devolução).</param>
public sealed record RegistrarEntradaRequest(int Quantidade, string Motivo);

/// <summary>Baixa por perda, avaria ou vencimento.</summary>
/// <param name="Quantidade">Quantidade baixada, maior que zero.</param>
/// <param name="Motivo">Justificativa obrigatória da perda.</param>
public sealed record RegistrarPerdaRequest(int Quantidade, string Motivo);

/// <summary>Acerto do saldo após contagem física.</summary>
/// <param name="QuantidadeApurada">Saldo real contado na prateleira.</param>
/// <param name="Motivo">Justificativa do ajuste (inventário, divergência).</param>
public sealed record AjustarEstoqueRequest(int QuantidadeApurada, string Motivo);

/// <summary>Representação da peça devolvida pela API, já com o saldo decomposto.</summary>
public sealed record PecaResponse
{
    public required Guid Id { get; init; }

    public required string Codigo { get; init; }

    public required string Nome { get; init; }

    public string? Descricao { get; init; }

    public required UnidadeMedida UnidadeMedida { get; init; }

    public required decimal PrecoUnitario { get; init; }

    /// <summary>Saldo físico na prateleira.</summary>
    public required int QuantidadeEmEstoque { get; init; }

    /// <summary>Parcela já comprometida com orçamentos pendentes de aprovação.</summary>
    public required int QuantidadeReservada { get; init; }

    /// <summary>O que pode ser prometido a uma nova Ordem de Serviço.</summary>
    public required int QuantidadeDisponivel { get; init; }

    public required int EstoqueMinimo { get; init; }

    /// <summary>Indica necessidade de ressuprimento.</summary>
    public required bool AbaixoDoEstoqueMinimo { get; init; }

    public required bool Ativo { get; init; }

    public required DateTimeOffset CadastradoEm { get; init; }

    public DateTimeOffset? AtualizadoEm { get; init; }

    public static PecaResponse De(Peca peca) => new()
    {
        Id = peca.Id,
        Codigo = peca.Codigo,
        Nome = peca.Nome,
        Descricao = peca.Descricao,
        UnidadeMedida = peca.UnidadeMedida,
        PrecoUnitario = peca.PrecoUnitario.Valor,
        QuantidadeEmEstoque = peca.QuantidadeEmEstoque,
        QuantidadeReservada = peca.QuantidadeReservada,
        QuantidadeDisponivel = peca.QuantidadeDisponivel,
        EstoqueMinimo = peca.EstoqueMinimo,
        AbaixoDoEstoqueMinimo = peca.AbaixoDoEstoqueMinimo,
        Ativo = peca.Ativo,
        CadastradoEm = peca.CadastradoEm,
        AtualizadoEm = peca.AtualizadoEm
    };
}

/// <summary>Lançamento do razão de estoque devolvido pela API.</summary>
public sealed record MovimentoEstoqueResponse
{
    public required Guid Id { get; init; }

    public required Guid PecaId { get; init; }

    public required TipoMovimentoEstoque Tipo { get; init; }

    public required int Quantidade { get; init; }

    public required int SaldoAnterior { get; init; }

    public required int SaldoAtual { get; init; }

    public required string Motivo { get; init; }

    public Guid? OrdemServicoId { get; init; }

    public required DateTimeOffset OcorridoEm { get; init; }

    public static MovimentoEstoqueResponse De(MovimentoEstoque movimento) => new()
    {
        Id = movimento.Id,
        PecaId = movimento.PecaId,
        Tipo = movimento.Tipo,
        Quantidade = movimento.Quantidade,
        SaldoAnterior = movimento.SaldoAnterior,
        SaldoAtual = movimento.SaldoAtual,
        Motivo = movimento.Motivo,
        OrdemServicoId = movimento.OrdemServicoId,
        OcorridoEm = movimento.OcorridoEm
    };
}

/// <summary>Linha do relatório de peças que precisam de reposição.</summary>
/// <param name="PecaId">Identificador da peça.</param>
/// <param name="Codigo">Código interno / SKU.</param>
/// <param name="Nome">Nome da peça.</param>
/// <param name="QuantidadeDisponivel">Saldo disponível atual.</param>
/// <param name="EstoqueMinimo">Ponto de ressuprimento configurado.</param>
/// <param name="QuantidadeSugeridaDeCompra">Quanto comprar para voltar ao dobro do mínimo.</param>
public sealed record AlertaDeEstoqueResponse(
    Guid PecaId,
    string Codigo,
    string Nome,
    int QuantidadeDisponivel,
    int EstoqueMinimo,
    int QuantidadeSugeridaDeCompra);
