using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.Identidade;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AutoMecanic.Infrastructure.Seguranca;

/// <summary>
/// Emite os tokens JWT usados pelas APIs administrativas.
/// <para>
/// O token carrega apenas o necessário para autorizar: identificador, e-mail, nome e perfil.
/// Nada sensível entra nas reivindicações — o conteúdo de um JWT é assinado, mas <b>não é
/// criptografado</b> e pode ser lido por qualquer um que o intercepte.
/// </para>
/// </summary>
public sealed class GeradorDeTokenJwt(IOptions<OpcoesDeJwt> opcoes) : IGeradorDeToken
{
    private readonly OpcoesDeJwt _opcoes = opcoes.Value;

    public TokenDeAcesso Gerar(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var agora = DateTimeOffset.UtcNow;
        var expiraEm = agora.AddMinutes(_opcoes.ValidadeEmMinutos);

        var reivindicacoes = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email.Endereco),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),

            // Identificador único do token: permite revogação individual e rastreio em auditoria.
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(JwtRegisteredClaimNames.Iat, agora.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),

            // O perfil vira uma role padrão do ASP.NET Core, habilitando [Authorize(Roles = ...)].
            new(ClaimTypes.Role, usuario.Perfil.ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString())
        };

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.ChaveDeAssinatura));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opcoes.Emissor,
            audience: _opcoes.Audiencia,
            claims: reivindicacoes,
            notBefore: agora.UtcDateTime,
            expires: expiraEm.UtcDateTime,
            signingCredentials: credenciais);

        return new TokenDeAcesso(new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
