using AutoMecanic.Application.Abstractions;

namespace AutoMecanic.Infrastructure.Servicos;

/// <summary>
/// Relógio da aplicação, sempre em UTC, sobre o <see cref="TimeProvider"/> registrado no
/// contêiner — o mesmo que alimenta o relógio do domínio. Aplicação e domínio enxergam,
/// portanto, um único "agora".
/// <para>
/// Armazenar e comparar tudo em UTC evita a classe de defeitos que aparece no horário de
/// verão e em servidores com fuso diferente do da oficina. A conversão para o fuso local é
/// responsabilidade da camada de apresentação.
/// </para>
/// </summary>
public sealed class ProvedorDeDataHora(TimeProvider provedor) : IProvedorDeDataHora
{
    public DateTimeOffset Agora => provedor.GetUtcNow();
}
