using AutoMecanic.Application.Abstractions;
using AutoMecanic.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoMecanic.Infrastructure.Notificacoes;

/// <summary>Configurações do processamento da caixa de saída.</summary>
public sealed class OpcoesDaCaixaDeSaida
{
    public const string SecaoDeConfiguracao = "Notificacoes";

    /// <summary>
    /// Liga o processo em segundo plano que envia os e-mails. Desligado, as mensagens continuam
    /// sendo registradas e saem quando ele for ligado.
    /// </summary>
    public bool Habilitadas { get; set; }

    /// <summary>Intervalo entre as verificações da caixa de saída.</summary>
    public int IntervaloEmSegundos { get; set; } = 5;

    /// <summary>Mensagens tratadas por rodada.</summary>
    public int TamanhoDoLote { get; set; } = 20;

    /// <summary>Tentativas antes de desistir de uma mensagem.</summary>
    public int MaximoDeTentativas { get; set; } = 5;
}

/// <summary>
/// Envia um lote de mensagens pendentes da caixa de saída.
/// <para>
/// Com várias réplicas da API sob o autoescalonamento, cada uma roda este processador. O
/// <c>FOR UPDATE SKIP LOCKED</c> faz cada réplica travar um lote diferente: uma mensagem nunca é
/// enviada por duas réplicas, e nenhuma espera pela outra. Se a réplica cair no meio do lote, a
/// transação é desfeita e as mensagens voltam a ficar disponíveis.
/// </para>
/// <para>
/// Falhas de envio não descartam a mensagem: ela volta para a fila com espera crescente
/// (1, 2, 4, 8… minutos) até esgotar as tentativas.
/// </para>
/// </summary>
public sealed class ProcessadorDaCaixaDeSaida(
    AutoMecanicDbContext contexto,
    IEnviadorDeEmail enviador,
    TimeProvider relogio,
    IOptions<OpcoesDaCaixaDeSaida> opcoes,
    ILogger<ProcessadorDaCaixaDeSaida> logger)
{
    private readonly OpcoesDaCaixaDeSaida _opcoes = opcoes.Value;

    /// <returns>Quantidade de mensagens enviadas nesta rodada.</returns>
    public async Task<int> ProcessarLoteAsync(CancellationToken cancellationToken = default)
    {
        var estrategia = contexto.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async ct =>
        {
            contexto.ChangeTracker.Clear();

            await using var transacao = await contexto.Database.BeginTransactionAsync(ct);

            var agora = relogio.GetUtcNow();
            var pendente = nameof(SituacaoDaMensagem.Pendente);

            var lote = await contexto.MensagensDeSaida
                .FromSql($"""
                    SELECT * FROM automecanic.mensagem_saida
                    WHERE situacao = {pendente} AND proxima_tentativa_em <= {agora}
                    ORDER BY criada_em
                    LIMIT {_opcoes.TamanhoDoLote}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            var enviadas = 0;

            foreach (var mensagem in lote)
            {
                if (await TentarEnviarAsync(mensagem, agora, ct))
                {
                    enviadas++;
                }
            }

            await contexto.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);

            if (lote.Count > 0)
            {
                logger.LogInformation("Caixa de saída: {Enviadas} de {Total} mensagem(ns) enviada(s).", enviadas, lote.Count);
            }

            return enviadas;
        }, cancellationToken);
    }

    private async Task<bool> TentarEnviarAsync(MensagemDeSaida mensagem, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        try
        {
            await enviador.EnviarAsync(mensagem.ParaEmail(), cancellationToken);

            mensagem.Situacao = SituacaoDaMensagem.Enviada;
            mensagem.EnviadaEm = agora;
            mensagem.Tentativas++;
            mensagem.UltimoErro = null;

            return true;
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            mensagem.Tentativas++;
            mensagem.UltimoErro = excecao.Message.Length > 1000 ? excecao.Message[..1000] : excecao.Message;

            if (mensagem.Tentativas >= _opcoes.MaximoDeTentativas)
            {
                mensagem.Situacao = SituacaoDaMensagem.Falhou;

                logger.LogError(excecao,
                    "E-mail {Id} da OS {Referencia} descartado após {Tentativas} tentativas.",
                    mensagem.Id, mensagem.Referencia, mensagem.Tentativas);
            }
            else
            {
                mensagem.ProximaTentativaEm = agora.AddMinutes(Math.Pow(2, mensagem.Tentativas - 1));

                logger.LogWarning(excecao,
                    "Falha ao enviar o e-mail {Id} da OS {Referencia} (tentativa {Tentativas}); nova tentativa às {Proxima}.",
                    mensagem.Id, mensagem.Referencia, mensagem.Tentativas, mensagem.ProximaTentativaEm);
            }

            return false;
        }
    }
}
