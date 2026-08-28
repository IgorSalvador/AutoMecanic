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

        consulta = AplicarBusca(consulta, termoDeBusca);

        return await PaginarAsync(consulta.OrderBy(c => c.Nome), paginacao, cancellationToken);
    }

    /// <summary>
    /// Aplica a busca livre.
    /// <para>
    /// O nome é uma coluna de texto comum e aceita <c>ILIKE</c> (busca parcial, sem
    /// distinção de maiúsculas). Documento e e-mail são Objetos de Valor gravados por
    /// conversor: para o banco são colunas de texto, mas para o LINQ são tipos opacos, e o
    /// provedor não consegue traduzir uma comparação parcial sobre eles.
    /// </para>
    /// <para>
    /// A saída é comparar esses dois por <b>igualdade do próprio Objeto de Valor</b>, quando
    /// o termo digitado for um documento ou e-mail válido. Na prática é o comportamento que
    /// o atendente espera: busca-se um CPF inteiro, não um pedaço dele.
    /// </para>
    /// </summary>
    private static IQueryable<Cliente> AplicarBusca(IQueryable<Cliente> consulta, string? termoDeBusca)
    {
        if (PrepararTermo(termoDeBusca) is not { } termo)
        {
            return consulta;
        }

        var achouDocumento = Documento.TentarCriar(termoDeBusca, out var documento);
        var achouEmail = Email.TentarCriar(termoDeBusca, out var email);

        return (achouDocumento, achouEmail) switch
        {
            (true, _) => consulta.Where(c => EF.Functions.ILike(c.Nome, termo) || c.Documento == documento),
            (_, true) => consulta.Where(c => EF.Functions.ILike(c.Nome, termo) || c.Email == email),
            _ => consulta.Where(c => EF.Functions.ILike(c.Nome, termo))
        };
    }
}
