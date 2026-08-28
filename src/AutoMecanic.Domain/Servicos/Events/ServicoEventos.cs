using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Servicos.Events;

/// <summary>Um novo serviço entrou no catálogo da oficina.</summary>
public sealed record ServicoCadastrado(Guid ServicoId, string Nome, decimal Preco) : DomainEvent;

/// <summary>O preço de tabela do serviço foi alterado.</summary>
public sealed record PrecoDoServicoReajustado(Guid ServicoId, decimal PrecoAnterior, decimal PrecoNovo) : DomainEvent;

/// <summary>O serviço saiu de linha e não pode mais ser incluído em novas Ordens de Serviço.</summary>
public sealed record ServicoInativado(Guid ServicoId, string Nome) : DomainEvent;
