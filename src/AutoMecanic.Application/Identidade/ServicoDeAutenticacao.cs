using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.Identidade;

/// <summary>Autenticação dos usuários administrativos.</summary>
public interface IServicoDeAutenticacao
{
    /// <summary>Valida as credenciais e emite um JWT.</summary>
    /// <exception cref="NaoAutorizadoException">Credenciais inválidas, conta inativa ou bloqueada.</exception>
    Task<LoginResponse> AutenticarAsync(LoginRequest requisicao, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IServicoDeAutenticacao"/>
public sealed class ServicoDeAutenticacao(
    IRepositorioDeUsuarios repositorio,
    IServicoDeHashDeSenha hasher,
    IGeradorDeToken geradorDeToken,
    IProvedorDeDataHora relogio,
    IUnitOfWork unitOfWork,
    ILogger<ServicoDeAutenticacao> logger) : IServicoDeAutenticacao
{
    public async Task<LoginResponse> AutenticarAsync(
        LoginRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var usuario = await repositorio.ObterPorEmailAsync(
            (requisicao.Email ?? string.Empty).Trim().ToLowerInvariant(),
            cancellationToken);

        if (usuario is null)
        {
            // Mensagem idêntica à de senha errada: revelar que o e-mail não existe
            // permitiria enumerar contas válidas do sistema.
            logger.LogWarning("Tentativa de login para e-mail não cadastrado.");
            throw new NaoAutorizadoException();
        }

        bool autenticado;

        try
        {
            autenticado = usuario.TentarAutenticar(requisicao.Senha, hasher.Verificar, relogio.Agora);
        }
        catch (DomainException excecao)
        {
            // Conta inativa ou bloqueada: o estado do usuário mudou (contador de tentativas),
            // então é preciso persistir antes de propagar a recusa.
            await PersistirEstadoAsync(usuario, cancellationToken);
            throw new NaoAutorizadoException(excecao.Message);
        }

        await PersistirEstadoAsync(usuario, cancellationToken);

        if (!autenticado)
        {
            logger.LogWarning(
                "Falha de autenticação do usuário {UsuarioId}. Tentativas consecutivas: {Tentativas}.",
                usuario.Id, usuario.TentativasFalhas);

            throw new NaoAutorizadoException();
        }

        var token = geradorDeToken.Gerar(usuario);

        logger.LogInformation("Usuário {UsuarioId} autenticado com sucesso.", usuario.Id);

        return new LoginResponse
        {
            Token = token.Token,
            TipoToken = token.TipoToken,
            ExpiraEm = token.ExpiraEm,
            Usuario = UsuarioResponse.De(usuario)
        };
    }

    private async Task PersistirEstadoAsync(Domain.Identidade.Usuario usuario, CancellationToken cancellationToken)
    {
        repositorio.Atualizar(usuario);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }
}
