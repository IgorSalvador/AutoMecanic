using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.OrdensServico.CasosDeUso;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Application.Servicos.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Infrastructure.Persistencia.Seed;

/// <summary>
/// Semeia as Ordens de Serviço de demonstração percorrendo os mesmos casos de uso que a API
/// expõe — recepção, diagnóstico, composição, orçamento, aprovação, execução e entrega. Cada
/// etapa é injetada explicitamente: o construtor longo é o retrato fiel de quantos passos o
/// fluxo real tem.
/// </summary>
public sealed class SemeadorDeOrdensDeServico(
    AutoMecanicDbContext contexto,
    ReceberVeiculo receberVeiculo,
    IniciarDiagnostico iniciarDiagnostico,
    RegistrarDiagnostico registrarDiagnostico,
    AdicionarServicoNaOrdem adicionarServico,
    AdicionarPecaNaOrdem adicionarPeca,
    GerarOrcamento gerarOrcamento,
    EnviarOrcamento enviarOrcamento,
    AprovarOrcamento aprovarOrcamento,
    ReprovarOrcamento reprovarOrcamento,
    FinalizarServico finalizarServico,
    EntregarVeiculo entregarVeiculo,
    IProvedorDeDataHora relogio,
    ILogger<SemeadorDeOrdensDeServico> logger)
{
    // -----------------------------------------------------------------
    // Ordens de Serviço de demonstração
    // -----------------------------------------------------------------

    /// <summary>
    /// Cria Ordens de Serviço cobrindo todas as situações do ciclo de vida, para que o painel
    /// operacional, o indicador de tempo médio e a posição de estoque tenham conteúdo real
    /// assim que o ambiente sobe — sem que o avaliador precise executar o fluxo à mão antes
    /// de ver qualquer número.
    /// </summary>
    public async Task ExecutarAsync(
        IReadOnlyList<ServicoResponse> servicos,
        IReadOnlyList<PecaResponse> pecas,
        CancellationToken cancellationToken)
    {
        var trocaDeOleo = PorNome(servicos, "Troca de óleo do motor");
        var pastilhas = PorNome(servicos, "Troca de pastilhas de freio");
        var revisaoDeFreios = PorNome(servicos, "Revisão de freios");
        var alinhamento = PorNome(servicos, "Alinhamento e balanceamento");
        var diagnostico = PorNome(servicos, "Diagnóstico eletrônico");
        var arCondicionado = PorNome(servicos, "Higienização do ar-condicionado");

        var oleo = PorCodigo(pecas, "OL-5W30-1L");
        var filtroDeOleo = PorCodigo(pecas, "FIL-OLEO-001");
        var pastilhaDianteira = PorCodigo(pecas, "PAST-FRE-DIA");
        var filtroDeCabine = PorCodigo(pecas, "FIL-CABINE-001");

        // ── Três OS já entregues, com durações distintas ──────────────────────
        // São elas que alimentam o indicador de tempo médio de execução. As durações
        // (90, 150 e 330 minutos) e a distância no passado são aplicadas depois, pelo
        // ajuste de carimbos de tempo — ver RetroagirExecucaoAsync.

        var entregue1 = await ExecutarFluxoCompletoAsync(
            new DadosDeVeiculo("529.982.247-25", "Maria Aparecida Souza", "maria.souza@exemplo.com.br", "11987654321",
                "ABC1D23", "Volkswagen", "Gol 1.0", 2019, 2020, "Branco", 68_500),
            "Troca de óleo da revisão dos 70 mil km.",
            [(trocaDeOleo, 1)],
            [(oleo, 4), (filtroDeOleo, 1)],
            descontoPercentual: 0m,
            cancellationToken);

        await RetroagirExecucaoAsync(entregue1, diasAtras: 12, duracao: TimeSpan.FromMinutes(90), cancellationToken);

        var entregue2 = await ExecutarFluxoCompletoAsync(
            new DadosDeVeiculo("168.995.350-09", "João Carlos Pereira", "joao.pereira@exemplo.com.br", "21976543210",
                "XYZ4567", "Fiat", "Argo Drive 1.3", 2021, 2021, "Prata", 41_200),
            "Barulho ao frear e pedal baixo.",
            [(pastilhas, 1), (revisaoDeFreios, 1)],
            [(pastilhaDianteira, 1)],
            descontoPercentual: 5m,
            cancellationToken);

        await RetroagirExecucaoAsync(entregue2, diasAtras: 7, duracao: TimeSpan.FromMinutes(150), cancellationToken);

        var entregue3 = await ExecutarFluxoCompletoAsync(
            new DadosDeVeiculo("390.533.447-05", "Ana Beatriz Ramos", "ana.ramos@exemplo.com.br", "31988776655",
                "BRA2E19", "Hyundai", "HB20 Comfort", 2022, 2023, "Azul", 22_400),
            "Ar-condicionado com cheiro forte ao ligar.",
            [(arCondicionado, 1), (alinhamento, 1)],
            [(filtroDeCabine, 1)],
            descontoPercentual: 0m,
            cancellationToken);

        // Deliberadamente muito acima das outras: é o caso que separa a média da mediana
        // no indicador, e demonstra por que os dois números são reportados.
        await RetroagirExecucaoAsync(entregue3, diasAtras: 3, duracao: TimeSpan.FromMinutes(330), cancellationToken);

        // ── OS em execução ────────────────────────────────────────────────────
        var emExecucao = await AbrirComItensAsync(
            new DadosDeVeiculo("256.748.905-36", "Transportadora Rota Certa LTDA", "contato@rotacerta.com.br", "1133224455",
                "RCL7A88", "Mercedes-Benz", "Sprinter 416", 2022, 2023, "Branco", 112_800),
            "Revisão preventiva de frota, 115 mil km.",
            [(trocaDeOleo, 2), (revisaoDeFreios, 1)],
            [(oleo, 8), (filtroDeOleo, 2)],
            cancellationToken);

        await gerarOrcamento.ExecutarAsync(emExecucao, new GerarOrcamentoRequest(10m), cancellationToken);
        await enviarOrcamento.ExecutarAsync(emExecucao, new EnviarOrcamentoRequest(7), cancellationToken);
        await aprovarOrcamento.ExecutarAsync(emExecucao, cancellationToken);

        // ── OS aguardando aprovação ───────────────────────────────────────────
        // Mantém peças reservadas: é o estado que demonstra a diferença entre saldo
        // físico e disponível na consulta de estoque.
        var aguardando = await AbrirComItensAsync(
            new DadosDeVeiculo("872.615.340-80", "Ricardo Nogueira", "ricardo.nogueira@exemplo.com.br", "41991234567",
                "PRD4C21", "Chevrolet", "Onix LT 1.0", 2020, 2021, "Preto", 55_900),
            "Luz de injeção acesa no painel.",
            [(diagnostico, 1), (trocaDeOleo, 1)],
            [(oleo, 4), (filtroDeOleo, 1)],
            cancellationToken);

        await gerarOrcamento.ExecutarAsync(aguardando, new GerarOrcamentoRequest(0m), cancellationToken);
        await enviarOrcamento.ExecutarAsync(aguardando, new EnviarOrcamentoRequest(7), cancellationToken);

        // ── OS cancelada por reprovação do orçamento ──────────────────────────
        var reprovada = await AbrirComItensAsync(
            new DadosDeVeiculo("045.781.236-26", "Fernanda Lima Castro", "fernanda.castro@exemplo.com.br", "51993334444",
                "GHT8B45", "Renault", "Kwid Zen", 2021, 2022, "Vermelho", 33_100),
            "Embreagem patinando em subida.",
            [(PorNome(servicos, "Troca de embreagem"), 1)],
            [],
            cancellationToken);

        await gerarOrcamento.ExecutarAsync(reprovada, new GerarOrcamentoRequest(0m), cancellationToken);
        await enviarOrcamento.ExecutarAsync(reprovada, new EnviarOrcamentoRequest(7), cancellationToken);
        await reprovarOrcamento.ExecutarAsync(
            reprovada,
            new ReprovarOrcamentoRequest("Valor acima do orçado. Vou pesquisar outras oficinas."),
            cancellationToken);

        // ── OS em diagnóstico ─────────────────────────────────────────────────
        var emDiagnostico = await ReceberAsync(
            new DadosDeVeiculo("11.222.333/0001-81", "Locadora Movimenta LTDA", "frota@movimenta.com.br", "1140028922",
                "LOC3D77", "Toyota", "Corolla XEi 2.0", 2023, 2024, "Prata", 18_700),
            "Vibração no volante acima de 90 km/h.",
            cancellationToken);

        await iniciarDiagnostico.ExecutarAsync(emDiagnostico, cancellationToken);
        await registrarDiagnostico.ExecutarAsync(
            emDiagnostico,
            new RegistrarDiagnosticoRequest("Rodas desbalanceadas e pneu dianteiro direito com desgaste irregular."),
            cancellationToken);

        // ── OS recém-recebida, ainda na fila ──────────────────────────────────
        await ReceberAsync(
            new DadosDeVeiculo("05.960.834/0001-62", "Distribuidora Vale Verde LTDA", "manutencao@valeverde.com.br", "1155667788",
                "VVD9E12", "Fiat", "Fiorino Endurance", 2020, 2021, "Branco", 87_300),
            "Motor falhando em marcha lenta.",
            cancellationToken);

        logger.LogInformation(
            "Demonstração semeada: 8 clientes com veículos e 8 Ordens de Serviço "
            + "(3 entregues, 1 em execução, 1 aguardando aprovação, 1 cancelada, 1 em diagnóstico, 1 recebida).");
    }

    // -----------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------

    /// <summary>Dados de recepção de um veículo, agrupados para reduzir ruído nas chamadas.</summary>
    private sealed record DadosDeVeiculo(
        string Documento,
        string Nome,
        string Email,
        string Telefone,
        string Placa,
        string Marca,
        string Modelo,
        int AnoFabricacao,
        int AnoModelo,
        string Cor,
        int Quilometragem);

    private async Task<Guid> ReceberAsync(
        DadosDeVeiculo dados,
        string relatoDoProblema,
        CancellationToken cancellationToken)
    {
        var ordem = await receberVeiculo.ExecutarAsync(
            new ReceberVeiculoRequest(
                dados.Documento,
                dados.Nome,
                dados.Email,
                dados.Telefone,
                dados.Placa,
                dados.Marca,
                dados.Modelo,
                dados.AnoFabricacao,
                dados.AnoModelo,
                dados.Cor,
                relatoDoProblema,
                dados.Quilometragem),
            cancellationToken);

        return ordem.Id;
    }

    private async Task<Guid> AbrirComItensAsync(
        DadosDeVeiculo dados,
        string relatoDoProblema,
        IEnumerable<(ServicoResponse Servico, int Quantidade)> servicos,
        IEnumerable<(PecaResponse Peca, int Quantidade)> pecas,
        CancellationToken cancellationToken)
    {
        var ordemId = await ReceberAsync(dados, relatoDoProblema, cancellationToken);

        await iniciarDiagnostico.ExecutarAsync(ordemId, cancellationToken);

        foreach (var (servico, quantidade) in servicos)
        {
            await adicionarServico.ExecutarAsync(
                ordemId, new AdicionarServicoRequest(servico.Id, quantidade), cancellationToken);
        }

        // Cada inclusão de peça reserva o saldo, exatamente como em produção.
        foreach (var (peca, quantidade) in pecas)
        {
            await adicionarPeca.ExecutarAsync(
                ordemId, new AdicionarPecaRequest(peca.Id, quantidade), cancellationToken);
        }

        return ordemId;
    }

    private async Task<Guid> ExecutarFluxoCompletoAsync(
        DadosDeVeiculo dados,
        string relatoDoProblema,
        IEnumerable<(ServicoResponse Servico, int Quantidade)> servicos,
        IEnumerable<(PecaResponse Peca, int Quantidade)> pecas,
        decimal descontoPercentual,
        CancellationToken cancellationToken)
    {
        var ordemId = await AbrirComItensAsync(dados, relatoDoProblema, servicos, pecas, cancellationToken);

        await registrarDiagnostico.ExecutarAsync(
            ordemId,
            new RegistrarDiagnosticoRequest("Avaliação concluída. Serviços e peças confirmados com o cliente."),
            cancellationToken);

        await gerarOrcamento.ExecutarAsync(
            ordemId, new GerarOrcamentoRequest(descontoPercentual), cancellationToken);

        await enviarOrcamento.ExecutarAsync(ordemId, new EnviarOrcamentoRequest(7), cancellationToken);
        await aprovarOrcamento.ExecutarAsync(ordemId, cancellationToken);
        await finalizarServico.ExecutarAsync(
            ordemId, new FinalizarServicoRequest("Serviços concluídos e testados."), cancellationToken);

        await entregarVeiculo.ExecutarAsync(
            ordemId, new EntregarVeiculoRequest("Veículo entregue e conferido pelo cliente."), cancellationToken);

        return ordemId;
    }

    /// <summary>
    /// Recua no tempo os marcos de uma Ordem de Serviço já entregue, para que o indicador de
    /// tempo médio tenha durações realistas e distribuídas no período.
    /// <para>
    /// O ajuste é feito pelo rastreador de mudanças do EF Core, e não pelo agregado. É
    /// deliberado: o domínio <b>não</b> oferece — e não deve oferecer — como forjar a data de
    /// uma execução, porque isso permitiria falsear o próprio indicador que a oficina usa para
    /// se avaliar. Como o semeador é infraestrutura, ele grava direto no modelo de persistência,
    /// que é o lugar certo para uma carga de demonstração.
    /// </para>
    /// </summary>
    private async Task RetroagirExecucaoAsync(
        Guid ordemId,
        int diasAtras,
        TimeSpan duracao,
        CancellationToken cancellationToken)
    {
        var ordem = await contexto.OrdensDeServico.FirstAsync(o => o.Id == ordemId, cancellationToken);
        var entrada = contexto.Entry(ordem);

        var abertura = relogio.Agora.AddDays(-diasAtras);
        var inicioDaExecucao = abertura.AddHours(2);
        var finalizacao = inicioDaExecucao.Add(duracao);
        var entrega = finalizacao.AddHours(3);

        entrada.Property(o => o.CriadaEm).CurrentValue = abertura;
        entrada.Property(o => o.ExecucaoIniciadaEm).CurrentValue = inicioDaExecucao;
        entrada.Property(o => o.FinalizadaEm).CurrentValue = finalizacao;
        entrada.Property(o => o.EntregueEm).CurrentValue = entrega;
        entrada.Property(o => o.AtualizadaEm).CurrentValue = entrega;

        await contexto.SaveChangesAsync(cancellationToken);
    }

    private static ServicoResponse PorNome(IReadOnlyList<ServicoResponse> servicos, string nome) =>
        servicos.FirstOrDefault(s => s.Nome == nome)
        ?? throw new InvalidOperationException($"Serviço '{nome}' não encontrado no catálogo semeado.");

    private static PecaResponse PorCodigo(IReadOnlyList<PecaResponse> pecas, string codigo) =>
        pecas.FirstOrDefault(p => p.Codigo == codigo)
        ?? throw new InvalidOperationException($"Peça '{codigo}' não encontrada no estoque semeado.");
}
