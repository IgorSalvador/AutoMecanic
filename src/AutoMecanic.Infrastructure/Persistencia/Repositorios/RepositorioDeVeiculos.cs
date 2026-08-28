using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Domain.Veiculos.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeVeiculos"/>
public sealed class RepositorioDeVeiculos(AutoMecanicDbContext contexto)
    : RepositorioBase<Veiculo>(contexto), IRepositorioDeVeiculos
{
    public async Task<Veiculo?> ObterPorPlacaAsync(Placa placa, CancellationToken cancellationToken = default) =>
        await Conjunto.FirstOrDefaultAsync(v => v.Placa == placa, cancellationToken);

    public async Task<bool> ExisteComPlacaAsync(
        Placa placa,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default) =>
        await Conjunto.AnyAsync(
            v => v.Placa == placa && (ignorarId == null || v.Id != ignorarId),
            cancellationToken);

    public async Task<IReadOnlyList<Veiculo>> ListarPorClienteAsync(
        Guid clienteId,
        CancellationToken cancellationToken = default) =>
        await Conjunto
            .AsNoTracking()
            .Where(v => v.ClienteId == clienteId)
            .OrderBy(v => v.Marca)
            .ThenBy(v => v.Modelo)
            .ToListAsync(cancellationToken);

    public async Task<ResultadoPaginado<Veiculo>> ListarAsync(
        string? termoDeBusca,
        Guid? clienteId,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var consulta = Conjunto.AsNoTracking();

        if (clienteId is Guid id)
        {
            consulta = consulta.Where(v => v.ClienteId == id);
        }

        if (apenasAtivos is bool ativo)
        {
            consulta = consulta.Where(v => v.Ativo == ativo);
        }

        if (PrepararTermo(termoDeBusca) is { } termo)
        {
            consulta = consulta.Where(v =>
                EF.Functions.ILike(EF.Property<string>(v, "placa"), termo)
                || EF.Functions.ILike(v.Marca, termo)
                || EF.Functions.ILike(v.Modelo, termo));
        }

        return await PaginarAsync(
            consulta.OrderBy(v => v.Marca).ThenBy(v => v.Modelo),
            paginacao,
            cancellationToken);
    }
}
