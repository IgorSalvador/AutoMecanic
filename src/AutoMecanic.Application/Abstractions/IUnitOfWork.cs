namespace AutoMecanic.Application.Abstractions;

/// <summary>
/// <b>Unidade de Trabalho</b>: define a fronteira transacional de um caso de uso.
/// <para>
/// Todas as alterações feitas nos agregados durante uma operação são persistidas em uma
/// única transação. Antes do commit, os eventos de domínio acumulados nos agregados são
/// despachados, de modo que os efeitos colaterais (razão de estoque, alertas) participem
/// da mesma transação — não existe estado meio salvo.
/// </para>
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Despacha os eventos de domínio pendentes e persiste todas as alterações.
    /// </summary>
    /// <returns>Quantidade de registros afetados.</returns>
    Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa a operação dentro de uma transação explícita, com rollback automático em caso
    /// de exceção. Usado nos casos de uso que tocam mais de um agregado — por exemplo, aprovar
    /// um orçamento (Ordem de Serviço) consumindo peças (Estoque).
    /// </summary>
    Task<TResultado> ExecutarEmTransacaoAsync<TResultado>(
        Func<CancellationToken, Task<TResultado>> operacao,
        CancellationToken cancellationToken = default);
}
