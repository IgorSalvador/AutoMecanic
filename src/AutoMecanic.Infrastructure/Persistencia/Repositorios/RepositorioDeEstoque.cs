using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Estoque;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDePecas"/>
public sealed class RepositorioDePecas(AutoMecanicDbContext contexto)
    : RepositorioBase<Peca>(contexto), IRepositorioDePecas
{
    public async Task<Peca?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var normalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();

        return await Conjunto.FirstOrDefaultAsync(p => p.Codigo == normalizado, cancellationToken);
    }

    public async Task<bool> ExisteComCodigoAsync(
        string codigo,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();

        return await Conjunto.AnyAsync(
            p => p.Codigo == normalizado && (ignorarId == null || p.Id != ignorarId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<Peca>> ObterPorIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        // Carga em lote: uma consulta para N peças, em vez de N consultas dentro do laço
        // que consome as reservas de uma OS.
        return await Conjunto
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Peca>> ListarAbaixoDoEstoqueMinimoAsync(CancellationToken cancellationToken = default) =>
        await Conjunto
            .AsNoTracking()
            .Where(p => p.Ativo && p.QuantidadeEmEstoque - p.QuantidadeReservada <= p.EstoqueMinimo)
            .OrderBy(p => p.QuantidadeEmEstoque - p.QuantidadeReservada)
            .ThenBy(p => p.Nome)
            .ToListAsync(cancellationToken);

    public async Task<ResultadoPaginado<Peca>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivas,
        bool? apenasAbaixoDoMinimo,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (apenasAtivas is bool ativa)
        {
            consulta = consulta.Where(p => p.Ativo == ativa);
        }

        if (apenasAbaixoDoMinimo == true)
        {
            // QuantidadeDisponivel é calculada em memória e não existe como coluna: a mesma
            // regra é reescrita aqui em termos das colunas persistidas, para ser traduzida
            // em SQL e filtrar no banco.
            consulta = consulta.Where(p => p.QuantidadeEmEstoque - p.QuantidadeReservada <= p.EstoqueMinimo);
        }

        if (PrepararTermo(termoDeBusca) is { } termo)
        {
            consulta = consulta.Where(p =>
                EF.Functions.ILike(p.Codigo, termo)
                || EF.Functions.ILike(p.Nome, termo)
                || (p.Descricao != null && EF.Functions.ILike(p.Descricao, termo)));
        }

        return await PaginarAsync(consulta.OrderBy(p => p.Nome), paginacao, cancellationToken);
    }
}

/// <inheritdoc cref="IRepositorioDeMovimentosDeEstoque"/>
public sealed class RepositorioDeMovimentosDeEstoque(AutoMecanicDbContext contexto)
    : RepositorioBase<MovimentoEstoque>(contexto), IRepositorioDeMovimentosDeEstoque
{
    public async Task<ResultadoPaginado<MovimentoEstoque>> ListarAsync(
        Guid? pecaId,
        Guid? ordemServicoId,
        TipoMovimentoEstoque? tipo,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (pecaId is Guid peca)
        {
            consulta = consulta.Where(m => m.PecaId == peca);
        }

        if (ordemServicoId is Guid ordem)
        {
            consulta = consulta.Where(m => m.OrdemServicoId == ordem);
        }

        if (tipo is TipoMovimentoEstoque valor)
        {
            consulta = consulta.Where(m => m.Tipo == valor);
        }

        if (de is DateTimeOffset inicio)
        {
            consulta = consulta.Where(m => m.OcorridoEm >= inicio);
        }

        if (ate is DateTimeOffset fim)
        {
            consulta = consulta.Where(m => m.OcorridoEm <= fim);
        }

        // Extrato do mais recente para o mais antigo, que é como o almoxarife lê.
        return await PaginarAsync(
            consulta.OrderByDescending(m => m.OcorridoEm),
            paginacao,
            cancellationToken);
    }
}
