using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Domain.Identidade;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.Identidade;

/// <summary>Gestão dos usuários administrativos do sistema.</summary>
public interface IServicoDeUsuarios
{
    Task<UsuarioResponse> CriarAsync(CriarUsuarioRequest requisicao, CancellationToken cancellationToken = default);

    Task<UsuarioResponse> AtualizarAsync(Guid id, AtualizarUsuarioRequest requisicao, CancellationToken cancellationToken = default);

    Task<UsuarioResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<UsuarioResponse>> ListarAsync(
        string? termoDeBusca,
        PerfilUsuario? perfil,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    /// <summary>Troca de senha pelo próprio usuário, exigindo a senha atual.</summary>
    Task AlterarSenhaAsync(Guid id, AlterarSenhaRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Redefinição administrativa, sem exigir a senha anterior.</summary>
    Task RedefinirSenhaAsync(Guid id, RedefinirSenhaRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Libera manualmente uma conta bloqueada por tentativas malsucedidas.</summary>
    Task DesbloquearAsync(Guid id, CancellationToken cancellationToken = default);

    Task InativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IServicoDeUsuarios"/>
public sealed class ServicoDeUsuarios(
    IRepositorioDeUsuarios repositorio,
    IServicoDeHashDeSenha hasher,
    IUsuarioAtual usuarioAtual,
    IUnitOfWork unitOfWork,
    ILogger<ServicoDeUsuarios> logger) : IServicoDeUsuarios
{
    public async Task<UsuarioResponse> CriarAsync(
        CriarUsuarioRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var email = (requisicao.Email ?? string.Empty).Trim().ToLowerInvariant();

        if (await repositorio.ExisteComEmailAsync(email, cancellationToken: cancellationToken))
        {
            throw new ConflitoException("EMAIL_DUPLICADO", $"Já existe um usuário cadastrado com o e-mail '{email}'.");
        }

        var usuario = Usuario.Criar(
            requisicao.Nome,
            requisicao.Email,
            requisicao.Senha,
            requisicao.Perfil,
            hasher.GerarHash);

        await repositorio.AdicionarAsync(usuario, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Usuário {UsuarioId} criado com perfil {Perfil}.", usuario.Id, usuario.Perfil);

        return UsuarioResponse.De(usuario);
    }

    public async Task<UsuarioResponse> AtualizarAsync(
        Guid id,
        AtualizarUsuarioRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var usuario = await ExigirUsuarioAsync(id, cancellationToken);

        // Um administrador não pode rebaixar o próprio perfil: isso poderia deixar o
        // sistema sem ninguém capaz de gerenciar usuários.
        if (usuarioAtual.Id == id
            && usuario.Perfil == PerfilUsuario.Administrador
            && requisicao.Perfil != PerfilUsuario.Administrador)
        {
            throw new ConflitoException(
                "AUTO_REBAIXAMENTO",
                "Um administrador não pode remover o próprio perfil de administrador.");
        }

        usuario.AtualizarNome(requisicao.Nome);
        usuario.AlterarPerfil(requisicao.Perfil);

        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return UsuarioResponse.De(usuario);
    }

    public async Task<UsuarioResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        UsuarioResponse.De(await ExigirUsuarioAsync(id, cancellationToken));

    public async Task<ResultadoPaginado<UsuarioResponse>> ListarAsync(
        string? termoDeBusca,
        PerfilUsuario? perfil,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorio.ListarAsync(termoDeBusca, perfil, apenasAtivos, paginacao, cancellationToken);

        return ResultadoPaginado<UsuarioResponse>.Criar(
            [.. pagina.Itens.Select(UsuarioResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }

    public async Task AlterarSenhaAsync(
        Guid id,
        AlterarSenhaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var usuario = await ExigirUsuarioAsync(id, cancellationToken);

        usuario.AlterarSenha(requisicao.SenhaAtual, requisicao.NovaSenha, hasher.Verificar, hasher.GerarHash);

        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Senha do usuário {UsuarioId} alterada pelo próprio usuário.", usuario.Id);
    }

    public async Task RedefinirSenhaAsync(
        Guid id,
        RedefinirSenhaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var usuario = await ExigirUsuarioAsync(id, cancellationToken);

        usuario.RedefinirSenha(requisicao.NovaSenha, hasher.GerarHash);

        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogWarning("Senha do usuário {UsuarioId} redefinida por {AdministradorId}.", usuario.Id, usuarioAtual.Id);
    }

    public async Task DesbloquearAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await ExigirUsuarioAsync(id, cancellationToken);

        usuario.Desbloquear();

        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    public async Task InativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (usuarioAtual.Id == id)
        {
            throw new ConflitoException("AUTO_INATIVACAO", "Um usuário não pode inativar a própria conta.");
        }

        var usuario = await ExigirUsuarioAsync(id, cancellationToken);

        usuario.Inativar();

        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await ExigirUsuarioAsync(id, cancellationToken);

        usuario.Reativar();

        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    private async Task<Usuario> ExigirUsuarioAsync(Guid id, CancellationToken cancellationToken) =>
        await repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Usuário", id);
}
