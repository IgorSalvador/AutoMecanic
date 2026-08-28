using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Estoque.Events;

/// <summary>Uma nova peça ou insumo passou a ser controlado pelo estoque.</summary>
public sealed record PecaCadastrada(Guid PecaId, string Codigo, string Nome) : DomainEvent;

/// <summary>
/// Um lançamento foi feito no razão de estoque. É o evento que materializa o
/// <see cref="MovimentoEstoque"/>, garantindo que todo saldo tenha um lançamento correspondente.
/// </summary>
public sealed record EstoqueMovimentado(
    Guid PecaId,
    TipoMovimentoEstoque Tipo,
    int Quantidade,
    int SaldoAnterior,
    int SaldoAtual,
    string Motivo,
    Guid? OrdemServicoId) : DomainEvent;

/// <summary>
/// O saldo disponível cruzou para baixo o ponto de ressuprimento. Dispara a necessidade
/// de compra — é o alerta que resolve a "falha no controle de peças e insumos".
/// </summary>
public sealed record EstoqueAtingiuNivelMinimo(
    Guid PecaId,
    string Codigo,
    string Nome,
    int SaldoDisponivel,
    int EstoqueMinimo) : DomainEvent;

/// <summary>Quantidade separada para um orçamento ainda não aprovado.</summary>
public sealed record QuantidadeReservada(Guid PecaId, Guid OrdemServicoId, int Quantidade) : DomainEvent;

/// <summary>Reserva desfeita — tipicamente após reprovação ou cancelamento do orçamento.</summary>
public sealed record ReservaLiberada(Guid PecaId, Guid OrdemServicoId, int Quantidade) : DomainEvent;

/// <summary>O preço unitário de venda da peça foi alterado.</summary>
public sealed record PrecoDaPecaReajustado(Guid PecaId, decimal PrecoAnterior, decimal PrecoNovo) : DomainEvent;

/// <summary>A peça saiu de linha e não pode mais ser incluída em novas Ordens de Serviço.</summary>
public sealed record PecaInativada(Guid PecaId, string Codigo) : DomainEvent;
