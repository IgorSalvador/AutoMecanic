using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoMecanic.Application.Notificacoes;

/// <summary>
/// Avisa o cliente por e-mail a cada mudança de situação da Ordem de Serviço — é a
/// "atualização de status via e-mail" do requisito.
/// <para>
/// Roda dentro da transação da mudança, mas não envia nada: só registra a mensagem na caixa de
/// saída, de onde um processo em segundo plano a envia depois do commit.
/// </para>
/// <list type="bullet">
///   <item>Abertura: confirma o recebimento do veículo.</item>
///   <item>Envio do orçamento: valor, validade e como responder.</item>
///   <item>Demais mudanças: a nova situação e o que ela significa.</item>
/// </list>
/// </summary>
public sealed class NotificarClienteHandler(
    IRepositorioDeClientes repositorioDeClientes,
    IRepositorioDeVeiculos repositorioDeVeiculos,
    ICaixaDeSaidaDeEmails caixaDeSaida,
    IOptions<OpcoesDeNotificacao> opcoes,
    ILogger<NotificarClienteHandler> logger) :
    IDomainEventHandler<OrdemDeServicoAberta>,
    IDomainEventHandler<StatusDaOrdemAlterado>,
    IDomainEventHandler<OrcamentoEnviadoAoCliente>
{
    private readonly OpcoesDeNotificacao _opcoes = opcoes.Value;

    public async Task TratarAsync(OrdemDeServicoAberta evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var veiculo = await repositorioDeVeiculos.ObterPorIdAsync(evento.VeiculoId, cancellationToken);

        await EnfileirarAsync(
            evento.ClienteId,
            evento.Numero,
            nome => ModelosDeEmailDaOrdemServico.OrdemAberta(nome, evento.Numero, veiculo?.Descricao ?? "informado na recepção", _opcoes.NomeDaOficina),
            cancellationToken);
    }

    public async Task TratarAsync(StatusDaOrdemAlterado evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        // O envio do orçamento tem e-mail próprio, mais completo, disparado pelo evento
        // OrcamentoEnviadoAoCliente; avisar duas vezes da mesma mudança só confundiria o cliente.
        if (evento.StatusAtual == StatusOrdemServico.AguardandoAprovacao)
        {
            return;
        }

        await EnfileirarAsync(
            evento.ClienteId,
            evento.Numero,
            nome => ModelosDeEmailDaOrdemServico.SituacaoAlterada(nome, evento.Numero, evento.StatusAtual, _opcoes.NomeDaOficina),
            cancellationToken);
    }

    public async Task TratarAsync(OrcamentoEnviadoAoCliente evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        await EnfileirarAsync(
            evento.ClienteId,
            evento.Numero,
            nome => ModelosDeEmailDaOrdemServico.OrcamentoEnviado(
                nome, evento.Numero, evento.ValorTotal, evento.ValidoAte, _opcoes.NomeDaOficina, _opcoes.UrlPublicaDaApi),
            cancellationToken);
    }

    private async Task EnfileirarAsync(
        Guid clienteId,
        string numero,
        Func<string, ConteudoDeEmail> montar,
        CancellationToken cancellationToken)
    {
        var cliente = await repositorioDeClientes.ObterPorIdAsync(clienteId, cancellationToken);

        if (cliente is null)
        {
            logger.LogWarning("Cliente {ClienteId} da OS {Numero} não encontrado; notificação não registrada.", clienteId, numero);
            return;
        }

        var conteudo = montar(cliente.Nome);

        await caixaDeSaida.EnfileirarAsync(
            new EmailDeSaida(cliente.Email.Endereco, cliente.Nome, conteudo.Assunto, conteudo.CorpoHtml, conteudo.CorpoTexto, numero),
            cancellationToken);
    }
}
