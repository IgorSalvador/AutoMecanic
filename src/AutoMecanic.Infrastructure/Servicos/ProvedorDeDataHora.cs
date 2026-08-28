using AutoMecanic.Application.Abstractions;

namespace AutoMecanic.Infrastructure.Servicos;

/// <summary>
/// Relógio do sistema, sempre em UTC.
/// <para>
/// Armazenar e comparar tudo em UTC evita a classe de defeitos que aparece no horário de
/// verão e em servidores com fuso diferente do da oficina. A conversão para o fuso local é
/// responsabilidade da camada de apresentação.
/// </para>
/// </summary>
public sealed class ProvedorDeDataHora : IProvedorDeDataHora
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}
