using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
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

    public async Task<OrdemServico?> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        // O número é Objeto de Valor gravado por conversor: a comparação é feita sobre o
        // próprio VO, e o provedor traduz para a coluna de texto correspondente.
        var numeroValidado = NumeroOrdemServico.Analisar(numero);

        return await Conjunto.FirstOrDefaultAsync(o => o.Numero == numeroValidado, cancellationToken);
    }

    public async Task<ResultadoPaginado<OrdemServico>> ListarAsync(
        StatusOrdemServico? status,
        Guid? clienteId,
        Guid? veiculoId,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (status is StatusOrdemServico valor)
        {
            consulta = consulta.Where(o => o.Status == valor);
        }

        if (clienteId is Guid cliente)
        {
            consulta = consulta.Where(o => o.ClienteId == cliente);
        }

        if (veiculoId is Guid veiculo)
        {
            consulta = consulta.Where(o => o.VeiculoId == veiculo);
        }

        if (de is DateTimeOffset inicio)
        {
            consulta = consulta.Where(o => o.CriadaEm >= inicio);
        }

        if (ate is DateTimeOffset fim)
        {
            consulta = consulta.Where(o => o.CriadaEm <= fim);
        }

        return await PaginarAsync(
            consulta.OrderByDescending(o => o.CriadaEm),
            paginacao,
            cancellationToken);
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
