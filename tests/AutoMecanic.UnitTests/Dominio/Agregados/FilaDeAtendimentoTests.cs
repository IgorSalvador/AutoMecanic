using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.UnitTests.Dominio.Agregados;

/// <summary>
/// A ordem da fila de trabalho é regra de negócio: o que está na rampa vem antes do que espera
/// o cliente, que vem antes do que ainda está em avaliação ou acabou de chegar.
/// </summary>
public sealed class FilaDeAtendimentoTests
{
    [Fact]
    public void PrioridadeDeAtendimento_SegueAOrdemExigida() =>
        StatusOrdemServicoExtensions.PrioridadeDeAtendimento.ShouldBe(
        [
            StatusOrdemServico.EmExecucao,
            StatusOrdemServico.AguardandoAprovacao,
            StatusOrdemServico.EmDiagnostico,
            StatusOrdemServico.Recebida
        ]);

    [Fact]
    public void ForaDaFila_SaoAsFinalizadasEntreguesECanceladas() =>
        StatusOrdemServicoExtensions.ForaDaFila.ShouldBe(
        [
            StatusOrdemServico.Finalizada,
            StatusOrdemServico.Entregue,
            StatusOrdemServico.Cancelada
        ]);

    [Fact]
    public void FilaEForaDaFila_CobremTodasAsSituacoesSemSobreposicao()
    {
        // Uma situação nova no enum precisa ser posicionada explicitamente em uma das listas.
        var todas = StatusOrdemServicoExtensions.PrioridadeDeAtendimento
            .Concat(StatusOrdemServicoExtensions.ForaDaFila)
            .ToList();

        todas.Distinct().Count().ShouldBe(todas.Count);
        todas.ShouldBe(Enum.GetValues<StatusOrdemServico>(), ignoreOrder: true);
    }
}
