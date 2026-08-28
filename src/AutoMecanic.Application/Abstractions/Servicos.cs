using AutoMecanic.Domain.Identidade;

namespace AutoMecanic.Application.Abstractions;

/// <summary>
/// Fonte de tempo injetável. Existe para que regras sensíveis a data (validade de orçamento,
/// bloqueio de usuário) possam ser testadas de forma determinística.
/// </summary>
public interface IProvedorDeDataHora
{
    DateTimeOffset Agora { get; }
}

/// <summary>
/// Serviço de hash de senha. A escolha do algoritmo (BCrypt) é detalhe de infraestrutura;
/// o domínio e a aplicação conhecem apenas este contrato.
/// </summary>
public interface IServicoDeHashDeSenha
{
    string GerarHash(string senha);

    bool Verificar(string senha, string hash);
}

/// <summary>Token JWT emitido para um usuário autenticado.</summary>
/// <param name="Token">JWT assinado a ser enviado no cabeçalho <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiraEm">Instante de expiração do token.</param>
/// <param name="TipoToken">Esquema de autenticação, sempre <c>Bearer</c>.</param>
public sealed record TokenDeAcesso(string Token, DateTimeOffset ExpiraEm, string TipoToken = "Bearer");

/// <summary>Emissor de tokens JWT para as APIs administrativas.</summary>
public interface IGeradorDeToken
{
    TokenDeAcesso Gerar(Usuario usuario);
}

/// <summary>
/// Identidade do usuário da requisição corrente, extraída do JWT. Permite que os casos de
/// uso registrem autoria (quem iniciou o diagnóstico, quem finalizou o serviço) sem que a
/// camada de aplicação conheça <c>HttpContext</c>.
/// </summary>
public interface IUsuarioAtual
{
    Guid? Id { get; }

    string? Email { get; }

    PerfilUsuario? Perfil { get; }

    bool EstaAutenticado { get; }
}

/// <summary>
/// Gera o próximo número sequencial de Ordem de Serviço do ano corrente. A implementação usa
/// uma sequência do PostgreSQL, garantindo unicidade mesmo com requisições concorrentes.
/// </summary>
public interface IGeradorDeNumeroDeOrdemServico
{
    Task<int> ProximoSequencialAsync(int ano, CancellationToken cancellationToken = default);
}
