namespace AutoMecanic.Application.Abstractions;

/// <summary>E-mail pronto para envio: destinatário e conteúdo já resolvidos.</summary>
/// <param name="Destinatario">Endereço de e-mail do destinatário.</param>
/// <param name="NomeDoDestinatario">Nome exibido na saudação e no cabeçalho.</param>
/// <param name="Assunto">Linha de assunto.</param>
/// <param name="CorpoHtml">Corpo em HTML.</param>
/// <param name="CorpoTexto">Corpo em texto simples, para clientes de e-mail sem HTML.</param>
/// <param name="Referencia">Origem da mensagem (número da OS), para rastreio.</param>
public sealed record EmailDeSaida(
    string Destinatario,
    string NomeDoDestinatario,
    string Assunto,
    string CorpoHtml,
    string CorpoTexto,
    string Referencia);

/// <summary>
/// <b>Caixa de saída</b> (padrão <i>outbox</i>): registra o e-mail na mesma transação da mudança
/// que o originou, para envio posterior.
/// <para>
/// Enviar dentro da transação faria a operação do cliente depender do servidor de e-mail — e,
/// pior, poderia enviar a notificação de uma mudança que depois fosse desfeita. Enviar depois do
/// commit, sem registro, perderia o e-mail se o processo caísse no intervalo, o que acontece
/// toda vez que o autoescalonamento remove uma réplica. Registrado aqui, o e-mail sai se e
/// somente se a mudança foi gravada.
/// </para>
/// </summary>
public interface ICaixaDeSaidaDeEmails
{
    Task EnfileirarAsync(EmailDeSaida email, CancellationToken cancellationToken = default);
}

/// <summary>Envio efetivo de um e-mail. A implementação usa SMTP, servindo a qualquer provedor.</summary>
public interface IEnviadorDeEmail
{
    Task EnviarAsync(EmailDeSaida email, CancellationToken cancellationToken = default);
}

/// <summary>Configurações das notificações ao cliente usadas pela camada de aplicação.</summary>
public sealed class OpcoesDeNotificacao
{
    public const string SecaoDeConfiguracao = "Notificacoes";

    /// <summary>
    /// Endereço público da API, citado nos e-mails para o cliente acompanhar a OS e responder
    /// ao orçamento. Vazio omite o endereço e mantém apenas as instruções.
    /// </summary>
    public string UrlPublicaDaApi { get; set; } = string.Empty;

    /// <summary>Nome da oficina exibido nos e-mails.</summary>
    public string NomeDaOficina { get; set; } = "AutoMecanic";
}
