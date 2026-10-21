using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;

/// <summary>
/// Efeitos da decisão do cliente sobre o orçamento, iguais por qualquer canal — o atendente
/// registrando a resposta recebida por telefone ou o próprio cliente pela consulta pública.
/// <list type="bullet">
///   <item>Aprovar: a OS entra em execução e as peças reservadas são baixadas do estoque.</item>
///   <item>Reprovar: a OS é cancelada e as reservas voltam ao saldo disponível.</item>
/// </list>
/// Deve ser chamada dentro da transação do caso de uso.
/// </summary>
public sealed class DecisaoDoOrcamento(ReservasDaOrdemServico reservas)
{
    public async Task AprovarAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        ordem.AprovarOrcamento();

        await reservas.ConsumirAsync(ordem, cancellationToken);
    }

    public async Task ReprovarAsync(OrdemServico ordem, string? motivo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        ordem.ReprovarOrcamento(motivo);

        await reservas.LiberarAsync(ordem, cancellationToken);
    }
}
