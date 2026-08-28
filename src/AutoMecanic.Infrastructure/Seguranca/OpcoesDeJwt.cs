using System.ComponentModel.DataAnnotations;

namespace AutoMecanic.Infrastructure.Seguranca;

/// <summary>
/// Configuração da emissão e validação de tokens JWT.
/// <para>
/// A chave de assinatura <b>não tem valor padrão</b> de propósito: a aplicação recusa subir
/// sem que ela seja fornecida por variável de ambiente ou cofre de segredos. Uma chave
/// embutida no código seria pública no repositório e permitiria a qualquer pessoa forjar
/// tokens de administrador.
/// </para>
/// </summary>
public sealed class OpcoesDeJwt
{
    /// <summary>Caminho da seção correspondente no arquivo de configuração.</summary>
    public const string SecaoDeConfiguracao = "Jwt";

    /// <summary>Comprimento mínimo da chave: 256 bits, exigido pelo algoritmo HMAC-SHA256.</summary>
    public const int ComprimentoMinimoDaChave = 32;

    /// <summary>Quem emite o token. Validado na entrada de cada requisição.</summary>
    [Required(ErrorMessage = "O emissor (Jwt:Emissor) é obrigatório.")]
    public string Emissor { get; set; } = string.Empty;

    /// <summary>Para quem o token se destina. Validado na entrada de cada requisição.</summary>
    [Required(ErrorMessage = "A audiência (Jwt:Audiencia) é obrigatória.")]
    public string Audiencia { get; set; } = string.Empty;

    /// <summary>Segredo usado para assinar o token com HMAC-SHA256.</summary>
    [Required(ErrorMessage = "A chave de assinatura (Jwt:ChaveDeAssinatura) é obrigatória.")]
    [MinLength(ComprimentoMinimoDaChave,
        ErrorMessage = "A chave de assinatura deve ter no mínimo 32 caracteres (256 bits).")]
    public string ChaveDeAssinatura { get; set; } = string.Empty;

    /// <summary>
    /// Validade do token em minutos. Curta por padrão: um token vazado tem janela de uso
    /// limitada, e o custo de renovar é baixo para um sistema administrativo.
    /// </summary>
    [Range(5, 1440, ErrorMessage = "A validade do token deve estar entre 5 e 1440 minutos.")]
    public int ValidadeEmMinutos { get; set; } = 60;
}
