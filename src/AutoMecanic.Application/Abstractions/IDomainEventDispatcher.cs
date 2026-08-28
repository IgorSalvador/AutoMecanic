using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Application.Abstractions;

/// <summary>
/// Publica os eventos de domínio acumulados nos agregados para os respectivos manipuladores.
/// A implementação vive na infraestrutura; a aplicação depende apenas desta abstração.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DespacharAsync(IReadOnlyCollection<IDomainEvent> eventos, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manipulador de um evento de domínio específico. Executa <b>dentro</b> da transação do
/// caso de uso, o que o torna adequado a efeitos que precisam ser atômicos com a operação
/// original (gravar o razão de estoque, por exemplo) — e inadequado a chamadas externas lentas.
/// </summary>
/// <typeparam name="TEvento">Tipo do evento tratado.</typeparam>
public interface IDomainEventHandler<in TEvento>
    where TEvento : IDomainEvent
{
    Task TratarAsync(TEvento evento, CancellationToken cancellationToken = default);
}
