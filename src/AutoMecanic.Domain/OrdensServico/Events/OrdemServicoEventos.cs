using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.OrdensServico.Events;

// Cada evento abaixo corresponde a um post-it laranja do Event Storming e é nomeado no
// passado, como um fato consumado do negócio. A correspondência com o quadro completo está
// documentada em docs/01-event-storming.md.

/// <summary>O veículo foi recebido e a Ordem de Serviço foi aberta.</summary>
public sealed record OrdemDeServicoAberta(
    Guid OrdemServicoId,
    string Numero,
    Guid ClienteId,
    Guid VeiculoId) : DomainEvent;

/// <summary>Um mecânico assumiu a OS e iniciou a avaliação do veículo.</summary>
public sealed record DiagnosticoIniciado(Guid OrdemServicoId, Guid? ResponsavelId) : DomainEvent;

/// <summary>O laudo técnico foi registrado na OS.</summary>
public sealed record DiagnosticoRegistrado(Guid OrdemServicoId, string Diagnostico) : DomainEvent;

/// <summary>Um serviço do catálogo foi incluído na OS.</summary>
public sealed record ServicoIncluidoNaOrdem(
    Guid OrdemServicoId,
    Guid ItemId,
    Guid ServicoId,
    int Quantidade,
    decimal Subtotal) : DomainEvent;

/// <summary>Uma peça foi incluída na OS. Dispara a reserva no estoque.</summary>
public sealed record PecaIncluidaNaOrdem(
    Guid OrdemServicoId,
    Guid ItemId,
    Guid PecaId,
    int Quantidade,
    decimal Subtotal) : DomainEvent;

/// <summary>Um item foi retirado da OS. Se era peça, libera a reserva no estoque.</summary>
public sealed record ItemRemovidoDaOrdem(Guid OrdemServicoId, Guid ItemId, bool EraPeca, Guid ReferenciaId, int Quantidade) : DomainEvent;

/// <summary>O orçamento foi calculado automaticamente a partir dos itens da OS.</summary>
public sealed record OrcamentoGerado(
    Guid OrdemServicoId,
    Guid OrcamentoId,
    decimal ValorServicos,
    decimal ValorPecas,
    decimal ValorTotal) : DomainEvent;

/// <summary>O orçamento foi enviado ao cliente. A OS passa a aguardar aprovação.</summary>
public sealed record OrcamentoEnviadoAoCliente(
    Guid OrdemServicoId,
    Guid OrcamentoId,
    Guid ClienteId,
    decimal ValorTotal,
    DateTimeOffset ValidoAte) : DomainEvent;

/// <summary>O cliente aprovou o orçamento. Autoriza a execução e o consumo das peças.</summary>
public sealed record OrcamentoAprovadoPeloCliente(
    Guid OrdemServicoId,
    Guid OrcamentoId,
    decimal ValorTotal) : DomainEvent;

/// <summary>O cliente reprovou o orçamento. Libera as reservas e encerra a OS.</summary>
public sealed record OrcamentoReprovadoPeloCliente(
    Guid OrdemServicoId,
    Guid OrcamentoId,
    string Motivo) : DomainEvent;

/// <summary>O prazo de resposta do orçamento venceu sem decisão do cliente.</summary>
public sealed record OrcamentoExpirado(Guid OrdemServicoId, Guid OrcamentoId) : DomainEvent;

/// <summary>A execução dos serviços começou. Marca o início da contagem do tempo de execução.</summary>
public sealed record ExecucaoIniciada(Guid OrdemServicoId, DateTimeOffset IniciadaEm) : DomainEvent;

/// <summary>Todos os serviços foram concluídos. Carrega a duração real para o indicador de tempo médio.</summary>
public sealed record ServicoFinalizado(
    Guid OrdemServicoId,
    DateTimeOffset FinalizadaEm,
    int DuracaoEmMinutos) : DomainEvent;

/// <summary>O veículo foi entregue ao cliente. Encerra o ciclo de vida da OS.</summary>
public sealed record VeiculoEntregueAoCliente(
    Guid OrdemServicoId,
    Guid ClienteId,
    Guid VeiculoId,
    DateTimeOffset EntregueEm) : DomainEvent;

/// <summary>A OS foi cancelada antes da execução.</summary>
public sealed record OrdemDeServicoCancelada(Guid OrdemServicoId, string Motivo) : DomainEvent;

/// <summary>
/// Transição de status da OS. Evento transversal usado para notificar o cliente e alimentar
/// a consulta pública de acompanhamento.
/// </summary>
public sealed record StatusDaOrdemAlterado(
    Guid OrdemServicoId,
    string Numero,
    StatusOrdemServico? StatusAnterior,
    StatusOrdemServico StatusAtual) : DomainEvent;
