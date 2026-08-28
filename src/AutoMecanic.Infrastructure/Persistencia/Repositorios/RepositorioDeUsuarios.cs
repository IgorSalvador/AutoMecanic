using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeUsuarios"/>
public sealed class RepositorioDeUsuarios(AutoMecanicDbContext contexto)
    : RepositorioBase<Usuario>(contexto), IRepositorioDeUsuarios
{
    public async Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizado = (email ?? string.Empty).Trim().ToLowerInvariant();

        return await Conjunto.FirstOrDefaultAsync(
            u => EF.Property<string>(u, "email") == normalizado,
            cancellationToken);
    }

    public async Task<bool> ExisteComEmailAsync(
        string email,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizado = (email ?? string.Empty).Trim().ToLowerInvariant();

        return await Conjunto.AnyAsync(
            u => EF.Property<string>(u, "email") == normalizado && (ignorarId == null || u.Id != ignorarId),
            cancellationToken);
    }

    public async Task<bool> ExisteAlgumUsuarioAsync(CancellationToken cancellationToken = default) =>
        await Conjunto.AnyAsync(cancellationToken);

    public async Task<ResultadoPaginado<Usuario>> ListarAsync(
        string? termoDeBusca,
        PerfilUsuario? perfil,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (perfil is PerfilUsuario valor)
        {
            consulta = consulta.Where(u => u.Perfil == valor);
        }

        if (apenasAtivos is bool ativo)
        {
            consulta = consulta.Where(u => u.Ativo == ativo);
        }

        if (PrepararTermo(termoDeBusca) is { } termo)
        {
            consulta = consulta.Where(u =>
                EF.Functions.ILike(u.Nome, termo)
                || EF.Functions.ILike(EF.Property<string>(u, "email"), termo));
        }

        return await PaginarAsync(consulta.OrderBy(u => u.Nome), paginacao, cancellationToken);
    }
}
