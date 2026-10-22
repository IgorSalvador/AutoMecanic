using System.ComponentModel.DataAnnotations;
using AutoMecanic.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoMecanic.Infrastructure.Notificacoes;

/// <summary>
/// Servidor SMTP de saída. Os mesmos campos servem ao Mailpit em desenvolvimento e ao Gmail (ou
/// qualquer outro provedor) em produção: só a configuração muda.
/// </summary>
public sealed class OpcoesDeSmtp
{
    public const string SecaoDeConfiguracao = "Smtp";

    [Required(ErrorMessage = "Informe o servidor SMTP (Smtp:Host).")]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Porta { get; set; } = 587;

    /// <summary>
    /// <c>StartTls</c> (porta 587, padrão do Gmail), <c>SslNaConexao</c> (porta 465) ou
    /// <c>Nenhuma</c> (apenas para o Mailpit local).
    /// </summary>
    public SegurancaSmtp Seguranca { get; set; } = SegurancaSmtp.StartTls;

    /// <summary>Usuário de autenticação. Vazio dispensa autenticação (Mailpit).</summary>
    public string? Usuario { get; set; }

    /// <summary>Senha de app do provedor. Vem de segredo, nunca do repositório.</summary>
    public string? Senha { get; set; }

    [Required(ErrorMessage = "Informe o remetente (Smtp:Remetente).")]
    [EmailAddress]
    public string Remetente { get; set; } = string.Empty;

    public string NomeDoRemetente { get; set; } = "AutoMecanic";

    [Range(1, 120)]
    public int TempoLimiteEmSegundos { get; set; } = 30;
}

/// <summary>Modo de segurança da conexão SMTP.</summary>
public enum SegurancaSmtp
{
    StartTls,
    SslNaConexao,
    Nenhuma
}

/// <summary>Envia e-mails por SMTP com MailKit, o cliente SMTP recomendado para .NET.</summary>
public sealed class EnviadorDeEmailSmtp(IOptions<OpcoesDeSmtp> opcoes) : IEnviadorDeEmail
{
    private readonly OpcoesDeSmtp _opcoes = opcoes.Value;

    public async Task EnviarAsync(EmailDeSaida email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var mensagem = new MimeMessage();
        mensagem.From.Add(new MailboxAddress(_opcoes.NomeDoRemetente, _opcoes.Remetente));
        mensagem.To.Add(new MailboxAddress(email.NomeDoDestinatario, email.Destinatario));
        mensagem.Subject = email.Assunto;
        mensagem.Body = new BodyBuilder { HtmlBody = email.CorpoHtml, TextBody = email.CorpoTexto }.ToMessageBody();

        using var cliente = new SmtpClient { Timeout = _opcoes.TempoLimiteEmSegundos * 1000 };

        var seguranca = _opcoes.Seguranca switch
        {
            SegurancaSmtp.SslNaConexao => SecureSocketOptions.SslOnConnect,
            SegurancaSmtp.Nenhuma => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls
        };

        await cliente.ConnectAsync(_opcoes.Host, _opcoes.Porta, seguranca, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_opcoes.Usuario))
        {
            await cliente.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha ?? string.Empty, cancellationToken);
        }

        await cliente.SendAsync(mensagem, cancellationToken);
        await cliente.DisconnectAsync(quit: true, cancellationToken);
    }
}
