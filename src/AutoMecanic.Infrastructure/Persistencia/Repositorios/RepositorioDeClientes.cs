using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeClientes"/>
public sealed class RepositorioDeClientes(AutoMecanicDbContext contexto)
    : RepositorioBase<Cliente>(contexto), IRepositorioDeClientes
{
    public async Task<Cliente?> ObterPorDocumentoAsync(
        Documento documento,
        CancellationToken cancellationToken = default) =>
        await Conjunto.FirstOrDefaultAsync(c => c.Documento == documento, cancellationToken);

    public async Task<bool> ExisteComDocumentoAsync(
        Documento documento,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default) =>
        await Conjunto.AnyAsync(
            c => c.Documento == documento && (ignorarId == null || c.Id != ignorarId),
            cancellationToken);

    public async Task<ResultadoPaginado<Cliente>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        // AsNoTracking em listagens: os objetos são projetados para DTO e descartados,
        // então rastreá-los só consumiria memória e tempo.
        var consulta = Conjunto.AsNoTracking();

        if (apenasAtivos is bool ativo)
        {
            consulta = consulta.Where(c => c.Ativo == ativo);
        }

        if (PrepararTermo(termoDeBusca) is { } termo)
        {
            // ILIKE é a busca sem distinção de maiúsculas do PostgreSQL; o termo é
            // parametrizado pelo EF, portanto não há superfície para injeção de SQL.
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.Nome, termo)
                || EF.Functions.ILike(EF.Property<string>(c, "documento"), termo)
                || EF.Functions.ILike(EF.Property<string>(c, "email"), termo));
        }

        return await PaginarAsync(consulta.OrderBy(c => c.Nome), paginacao, cancellationToken);
    }
}
