using AutoMecanic.Domain.Abstractions;

namespace AutoMecanic.Domain.OrdensServico;

/// <summary>
/// <b>Entidade filha</b> imutável que registra uma transição de status da Ordem de Serviço.
/// <para>
/// É a linha do tempo que o cliente consulta pela API pública ("meu carro está em qual
/// etapa?") e a base de cálculo do indicador de tempo médio de execução exigido pela gestão.
/// </para>
/// </summary>
public sealed class HistoricoStatus : Entity
{
    private HistoricoStatus()
    {
    }

    private HistoricoStatus(
        Guid id,
        Guid ordemServicoId,
        StatusOrdemServico? statusAnterior,
        StatusOrdemServico statusAtual,
        string? observacao,
        Guid? usuarioId,
        DateTimeOffset ocorridoEm)
        : base(id)
    {
        OrdemServicoId = ordemServicoId;
        StatusAnterior = statusAnterior;
        StatusAtual = statusAtual;
        Observacao = observacao;
        UsuarioId = usuarioId;
        OcorridoEm = ocorridoEm;
    }

    public Guid OrdemServicoId { get; private set; }

    /// <summary>Nulo apenas no registro de abertura da OS.</summary>
    public StatusOrdemServico? StatusAnterior { get; private set; }

    public StatusOrdemServico StatusAtual { get; private set; }

    public string? Observacao { get; private set; }

    /// <summary>Usuário que provocou a transição. Nulo quando a ação partiu do cliente.</summary>
    public Guid? UsuarioId { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }

    internal static HistoricoStatus Registrar(
        Guid ordemServicoId,
        StatusOrdemServico? statusAnterior,
        StatusOrdemServico statusAtual,
        string? observacao,
        Guid? usuarioId) =>
        new(NovoId(),
            ordemServicoId,
            statusAnterior,
            statusAtual,
            string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            usuarioId,
            DateTimeOffset.UtcNow);
}
