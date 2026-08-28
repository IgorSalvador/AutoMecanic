using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Veiculos.Events;

/// <summary>Um veículo foi vinculado a um cliente e passou a ser atendível pela oficina.</summary>
public sealed record VeiculoCadastrado(Guid VeiculoId, Guid ClienteId, string Placa) : DomainEvent;

/// <summary>A quilometragem do veículo foi atualizada — normalmente na recepção do veículo.</summary>
public sealed record QuilometragemAtualizada(Guid VeiculoId, int QuilometragemAnterior, int QuilometragemAtual) : DomainEvent;

/// <summary>O veículo mudou de dono dentro da base de clientes da oficina.</summary>
public sealed record VeiculoTransferido(Guid VeiculoId, Guid ClienteAnteriorId, Guid NovoClienteId) : DomainEvent;

/// <summary>O veículo foi inativado e não pode mais receber novas Ordens de Serviço.</summary>
public sealed record VeiculoInativado(Guid VeiculoId, string Motivo) : DomainEvent;
