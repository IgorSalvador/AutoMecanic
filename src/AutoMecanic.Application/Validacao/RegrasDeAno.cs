using AutoMecanic.Domain.Abstractions;
using FluentValidation;

namespace AutoMecanic.Application.Validacao;

/// <summary>
/// Limites de ano de fabricação e de modelo de veículo.
/// <para>
/// O limite superior é calculado <b>a cada validação</b>, e não na construção do validador:
/// os validadores são singletons, e um limite fixado na subida da aplicação ficaria preso ao
/// ano em que o processo iniciou — um servidor no ar na virada do ano recusaria os veículos
/// do ano-modelo seguinte.
/// </para>
/// </summary>
internal static class RegrasDeAno
{
    public const int AnoMinimo = 1900;

    /// <summary>Um veículo pode ser fabricado no ano seguinte ao corrente (lançamentos antecipados).</summary>
    public static int AnoMaximoDeFabricacao => RelogioDoDominio.Agora.Year + 1;

    /// <summary>O ano-modelo pode estar até dois anos à frente do ano corrente.</summary>
    public static int AnoMaximoDeModelo => RelogioDoDominio.Agora.Year + 2;

    public static IRuleBuilderOptions<T, int> AnoDeFabricacaoValido<T>(this IRuleBuilder<T, int> regra) =>
        regra
            .Must(ano => ano >= AnoMinimo && ano <= AnoMaximoDeFabricacao)
            .WithMessage(_ => MensagemDeFabricacao());

    public static IRuleBuilderOptions<T, int?> AnoDeFabricacaoValido<T>(this IRuleBuilder<T, int?> regra) =>
        regra
            .Must(ano => ano is null || (ano >= AnoMinimo && ano <= AnoMaximoDeFabricacao))
            .WithMessage(_ => MensagemDeFabricacao());

    public static IRuleBuilderOptions<T, int> AnoDeModeloValido<T>(this IRuleBuilder<T, int> regra) =>
        regra
            .Must(ano => ano >= AnoMinimo && ano <= AnoMaximoDeModelo)
            .WithMessage(_ => $"O ano-modelo deve estar entre {AnoMinimo} e {AnoMaximoDeModelo}.");

    private static string MensagemDeFabricacao() =>
        $"O ano de fabricação deve estar entre {AnoMinimo} e {AnoMaximoDeFabricacao}.";
}
