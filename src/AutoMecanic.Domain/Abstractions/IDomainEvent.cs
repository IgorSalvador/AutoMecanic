namespace AutoMecanic.Domain.Abstractions;

/// <summary>
/// <b>Evento de Domínio</b>: fato relevante para o negócio que já aconteceu.
/// Por convenção o nome é sempre escrito no passado ("OrdemDeServicoAberta"),
/// espelhando os post-its laranja do Event Storming.
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identificador único da ocorrência do evento.</summary>
    Guid EventoId { get; }

    /// <summary>Momento (UTC) em que o fato ocorreu.</summary>
    DateTimeOffset OcorridoEm { get; }
}

/// <summary>Implementação base que preenche identidade e carimbo de tempo do evento.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventoId { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OcorridoEm { get; init; } = DateTimeOffset.UtcNow;
}
