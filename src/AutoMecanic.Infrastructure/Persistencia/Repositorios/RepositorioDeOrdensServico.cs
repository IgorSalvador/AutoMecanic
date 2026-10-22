using System.Linq.Expressions;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeOrdensServico"/>
/// <remarks>
/// Itens, orçamento e histórico são navegações pertencentes ao agregado e, por isso, o EF
/// Core sempre os carrega junto com a Ordem de Serviço — não é preciso (nem possível esquecer)
/// um <c>Include</c> explícito. O agregado nunca é carregado pela metade.
/// </remarks>
public sealed class RepositorioDeOrdensServico(AutoMecanicDbContext contexto)
    : RepositorioBase<OrdemServico>(contexto), IRepositorioDeOrdensServico
{
    public async Task<OrdemServico?> ObterCompletaPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Conjunto.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<StatusDaOrdemServicoResponse?> ObterStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Projeção direta: uma consulta com subconsulta ao histórico, sem materializar o
        // agregado nem suas coleções.
        var situacao = await Conjunto
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.Id,
                o.Numero,
                o.Status,
                UltimaTransicao = EF.Property<IEnumerable<HistoricoStatus>>(o, "_historico")
                    .Max(h => (DateTimeOffset?)h.OcorridoEm),
                o.CriadaEm
            })
            .FirstOrDefaultAsync(cancellationToken);

        return situacao is null
            ? null
            : StatusDaOrdemServicoResponse.De(
                situacao.Id,
                situacao.Numero.Valor,
                situacao.Status,
                situacao.UltimaTransicao ?? situacao.CriadaEm);
    }

    public async Task<OrdemServico?> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        // O número é Objeto de Valor gravado por conversor: a comparação é feita sobre o
        // próprio VO, e o provedor traduz para a coluna de texto correspondente.
        var numeroValidado = NumeroOrdemServico.Analisar(numero);

        return await Conjunto.FirstOrDefaultAsync(o => o.Numero == numeroValidado, cancellationToken);
    }

    public async Task<ResultadoPaginado<OrdemServicoResumoResponse>> ListarAsync(
        FiltroDeOrdensServico filtro,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacao);

        var consulta = Filtrar(Conjunto.AsNoTracking(), filtro);

        // Projeção: só as colunas da listagem e os valores dos itens, sem histórico. A consulta
        // dividida evita o produto cartesiano entre itens de serviço e de peça.
        var projetada = consulta
            .OrderBy(PrioridadeDeAtendimento)
            .ThenBy(o => o.CriadaEm)
            .ThenBy(o => o.Id)
            .AsSplitQuery()
            .Select(o => new
            {
                o.Id,
                o.Numero,
                o.Status,
                o.ClienteId,
                o.VeiculoId,
                o.CriadaEm,
                o.EntregueEm,
                ValorDoOrcamento = o.Orcamento != null ? o.Orcamento.ValorTotal : null,
                Servicos = EF.Property<IEnumerable<ItemServico>>(o, "_itensServico")
                    .Select(i => new { i.PrecoUnitario, i.Quantidade })
                    .ToList(),
                Pecas = EF.Property<IEnumerable<ItemPeca>>(o, "_itensPeca")
                    .Select(i => new { i.PrecoUnitario, i.Quantidade })
                    .ToList()
            });

        var pagina = await PaginarAsync(projetada, paginacao, cancellationToken);

        var itens = pagina.Itens
            .Select(linha => new OrdemServicoResumoResponse(
                linha.Id,
                linha.Numero.Valor,
                linha.Status,
                linha.Status.Descricao(),
                linha.ClienteId,
                linha.VeiculoId,
                // Sem orçamento, o valor de referência é a soma dos itens já incluídos.
                linha.ValorDoOrcamento?.Valor
                    ?? linha.Servicos.Sum(i => i.PrecoUnitario.Valor * i.Quantidade)
                    + linha.Pecas.Sum(i => i.PrecoUnitario.Valor * i.Quantidade),
                linha.CriadaEm,
                linha.EntregueEm))
            .ToList();

        return ResultadoPaginado<OrdemServicoResumoResponse>.Criar(itens, pagina.TotalDeItens, paginacao);
    }

    private static IQueryable<OrdemServico> Filtrar(IQueryable<OrdemServico> consulta, FiltroDeOrdensServico filtro)
    {
        if (filtro.Status is StatusOrdemServico status)
        {
            // Pedir uma situação explicitamente vale mesmo para as encerradas.
            consulta = consulta.Where(o => o.Status == status);
        }
        else if (!filtro.IncluirEncerradas)
        {
            foreach (var encerrada in StatusOrdemServicoExtensions.ForaDaFila)
            {
                consulta = consulta.Where(o => o.Status != encerrada);
            }
        }

        if (filtro.ClienteId is Guid cliente)
        {
            consulta = consulta.Where(o => o.ClienteId == cliente);
        }

        if (filtro.VeiculoId is Guid veiculo)
        {
            consulta = consulta.Where(o => o.VeiculoId == veiculo);
        }

        if (filtro.De is DateTimeOffset inicio)
        {
            consulta = consulta.Where(o => o.CriadaEm >= inicio);
        }

        if (filtro.Ate is DateTimeOffset fim)
        {
            consulta = consulta.Where(o => o.CriadaEm <= fim);
        }

        return consulta;
    }

    /// <summary>
    /// Posição da situação na fila, traduzida para um <c>CASE</c> no banco. A regra de negócio
    /// vive no domínio (<see cref="StatusOrdemServicoExtensions.PrioridadeDeAtendimento"/>); aqui
    /// ela só é convertida em expressão, para que a ordenação e a paginação aconteçam no SQL.
    /// Situações fora da lista de prioridade vêm depois, na ordem do ciclo de vida.
    /// </summary>
    private static readonly Expression<Func<OrdemServico, int>> PrioridadeDeAtendimento = MontarPrioridade();

    private static Expression<Func<OrdemServico, int>> MontarPrioridade()
    {
        var ordem = Expression.Parameter(typeof(OrdemServico), "o");
        var status = Expression.Convert(Expression.Property(ordem, nameof(OrdemServico.Status)), typeof(int));

        var sequencia = StatusOrdemServicoExtensions.PrioridadeDeAtendimento
            .Concat(Enum.GetValues<StatusOrdemServico>().Except(StatusOrdemServicoExtensions.PrioridadeDeAtendimento))
            .ToList();

        Expression posicao = Expression.Constant(sequencia.Count);

        for (var indice = sequencia.Count - 1; indice >= 0; indice--)
        {
            posicao = Expression.Condition(
                Expression.Equal(status, Expression.Constant((int)sequencia[indice])),
                Expression.Constant(indice),
                posicao);
        }

        return Expression.Lambda<Func<OrdemServico, int>>(posicao, ordem);
    }

    public async Task<IReadOnlyList<OrdemServico>> ListarFinalizadasNoPeriodoAsync(
        DateTimeOffset de,
        DateTimeOffset ate,
        CancellationToken cancellationToken = default) =>
        await Conjunto
            .AsNoTracking()
            .Where(o => o.FinalizadaEm != null
                        && o.FinalizadaEm >= de
                        && o.FinalizadaEm <= ate
                        && o.ExecucaoIniciadaEm != null)
            .OrderBy(o => o.FinalizadaEm)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OrdemServico>> ListarComOrcamentoVencidoAsync(
        DateTimeOffset referencia,
        CancellationToken cancellationToken = default) =>
        // Rastreadas de propósito: estas ordens serão alteradas (expiradas) em seguida.
        await Conjunto
            .Where(o => o.Status == StatusOrdemServico.AguardandoAprovacao
                        && o.Orcamento != null
                        && o.Orcamento.ValidoAte != null
                        && o.Orcamento.ValidoAte < referencia)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<StatusOrdemServico, int>> ContarPorStatusAsync(
        CancellationToken cancellationToken = default)
    {
        // Agregação feita no banco: uma consulta com GROUP BY, sem trazer as ordens à memória.
        var contagens = await Conjunto
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(grupo => new { Status = grupo.Key, Total = grupo.Count() })
            .ToListAsync(cancellationToken);

        return contagens.ToDictionary(item => item.Status, item => item.Total);
    }
}
