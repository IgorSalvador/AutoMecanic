using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Clientes.Events;

/// <summary>Um novo cliente passou a existir na oficina.</summary>
public sealed record ClienteCadastrado(Guid ClienteId, string Documento, string Nome) : DomainEvent;

/// <summary>Os dados de contato do cliente mudaram (e-mail e/ou telefone).</summary>
public sealed record DadosDeContatoDoClienteAtualizados(Guid ClienteId, string Email, string Telefone) : DomainEvent;

/// <summary>O cliente foi inativado; não pode mais ser usado em novas Ordens de Serviço.</summary>
public sealed record ClienteInativado(Guid ClienteId, string Motivo) : DomainEvent;

/// <summary>O cliente inativo voltou a ser atendível.</summary>
public sealed record ClienteReativado(Guid ClienteId) : DomainEvent;
