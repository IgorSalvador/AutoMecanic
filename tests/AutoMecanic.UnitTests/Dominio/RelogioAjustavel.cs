namespace AutoMecanic.UnitTests.Dominio;

/// <summary>
/// Relógio controlado pelo teste: parado em um instante conhecido e avançado à mão. Usado com
/// <c>RelogioDoDominio.Congelar</c> para verificar durações e validades sem depender da hora
/// em que o teste roda.
/// </summary>
internal sealed class RelogioAjustavel(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Agora { get; private set; } = inicio;

    public override DateTimeOffset GetUtcNow() => Agora;

    public void Avancar(TimeSpan intervalo) => Agora = Agora.Add(intervalo);
}
