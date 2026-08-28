using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Identidade;
using Microsoft.EntityFrameworkCore;

namespace AutoMecanic.Infrastructure.Persistencia.Repositorios;

/// <inheritdoc cref="IRepositorioDeUsuarios"/>
public sealed class RepositorioDeUsuarios(AutoMecanicDbContext contexto)
    : RepositorioBase<Usuario>(contexto), IRepositorioDeUsuarios
{
    public async Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Um e-mail sintaticamente inválido não pode existir na base — o Objeto de Valor
        // impede isso na escrita. Retornar nulo sem consultar evita uma ida ao banco e
        // mantém a resposta de login idêntica à de "senha incorreta".
        if (!Email.TentarCriar(email, out var enderecoValido))
        {
            return null;
        }

        return await Conjunto.FirstOrDefaultAsync(u => u.Email == enderecoValido, cancellationToken);
    }

    public async Task<bool> ExisteComEmailAsync(
        string email,
        Guid? ignorarId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Email.TentarCriar(email, out var enderecoValido))
        {
            return false;
        }

        return await Conjunto.AnyAsync(
            u => u.Email == enderecoValido && (ignorarId == null || u.Id != ignorarId),
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
            consulta = Email.TentarCriar(termoDeBusca, out var email)
                ? consulta.Where(u => EF.Functions.ILike(u.Nome, termo) || u.Email == email)
                : consulta.Where(u => EF.Functions.ILike(u.Nome, termo));
        }

        return await PaginarAsync(consulta.OrderBy(u => u.Nome), paginacao, cancellationToken);
    }
}
