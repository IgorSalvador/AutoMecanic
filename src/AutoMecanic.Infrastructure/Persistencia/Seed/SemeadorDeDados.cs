using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Estoque;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Application.OrdensServico;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Application.Servicos;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.Servicos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Infrastructure.Persistencia.Seed;

/// <summary>
/// Popula o banco com o necessário para a aplicação ser utilizável logo após subir: o usuário
/// administrador, o catálogo de serviços e o estoque inicial — e, em modo de demonstração,
/// clientes, veículos e Ordens de Serviço distribuídas pelas várias situações do ciclo de vida.
/// <para>
/// <b>A carga é feita pelos serviços de aplicação, não montando agregados à mão.</b> Isso
/// significa que os dados de demonstração percorrem exatamente o mesmo caminho dos dados
/// reais: as mesmas validações, a mesma coordenação entre Ordem de Serviço e Estoque, os
/// mesmos eventos de domínio. Um seed que monta o estado diretamente consegue produzir
/// combinações que a aplicação jamais criaria — e é assim que se demonstra um sistema com
/// dados que o próprio sistema recusaria.
/// </para>
/// <para>
/// É <b>idempotente</b>: cada bloco só executa se a respectiva tabela estiver vazia, de modo
/// que reiniciar o contêiner não duplica dados nem sobrescreve alterações feitas pela oficina.
/// </para>
/// </summary>
public sealed class SemeadorDeDados(
    AutoMecanicDbContext contexto,
    IServicoDeUsuarios servicoDeUsuarios,
    IServicoDeCatalogo servicoDeCatalogo,
    IServicoDeEstoque servicoDeEstoque,
    IServicoDeOrdensServico servicoDeOrdensServico,
    ILogger<SemeadorDeDados> logger)
{
    /// <summary>
    /// Executa a carga inicial.
    /// </summary>
    /// <param name="senhaDoAdministrador">
    /// Senha do administrador padrão, fornecida por configuração. Nunca embutida no código:
    /// uma senha fixa no repositório seria conhecida por qualquer pessoa com acesso ao código.
    /// </param>
    /// <param name="incluirDadosDeDemonstracao">
    /// Quando verdadeiro, cria também clientes, veículos e Ordens de Serviço de exemplo.
    /// Destinado a ambiente de desenvolvimento e à avaliação do projeto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task ExecutarAsync(
        string senhaDoAdministrador,
        bool incluirDadosDeDemonstracao,
        CancellationToken cancellationToken = default)
    {
        await SemearUsuarioAdministradorAsync(senhaDoAdministrador, cancellationToken);

        var servicos = await SemearCatalogoDeServicosAsync(cancellationToken);
        var pecas = await SemearEstoqueAsync(cancellationToken);

        if (!incluirDadosDeDemonstracao)
        {
            return;
        }

        if (await contexto.OrdensDeServico.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Dados de demonstração já existem; nada a semear.");
            return;
        }

        await SemearOrdensDeServicoAsync(servicos, pecas, cancellationToken);
    }

    // -----------------------------------------------------------------
    // Usuário administrador
    // -----------------------------------------------------------------

    private async Task SemearUsuarioAdministradorAsync(string senha, CancellationToken cancellationToken)
    {
        if (await contexto.Usuarios.AnyAsync(cancellationToken))
        {
            return;
        }

        var administrador = await servicoDeUsuarios.CriarAsync(
            new CriarUsuarioRequest(
                "Administrador do Sistema",
                "admin@automecanic.com.br",
                senha,
                PerfilUsuario.Administrador),
            cancellationToken);

        logger.LogInformation("Usuário administrador padrão criado: {Email}", administrador.Email);
    }

    // -----------------------------------------------------------------
    // Catálogo de serviços
    // -----------------------------------------------------------------

    private async Task<IReadOnlyList<ServicoResponse>> SemearCatalogoDeServicosAsync(CancellationToken cancellationToken)
    {
        if (await contexto.Servicos.AnyAsync(cancellationToken))
        {
            return await CarregarServicosExistentesAsync(cancellationToken);
        }

        CriarServicoRequest[] catalogo =
        [
            new("Troca de óleo do motor", "Troca de óleo lubrificante e filtro de óleo.", CategoriaServico.ManutencaoPreventiva, 120.00m, 45),
            new("Alinhamento e balanceamento", "Alinhamento de direção e balanceamento das quatro rodas.", CategoriaServico.Suspensao, 180.00m, 60),
            new("Revisão de freios", "Inspeção de pastilhas, discos, fluido e cilindros.", CategoriaServico.Suspensao, 250.00m, 90),
            new("Troca de pastilhas de freio", "Substituição do jogo de pastilhas dianteiras ou traseiras.", CategoriaServico.Suspensao, 160.00m, 75),
            new("Diagnóstico eletrônico", "Leitura de códigos de falha via scanner automotivo.", CategoriaServico.Diagnostico, 150.00m, 40),
            new("Troca de correia dentada", "Substituição da correia dentada e tensionador.", CategoriaServico.ManutencaoPreventiva, 680.00m, 240),
            new("Revisão do sistema elétrico", "Teste de bateria, alternador e motor de partida.", CategoriaServico.Eletrica, 190.00m, 70),
            new("Higienização do ar-condicionado", "Limpeza do evaporador e troca do filtro de cabine.", CategoriaServico.ManutencaoPreventiva, 210.00m, 80),
            new("Troca de embreagem", "Substituição do kit de embreagem completo.", CategoriaServico.ManutencaoCorretiva, 1450.00m, 420),
            new("Polimento e cristalização", "Correção de pintura com polimento técnico.", CategoriaServico.Funilaria, 550.00m, 300)
        ];

        var criados = new List<ServicoResponse>(catalogo.Length);

        foreach (var servico in catalogo)
        {
            criados.Add(await servicoDeCatalogo.CadastrarAsync(servico, cancellationToken));
        }

        logger.LogInformation("Catálogo semeado com {Total} serviços.", criados.Count);

        return criados;
    }

    // -----------------------------------------------------------------
    // Estoque
    // -----------------------------------------------------------------

    private async Task<IReadOnlyList<PecaResponse>> SemearEstoqueAsync(CancellationToken cancellationToken)
    {
        if (await contexto.Pecas.AnyAsync(cancellationToken))
        {
            return await CarregarPecasExistentesAsync(cancellationToken);
        }

        // A bateria nasce abaixo do ponto de ressuprimento de propósito: sem nenhuma peça
        // em nível crítico, o endpoint de alertas de compra devolveria uma lista vazia e o
        // recurso ficaria indemonstrável logo após subir o ambiente.
        CriarPecaRequest[] almoxarifado =
        [
            new("OL-5W30-1L", "Óleo sintético 5W30", "Óleo lubrificante sintético para motor, 1 litro.", UnidadeMedida.Litro, 48.90m, 120, 24),
            new("FIL-OLEO-001", "Filtro de óleo", "Filtro de óleo para motores 1.0 a 1.6.", UnidadeMedida.Unidade, 32.50m, 60, 15),
            new("FIL-AR-001", "Filtro de ar do motor", "Elemento filtrante de ar.", UnidadeMedida.Unidade, 45.00m, 40, 10),
            new("FIL-CABINE-001", "Filtro de cabine", "Filtro de ar-condicionado com carvão ativado.", UnidadeMedida.Unidade, 58.00m, 35, 10),
            new("PAST-FRE-DIA", "Pastilha de freio dianteira", "Jogo de pastilhas dianteiras cerâmicas.", UnidadeMedida.Jogo, 189.90m, 25, 6),
            new("PAST-FRE-TRA", "Pastilha de freio traseira", "Jogo de pastilhas traseiras.", UnidadeMedida.Jogo, 164.90m, 20, 6),
            new("DISC-FRE-DIA", "Disco de freio dianteiro", "Par de discos ventilados.", UnidadeMedida.Jogo, 420.00m, 12, 4),
            new("VELA-IGN-001", "Vela de ignição", "Vela de ignição de níquel.", UnidadeMedida.Unidade, 28.00m, 80, 20),
            new("BAT-60AH", "Bateria 60Ah", "Bateria automotiva selada, 60 amperes-hora.", UnidadeMedida.Unidade, 549.00m, 2, 5),
            new("COR-DENT-001", "Correia dentada", "Correia dentada com tensionador.", UnidadeMedida.Jogo, 380.00m, 10, 3),
            new("FLU-FREIO-DOT4", "Fluido de freio DOT 4", "Fluido de freio DOT 4, 500 ml.", UnidadeMedida.Frasco, 39.90m, 30, 8),
            new("ADIT-RAD-1L", "Aditivo de radiador", "Aditivo orgânico concentrado, 1 litro.", UnidadeMedida.Litro, 42.00m, 25, 8)
        ];

        var criadas = new List<PecaResponse>(almoxarifado.Length);

        foreach (var peca in almoxarifado)
        {
            criadas.Add(await servicoDeEstoque.CadastrarAsync(peca, cancellationToken));
        }

        logger.LogInformation("Estoque semeado com {Total} peças.", criadas.Count);

        return criadas;
    }

    // -----------------------------------------------------------------
    // Ordens de Serviço de demonstração
    // -----------------------------------------------------------------

    /// <summary>
    /// Cria Ordens de Serviço cobrindo todas as situações do ciclo de vida, para que o painel
    /// operacional, o indicador de tempo médio e a posição de estoque tenham conteúdo real
    /// assim que o ambiente sobe — sem que o avaliador precise executar o fluxo à mão antes
    /// de ver qualquer número.
    /// </summary>
    private async Task SemearOrdensDeServicoAsync(
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

        await servicoDeOrdensServico.GerarOrcamentoAsync(emExecucao, new GerarOrcamentoRequest(10m), cancellationToken);
        await servicoDeOrdensServico.EnviarOrcamentoAsync(emExecucao, new EnviarOrcamentoRequest(7), cancellationToken);
        await servicoDeOrdensServico.AprovarOrcamentoAsync(emExecucao, cancellationToken);

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

        await servicoDeOrdensServico.GerarOrcamentoAsync(aguardando, new GerarOrcamentoRequest(0m), cancellationToken);
        await servicoDeOrdensServico.EnviarOrcamentoAsync(aguardando, new EnviarOrcamentoRequest(7), cancellationToken);

        // ── OS cancelada por reprovação do orçamento ──────────────────────────
        var reprovada = await AbrirComItensAsync(
            new DadosDeVeiculo("045.781.236-26", "Fernanda Lima Castro", "fernanda.castro@exemplo.com.br", "51993334444",
                "GHT8B45", "Renault", "Kwid Zen", 2021, 2022, "Vermelho", 33_100),
            "Embreagem patinando em subida.",
            [(PorNome(servicos, "Troca de embreagem"), 1)],
            [],
            cancellationToken);

        await servicoDeOrdensServico.GerarOrcamentoAsync(reprovada, new GerarOrcamentoRequest(0m), cancellationToken);
        await servicoDeOrdensServico.EnviarOrcamentoAsync(reprovada, new EnviarOrcamentoRequest(7), cancellationToken);
        await servicoDeOrdensServico.ReprovarOrcamentoAsync(
            reprovada,
            new ReprovarOrcamentoRequest("Valor acima do orçado. Vou pesquisar outras oficinas."),
            cancellationToken);

        // ── OS em diagnóstico ─────────────────────────────────────────────────
        var emDiagnostico = await ReceberAsync(
            new DadosDeVeiculo("11.222.333/0001-81", "Locadora Movimenta LTDA", "frota@movimenta.com.br", "1140028922",
                "LOC3D77", "Toyota", "Corolla XEi 2.0", 2023, 2024, "Prata", 18_700),
            "Vibração no volante acima de 90 km/h.",
            cancellationToken);

        await servicoDeOrdensServico.IniciarDiagnosticoAsync(emDiagnostico, cancellationToken);
        await servicoDeOrdensServico.RegistrarDiagnosticoAsync(
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
        var ordem = await servicoDeOrdensServico.ReceberVeiculoAsync(
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

        await servicoDeOrdensServico.IniciarDiagnosticoAsync(ordemId, cancellationToken);

        foreach (var (servico, quantidade) in servicos)
        {
            await servicoDeOrdensServico.AdicionarServicoAsync(
                ordemId, new AdicionarServicoRequest(servico.Id, quantidade), cancellationToken);
        }

        // Cada inclusão de peça reserva o saldo, exatamente como em produção.
        foreach (var (peca, quantidade) in pecas)
        {
            await servicoDeOrdensServico.AdicionarPecaAsync(
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

        await servicoDeOrdensServico.RegistrarDiagnosticoAsync(
            ordemId,
            new RegistrarDiagnosticoRequest("Avaliação concluída. Serviços e peças confirmados com o cliente."),
            cancellationToken);

        await servicoDeOrdensServico.GerarOrcamentoAsync(
            ordemId, new GerarOrcamentoRequest(descontoPercentual), cancellationToken);

        await servicoDeOrdensServico.EnviarOrcamentoAsync(ordemId, new EnviarOrcamentoRequest(7), cancellationToken);
        await servicoDeOrdensServico.AprovarOrcamentoAsync(ordemId, cancellationToken);
        await servicoDeOrdensServico.FinalizarServicoAsync(
            ordemId, new FinalizarServicoRequest("Serviços concluídos e testados."), cancellationToken);

        await servicoDeOrdensServico.EntregarVeiculoAsync(
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

        var abertura = DateTimeOffset.UtcNow.AddDays(-diasAtras);
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

    private async Task<IReadOnlyList<ServicoResponse>> CarregarServicosExistentesAsync(CancellationToken cancellationToken) =>
        [.. (await contexto.Servicos.AsNoTracking().ToListAsync(cancellationToken)).Select(ServicoResponse.De)];

    private async Task<IReadOnlyList<PecaResponse>> CarregarPecasExistentesAsync(CancellationToken cancellationToken) =>
        [.. (await contexto.Pecas.AsNoTracking().ToListAsync(cancellationToken)).Select(PecaResponse.De)];

    private static ServicoResponse PorNome(IReadOnlyList<ServicoResponse> servicos, string nome) =>
        servicos.FirstOrDefault(s => s.Nome == nome)
        ?? throw new InvalidOperationException($"Serviço '{nome}' não encontrado no catálogo semeado.");

    private static PecaResponse PorCodigo(IReadOnlyList<PecaResponse> pecas, string codigo) =>
        pecas.FirstOrDefault(p => p.Codigo == codigo)
        ?? throw new InvalidOperationException($"Peça '{codigo}' não encontrada no estoque semeado.");
}
