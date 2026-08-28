using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.Application.Indicadores;

/// <summary>
/// Indicador de tempo médio de execução, exigido pelo requisito "Monitoramento do tempo
/// médio de execução dos serviços".
/// </summary>
public sealed record TempoMedioDeExecucaoResponse
{
    /// <summary>Início do período analisado.</summary>
    public required DateTimeOffset PeriodoDe { get; init; }

    /// <summary>Fim do período analisado.</summary>
    public required DateTimeOffset PeriodoAte { get; init; }

    /// <summary>Ordens finalizadas no período, base do cálculo.</summary>
    public required int OrdensFinalizadas { get; init; }

    /// <summary>Média do intervalo entre início da execução e finalização, em minutos.</summary>
    public required double TempoMedioDeExecucaoEmMinutos { get; init; }

    /// <summary>Mediana da execução. Menos sensível a uma OS excepcionalmente longa que a média.</summary>
    public required double TempoMedianoDeExecucaoEmMinutos { get; init; }

    /// <summary>Menor tempo de execução observado no período.</summary>
    public required double MenorTempoEmMinutos { get; init; }

    /// <summary>Maior tempo de execução observado no período.</summary>
    public required double MaiorTempoEmMinutos { get; init; }

    /// <summary>
    /// Média do tempo total de permanência do veículo, da abertura à entrega. Considera
    /// apenas ordens já entregues.
    /// </summary>
    public required double TempoMedioDeAtendimentoEmMinutos { get; init; }

    /// <summary>
    /// Soma dos tempos estimados dos serviços contratados, dividida pelo número de ordens.
    /// Comparada ao tempo real, mostra se a tabela de tempos está calibrada.
    /// </summary>
    public required double TempoMedioEstimadoEmMinutos { get; init; }

    /// <summary>
    /// Razão entre tempo real e tempo estimado. Acima de 1 indica que a oficina leva mais
    /// tempo do que promete; abaixo de 1, que a estimativa está folgada.
    /// </summary>
    public required double AderenciaAEstimativa { get; init; }
}

/// <summary>Painel operacional da oficina.</summary>
public sealed record PainelOperacionalResponse
{
    /// <summary>Quantidade de Ordens de Serviço em cada situação.</summary>
    public required IReadOnlyDictionary<string, int> OrdensPorStatus { get; init; }

    /// <summary>Ordens em andamento (nem entregues, nem canceladas).</summary>
    public required int OrdensEmAberto { get; init; }

    /// <summary>Ordens aguardando decisão do cliente sobre o orçamento.</summary>
    public required int OrdensAguardandoAprovacao { get; init; }

    /// <summary>Peças no ponto de ressuprimento ou abaixo dele.</summary>
    public required int PecasAbaixoDoEstoqueMinimo { get; init; }
}

/// <summary>Indicadores gerenciais derivados das Ordens de Serviço.</summary>
public interface IServicoDeIndicadores
{
    /// <summary>Calcula o tempo médio de execução no período informado.</summary>
    /// <param name="de">Início do período. Quando omitido, usa os últimos 30 dias.</param>
    /// <param name="ate">Fim do período. Quando omitido, usa o instante atual.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<TempoMedioDeExecucaoResponse> ObterTempoMedioDeExecucaoAsync(
        DateTimeOffset? de,
        DateTimeOffset? ate,
        CancellationToken cancellationToken = default);

    /// <summary>Retorna a fotografia atual da operação.</summary>
    Task<PainelOperacionalResponse> ObterPainelOperacionalAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IServicoDeIndicadores"/>
