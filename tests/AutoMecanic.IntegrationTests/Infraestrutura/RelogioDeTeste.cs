namespace AutoMecanic.IntegrationTests.Infraestrutura;

/// <summary>
/// Relógio real deslocado por um intervalo ajustável. Começa sem deslocamento, de modo que os
/// demais testes da coleção não percebem diferença; quem adiantar deve restaurar ao final.
/// </summary>
public sealed class RelogioDeTeste : TimeProvider
{
    private TimeSpan _deslocamento = TimeSpan.Zero;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow().Add(_deslocamento);

    /// <summary>Adianta o relógio e devolve um objeto que o restaura ao ser descartado.</summary>
    public IDisposable Adiantar(TimeSpan intervalo)
    {
        _deslocamento += intervalo;

        return new Restauracao(this, intervalo);
    }

    private sealed class Restauracao(RelogioDeTeste relogio, TimeSpan intervalo) : IDisposable
    {
        public void Dispose() => relogio._deslocamento -= intervalo;
    }
}
