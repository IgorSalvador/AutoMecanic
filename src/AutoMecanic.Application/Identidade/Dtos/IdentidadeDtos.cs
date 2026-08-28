using System.Text.Json.Serialization;
using AutoMecanic.Domain.Identidade;

namespace AutoMecanic.Application.Identidade.Dtos;

/// <summary>Credenciais de acesso às APIs administrativas.</summary>
/// <param name="Email">E-mail cadastrado do usuário.</param>
/// <param name="Senha">Senha em claro. Trafega apenas sob HTTPS e nunca é registrada em log.</param>
public sealed record LoginRequest(string Email, string Senha);

/// <summary>Token JWT emitido após autenticação bem-sucedida.</summary>
public sealed record LoginResponse
{
    /// <summary>JWT a ser enviado no cabeçalho <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public required string Token { get; init; }

    public required string TipoToken { get; init; }

    public required DateTimeOffset ExpiraEm { get; init; }

    public required UsuarioResponse Usuario { get; init; }
}

/// <summary>Dados para criar um usuário administrativo.</summary>
/// <param name="Nome">Nome completo.</param>
/// <param name="Email">E-mail de login, único.</param>
/// <param name="Senha">Senha inicial. Mínimo 8 caracteres com maiúscula, minúscula, dígito e símbolo.</param>
/// <param name="Perfil">Perfil de acesso.</param>
public sealed record CriarUsuarioRequest(string Nome, string Email, string Senha, PerfilUsuario Perfil);

/// <summary>Atualização de dados básicos e perfil do usuário.</summary>
/// <param name="Nome">Nome completo.</param>
/// <param name="Perfil">Novo perfil de acesso.</param>
public sealed record AtualizarUsuarioRequest(string Nome, PerfilUsuario Perfil);

/// <summary>Troca de senha feita pelo próprio usuário.</summary>
/// <param name="SenhaAtual">Senha vigente, exigida como prova de identidade.</param>
/// <param name="NovaSenha">Nova senha, obedecendo à política mínima.</param>
public sealed record AlterarSenhaRequest(string SenhaAtual, string NovaSenha);

/// <summary>Redefinição de senha feita por um administrador.</summary>
/// <param name="NovaSenha">Nova senha, obedecendo à política mínima.</param>
public sealed record RedefinirSenhaRequest(string NovaSenha);

/// <summary>
/// Representação do usuário devolvida pela API. O hash da senha nunca aparece aqui — o DTO
/// existe justamente para tornar impossível vazá-lo por acidente ao serializar o agregado.
/// </summary>
public sealed record UsuarioResponse
{
    public required Guid Id { get; init; }

    public required string Nome { get; init; }

    public required string Email { get; init; }

    public required PerfilUsuario Perfil { get; init; }

    public required bool Ativo { get; init; }

    public DateTimeOffset? UltimoAcessoEm { get; init; }

    public required DateTimeOffset CadastradoEm { get; init; }

    /// <summary>Presente apenas quando a conta está sob bloqueio temporário.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? BloqueadoAte { get; init; }

    public static UsuarioResponse De(Usuario usuario) => new()
    {
        Id = usuario.Id,
        Nome = usuario.Nome,
        Email = usuario.Email.Endereco,
        Perfil = usuario.Perfil,
        Ativo = usuario.Ativo,
        UltimoAcessoEm = usuario.UltimoAcessoEm,
        CadastradoEm = usuario.CadastradoEm,
        BloqueadoAte = usuario.BloqueadoAte
    };
}