public sealed class ServicoDeIndicadores(
    IRepositorioDeOrdensServico repositorio,
    IRepositorioDePecas repositorioDePecas,
    IProvedorDeDataHora relogio) : IServicoDeIndicadores
{
    private const int JanelaPadraoEmDias = 30;

    public async Task<TempoMedioDeExecucaoResponse> ObterTempoMedioDeExecucaoAsync(
        DateTimeOffset? de,
        DateTimeOffset? ate,
        CancellationToken cancellationToken = default)
    {
        var fim = ate ?? relogio.Agora;
        var inicio = de ?? fim.AddDays(-JanelaPadraoEmDias);

        if (inicio > fim)
        {
            (inicio, fim) = (fim, inicio);
        }

        var ordens = await repositorio.ListarFinalizadasNoPeriodoAsync(inicio, fim, cancellationToken);

        // Só entram no cálculo as ordens que efetivamente têm os dois marcos temporais.
        var duracoes = ordens
            .Where(o => o.DuracaoDaExecucao is not null)
            .Select(o => o.DuracaoDaExecucao!.Value.TotalMinutes)
            .OrderBy(d => d)
            .ToList();

        if (duracoes.Count == 0)
        {
            return new TempoMedioDeExecucaoResponse
            {
                PeriodoDe = inicio,
                PeriodoAte = fim,
                OrdensFinalizadas = 0,
                TempoMedioDeExecucaoEmMinutos = 0,
                TempoMedianoDeExecucaoEmMinutos = 0,
                MenorTempoEmMinutos = 0,
                MaiorTempoEmMinutos = 0,
                TempoMedioDeAtendimentoEmMinutos = 0,
                TempoMedioEstimadoEmMinutos = 0,
                AderenciaAEstimativa = 0
            };
        }

        var atendimentos = ordens
            .Where(o => o.TempoTotalDeAtendimento is not null)
            .Select(o => o.TempoTotalDeAtendimento!.Value.TotalMinutes)
            .ToList();

        var estimativas = ordens
            .Where(o => o.TempoEstimadoTotalEmMinutos > 0)
            .Select(o => (double)o.TempoEstimadoTotalEmMinutos)
            .ToList();

        var mediaReal = duracoes.Average();
        var mediaEstimada = estimativas.Count > 0 ? estimativas.Average() : 0;

        return new TempoMedioDeExecucaoResponse
        {
            PeriodoDe = inicio,
            PeriodoAte = fim,
            OrdensFinalizadas = duracoes.Count,
            TempoMedioDeExecucaoEmMinutos = Arredondar(mediaReal),
            TempoMedianoDeExecucaoEmMinutos = Arredondar(CalcularMediana(duracoes)),
            MenorTempoEmMinutos = Arredondar(duracoes[0]),
            MaiorTempoEmMinutos = Arredondar(duracoes[^1]),
            TempoMedioDeAtendimentoEmMinutos = Arredondar(atendimentos.Count > 0 ? atendimentos.Average() : 0),
            TempoMedioEstimadoEmMinutos = Arredondar(mediaEstimada),
            AderenciaAEstimativa = mediaEstimada > 0 ? Arredondar(mediaReal / mediaEstimada) : 0
        };
    }

    public async Task<PainelOperacionalResponse> ObterPainelOperacionalAsync(CancellationToken cancellationToken = default)
    {
        var porStatus = await repositorio.ContarPorStatusAsync(cancellationToken);
        var pecasCriticas = await repositorioDePecas.ListarAbaixoDoEstoqueMinimoAsync(cancellationToken);

        var emAberto = porStatus
            .Where(par => !par.Key.EhTerminal())
            .Sum(par => par.Value);

        return new PainelOperacionalResponse
        {
            OrdensPorStatus = porStatus.ToDictionary(par => par.Key.Descricao(), par => par.Value),
            OrdensEmAberto = emAberto,
            OrdensAguardandoAprovacao = porStatus.GetValueOrDefault(StatusOrdemServico.AguardandoAprovacao),
            PecasAbaixoDoEstoqueMinimo = pecasCriticas.Count
        };
    }

    /// <summary>Mediana de uma sequência já ordenada de forma crescente.</summary>
    private static double CalcularMediana(IReadOnlyList<double> ordenados)
    {
        var meio = ordenados.Count / 2;

        return ordenados.Count % 2 == 1
            ? ordenados[meio]
            : (ordenados[meio - 1] + ordenados[meio]) / 2d;
    }

    private static double Arredondar(double valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
