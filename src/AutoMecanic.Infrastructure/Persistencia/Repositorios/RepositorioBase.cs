using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Implementação comum de repositório sobre o EF Core.
/// <para>
/// Os métodos de escrita apenas registram a intenção no rastreador de mudanças; nada vai ao
/// banco até o commit da Unidade de Trabalho. É isso que permite que um caso de uso altere
/// vários agregados e tudo seja gravado atomicamente.
/// </para>
/// </summary>
/// <typeparam name="TAgregado">Raiz do agregado gerenciado.</typeparam>
public abstract class RepositorioBase<TAgregado>(AutoMecanicDbContext contexto) : IRepositorio<TAgregado>
    where TAgregado : class
{
    /// <summary>Contexto de persistência compartilhado com a Unidade de Trabalho.</summary>
    protected AutoMecanicDbContext Contexto { get; } = contexto;

    /// <summary>Conjunto rastreado do agregado.</summary>
    protected DbSet<TAgregado> Conjunto => Contexto.Set<TAgregado>();

    public virtual async Task<TAgregado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Conjunto.FindAsync([id], cancellationToken);

    public virtual async Task AdicionarAsync(TAgregado agregado, CancellationToken cancellationToken = default) =>
        await Conjunto.AddAsync(agregado, cancellationToken);

    /// <summary>
    /// Marca o agregado como alterado.
    /// <para>
    /// Quando ele já está sendo rastreado — o caso normal, porque o caso de uso o carregou
    /// pelo próprio repositório — nada é feito: o rastreador de mudanças do EF Core já
    /// detecta as alterações no commit. Chamar <c>Update</c> aqui marcaria <b>todas</b> as
    /// propriedades como modificadas, inclusive as das entidades filhas, produzindo UPDATEs
    /// desnecessários e interferindo no token de concorrência.
    /// </para>
    /// <para>
    /// O <c>Update</c> só é necessário para um agregado desanexado, vindo de fora do contexto.
    /// </para>
    /// </summary>
    public virtual void Atualizar(TAgregado agregado)
    {
        if (Contexto.Entry(agregado).State == EntityState.Detached)
        {
            Conjunto.Update(agregado);
        }
    }

    public virtual void Remover(TAgregado agregado) => Conjunto.Remove(agregado);

    /// <summary>
    /// Materializa uma consulta em página de resultados, executando a contagem e a fatia em
    /// duas consultas — o suficiente para o volume de uma oficina, e mais simples de ler
    /// do que uma janela agregada.
    /// </summary>
    protected static async Task<ResultadoPaginado<TItem>> PaginarAsync<TItem>(
        IQueryable<TItem> consulta,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken)
    {
        var total = await consulta.CountAsync(cancellationToken);

        if (total == 0)
        {
            return ResultadoPaginado<TItem>.Vazio(paginacao);
        }

        var itens = await consulta
            .Skip(paginacao.Deslocamento)
            .Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);

        return ResultadoPaginado<TItem>.Criar(itens, total, paginacao);
    }

    /// <summary>Normaliza o termo de busca para uso com <c>ILIKE</c>, ou devolve nulo se vazio.</summary>
    protected static string? PrepararTermo(string? termo) =>
        string.IsNullOrWhiteSpace(termo) ? null : $"%{termo.Trim()}%";
}
