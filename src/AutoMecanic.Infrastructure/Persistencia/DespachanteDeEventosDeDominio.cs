using System.Collections.Concurrent;
using System.Reflection;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Infrastructure.Persistencia;

/// <summary>
/// Despacha eventos de domínio para todos os <see cref="IDomainEventHandler{TEvento}"/>
/// registrados no contêiner.
/// <para>
/// A resolução do tipo genérico usa reflexão, cujo resultado é memorizado por tipo de evento:
/// o custo é pago uma vez no primeiro despacho, e não a cada requisição.
/// </para>
/// </summary>
public sealed class DespachanteDeEventosDeDominio(
    IServiceProvider provedor,
    ILogger<DespachanteDeEventosDeDominio> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, (Type TipoDoServico, MethodInfo Metodo)> CacheDeManipuladores = new();

    public async Task DespacharAsync(
        IReadOnlyCollection<IDomainEvent> eventos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventos);

        foreach (var evento in eventos)
        {
            var (tipoDoServico, metodo) = CacheDeManipuladores.GetOrAdd(evento.GetType(), tipoDoEvento =>
            {
                var contrato = typeof(IDomainEventHandler<>).MakeGenericType(tipoDoEvento);

                return (
                    typeof(IEnumerable<>).MakeGenericType(contrato),
                    contrato.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.TratarAsync))!);
            });

            if (provedor.GetService(tipoDoServico) is not IEnumerable<object> manipuladores)
            {
                continue;
            }

            foreach (var manipulador in manipuladores)
            {
                logger.LogDebug(
                    "Despachando {Evento} para {Manipulador}.",
                    evento.GetType().Name,
                    manipulador.GetType().Name);

                await (Task)metodo.Invoke(manipulador, [evento, cancellationToken])!;
            }
        }
    }
}
