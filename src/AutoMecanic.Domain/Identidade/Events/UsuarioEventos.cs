using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Identidade.Events;

/// <summary>Um usuário administrativo foi criado.</summary>
public sealed record UsuarioCriado(Guid UsuarioId, string Email, PerfilUsuario Perfil) : DomainEvent;

/// <summary>Login bem-sucedido. Alimenta a trilha de auditoria de acesso.</summary>
public sealed record UsuarioAutenticado(Guid UsuarioId, string Email, DateTimeOffset Em) : DomainEvent;

/// <summary>Conta bloqueada por excesso de tentativas malsucedidas.</summary>
public sealed record UsuarioBloqueado(Guid UsuarioId, string Email, DateTimeOffset BloqueadoAte) : DomainEvent;

/// <summary>A senha do usuário foi alterada ou redefinida.</summary>
public sealed record SenhaAlterada(Guid UsuarioId) : DomainEvent;

/// <summary>O usuário perdeu o acesso ao sistema.</summary>
public sealed record UsuarioInativado(Guid UsuarioId, string Email) : DomainEvent;
