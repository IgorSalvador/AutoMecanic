using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Servicos;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeServicos"/>
public sealed class RepositorioDeServicos(AutoMecanicDbContext contexto)
    : RepositorioBase<Servico>(contexto), IRepositorioDeServicos
{
    public async Task<bool> ExisteComNomeAsync(
        string nome,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizado = (nome ?? string.Empty).Trim();

        return await Conjunto.AnyAsync(
            s => s.Nome.ToLower() == normalizado.ToLower() && (ignorarId == null || s.Id != ignorarId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<Servico>> ObterPorIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await Conjunto
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResultadoPaginado<Servico>> ListarAsync(
        string? termoDeBusca,
        CategoriaServico? categoria,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (categoria is CategoriaServico valor)
        {
            consulta = consulta.Where(s => s.Categoria == valor);
        }

        if (apenasAtivos is bool ativo)
        {
            consulta = consulta.Where(s => s.Ativo == ativo);
        }

        if (PrepararTermo(termoDeBusca) is { } termo)
        {
            consulta = consulta.Where(s =>
                EF.Functions.ILike(s.Nome, termo)
                || (s.Descricao != null && EF.Functions.ILike(s.Descricao, termo)));
        }

        return await PaginarAsync(consulta.OrderBy(s => s.Nome), paginacao, cancellationToken);
    }
}
