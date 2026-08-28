using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.Estoque;

/// <summary>
/// Lançamento no <b>razão de estoque</b> (kardex). É um agregado próprio e <b>imutável</b>:
/// criado uma vez, nunca alterado nem excluído.
/// <para>
/// Existe separado de <see cref="Peca"/> porque a coleção de lançamentos cresce
/// indefinidamente — mantê-la dentro do agregado obrigaria a carregar todo o histórico
/// para movimentar uma única peça. Cada lançamento é gerado a partir do evento de domínio
/// <see cref="Events.EstoqueMovimentado"/>, o que garante que saldo e histórico nunca divirjam.
/// </para>
/// </summary>
public sealed class MovimentoEstoque : AggregateRoot
{
    private MovimentoEstoque()
    {
        Motivo = null!;
    }

    private MovimentoEstoque(
        Guid id,
        Guid pecaId,
        TipoMovimentoEstoque tipo,
        int quantidade,
        int saldoAnterior,
        int saldoAtual,
        string motivo,
        Guid? ordemServicoId,
        DateTimeOffset ocorridoEm)
        : base(id)
    {
        PecaId = pecaId;
        Tipo = tipo;
        Quantidade = quantidade;
        SaldoAnterior = saldoAnterior;
        SaldoAtual = saldoAtual;
        Motivo = motivo;
        OrdemServicoId = ordemServicoId;
        OcorridoEm = ocorridoEm;
    }

    public Guid PecaId { get; private set; }

    public TipoMovimentoEstoque Tipo { get; private set; }

    /// <summary>Quantidade movimentada, sempre positiva. O sinal é dado por <see cref="Tipo"/>.</summary>
    public int Quantidade { get; private set; }

    public int SaldoAnterior { get; private set; }

    public int SaldoAtual { get; private set; }

    /// <summary>Justificativa do lançamento — exigência de auditoria.</summary>
    public string Motivo { get; private set; }

    /// <summary>Ordem de Serviço que originou o movimento, quando houver.</summary>
    public Guid? OrdemServicoId { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }

    public static MovimentoEstoque Registrar(
        Guid pecaId,
        TipoMovimentoEstoque tipo,
        int quantidade,
        int saldoAnterior,
        int saldoAtual,
        string motivo,
        Guid? ordemServicoId,
        DateTimeOffset ocorridoEm) =>
        new(NovoId(), pecaId, tipo, quantidade, saldoAnterior, saldoAtual, motivo, ordemServicoId, ocorridoEm);
}
