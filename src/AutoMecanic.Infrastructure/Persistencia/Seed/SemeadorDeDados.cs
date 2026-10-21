using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Estoque;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.Identidade;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Application.Servicos;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
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
    SemeadorDeOrdensDeServico semeadorDeOrdensDeServico,
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

        await semeadorDeOrdensDeServico.ExecutarAsync(servicos, pecas, cancellationToken);
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

    private async Task<IReadOnlyList<ServicoResponse>> CarregarServicosExistentesAsync(CancellationToken cancellationToken) =>
        [.. (await contexto.Servicos.AsNoTracking().ToListAsync(cancellationToken)).Select(ServicoResponse.De)];

    private async Task<IReadOnlyList<PecaResponse>> CarregarPecasExistentesAsync(CancellationToken cancellationToken) =>
        [.. (await contexto.Pecas.AsNoTracking().ToListAsync(cancellationToken)).Select(PecaResponse.De)];
}
