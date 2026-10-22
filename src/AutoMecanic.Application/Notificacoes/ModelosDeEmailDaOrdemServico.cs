using System.Globalization;
using System.Net;
using System.Text;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.Application.Notificacoes;

/// <summary>Assunto e corpos (HTML e texto) de um e-mail.</summary>
public sealed record ConteudoDeEmail(string Assunto, string CorpoHtml, string CorpoTexto);

/// <summary>
/// Conteúdo dos e-mails enviados ao cliente ao longo da Ordem de Serviço.
/// <para>
/// Ponto único de troca dos textos: a equipe pode substituir estes modelos pelos seus sem tocar
/// em quem decide <i>quando</i> notificar. Todo valor vindo de cadastro é codificado antes de
/// entrar no HTML.
/// </para>
/// </summary>
public static class ModelosDeEmailDaOrdemServico
{
    /// <summary>
    /// Horário de Brasília, fixo: o Brasil não adota horário de verão desde 2019, e um deslocamento
    /// fixo dispensa a base de fusos horários, ausente na imagem mínima do contêiner.
    /// </summary>
    private static readonly TimeSpan HorarioDeBrasilia = TimeSpan.FromHours(-3);

    public static ConteudoDeEmail OrdemAberta(string nomeDoCliente, string numero, string veiculo, string oficina) =>
        Montar(
            $"{oficina} · {numero} aberta",
            nomeDoCliente,
            oficina,
            [
                $"Recebemos o seu veículo <b>{Html(veiculo)}</b> e abrimos a Ordem de Serviço <b>{Html(numero)}</b>.",
                "Você receberá um e-mail a cada etapa do atendimento: diagnóstico, orçamento, execução e entrega."
            ],
            numero);

    public static ConteudoDeEmail SituacaoAlterada(string nomeDoCliente, string numero, StatusOrdemServico situacao, string oficina) =>
        Montar(
            $"{oficina} · {numero}: {situacao.Descricao()}",
            nomeDoCliente,
            oficina,
            [
                $"A Ordem de Serviço <b>{Html(numero)}</b> mudou de situação: <b>{Html(situacao.Descricao())}</b>.",
                Explicar(situacao)
            ],
            numero);

    public static ConteudoDeEmail OrcamentoEnviado(
        string nomeDoCliente,
        string numero,
        decimal valorTotal,
        DateTimeOffset validoAte,
        string oficina,
        string urlPublicaDaApi)
    {
        var validade = validoAte.ToOffset(HorarioDeBrasilia).ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.InvariantCulture);
        var base_ = urlPublicaDaApi.TrimEnd('/');

        var instrucoes = string.IsNullOrWhiteSpace(base_)
            ? "Para responder, informe o número da OS e o seu CPF/CNPJ na API de autoatendimento da oficina."
            : $"Para responder, envie o número da OS e o seu CPF/CNPJ para <b>{Html(base_)}/api/v1/acompanhamento/orcamento/aprovar</b> "
              + $"(ou <b>/reprovar</b>). A documentação está em <b>{Html(base_)}/swagger</b>.";

        return Montar(
            $"{oficina} · Orçamento da {numero} aguardando sua aprovação",
            nomeDoCliente,
            oficina,
            [
                $"O orçamento da Ordem de Serviço <b>{Html(numero)}</b> está pronto: <b>{Html(Dinheiro.De(valorTotal).ToString())}</b>.",
                $"Ele vale até <b>{Html(validade)}</b> (horário de Brasília). Sem resposta até lá, a OS é cancelada e as peças separadas voltam ao estoque.",
                instrucoes
            ],
            numero);
    }

    private static string Explicar(StatusOrdemServico situacao) => situacao switch
    {
        StatusOrdemServico.EmDiagnostico => "Um mecânico está avaliando o veículo. Em seguida você receberá o orçamento.",
        StatusOrdemServico.AguardandoAprovacao => "O orçamento foi enviado e aguarda a sua resposta.",
        StatusOrdemServico.EmExecucao => "O orçamento foi aprovado e os serviços estão em execução.",
        StatusOrdemServico.Finalizada => "Os serviços foram concluídos. O veículo está pronto para retirada.",
        StatusOrdemServico.Entregue => "O veículo foi entregue. Obrigado pela confiança!",
        StatusOrdemServico.Cancelada => "A Ordem de Serviço foi cancelada. Em caso de dúvida, fale com a oficina.",
        _ => "Acompanhe a Ordem de Serviço pela consulta pública da oficina."
    };

    private static ConteudoDeEmail Montar(
        string assunto,
        string nomeDoCliente,
        string oficina,
        IReadOnlyList<string> paragrafosHtml,
        string numero)
    {
        var html = new StringBuilder()
            .Append("<!doctype html><html lang=\"pt-BR\"><body style=\"font-family:Arial,sans-serif;color:#1f2933;line-height:1.5\">")
            .Append(CultureInfo.InvariantCulture, $"<p>Olá, {Html(nomeDoCliente)}.</p>");

        foreach (var paragrafo in paragrafosHtml)
        {
            html.Append(CultureInfo.InvariantCulture, $"<p>{paragrafo}</p>");
        }

        html.Append(CultureInfo.InvariantCulture, $"<p style=\"color:#6b7280;font-size:12px\">{Html(oficina)} · Ordem de Serviço {Html(numero)}</p>")
            .Append("</body></html>");

        var texto = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Olá, {nomeDoCliente}.")
            .AppendLine();

        foreach (var paragrafo in paragrafosHtml)
        {
            texto.AppendLine(ParaTexto(paragrafo)).AppendLine();
        }

        texto.Append(CultureInfo.InvariantCulture, $"{oficina} · Ordem de Serviço {numero}");

        return new ConteudoDeEmail(assunto, html.ToString(), texto.ToString());
    }

    private static string Html(string valor) => WebUtility.HtmlEncode(valor);

    private static string ParaTexto(string html) =>
        WebUtility.HtmlDecode(html.Replace("<b>", string.Empty, StringComparison.Ordinal).Replace("</b>", string.Empty, StringComparison.Ordinal));
}
