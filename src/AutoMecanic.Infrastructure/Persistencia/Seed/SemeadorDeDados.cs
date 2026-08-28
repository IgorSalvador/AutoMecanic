using AutoMecanic.Application.Abstractions;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.Servicos;
using AutoMecanic.Domain.Veiculos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Infrastructure.Persistencia.Seed;

/// <summary>
/// Popula o banco com o mínimo necessário para a aplicação ser utilizável logo após subir:
/// um usuário administrador, o catálogo básico de serviços e um estoque inicial de peças.
/// <para>
/// É <b>idempotente</b>: cada bloco só executa se a respectiva tabela estiver vazia, de modo
/// que reiniciar o contêiner não duplica dados nem sobrescreve alterações feitas pela oficina.
/// </para>
/// </summary>
public sealed class SemeadorDeDados(
    AutoMecanicDbContext contexto,
    IServicoDeHashDeSenha hasher,
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
    /// Quando verdadeiro, cria também clientes, veículos e uma Ordem de Serviço de exemplo.
    /// Destinado a ambiente de desenvolvimento e à avaliação do projeto.
    /// </param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task ExecutarAsync(
        string senhaDoAdministrador,
        bool incluirDadosDeDemonstracao,
        CancellationToken cancellationToken = default)
    {
        await SemearUsuarioAdministradorAsync(senhaDoAdministrador, cancellationToken);
        await SemearCatalogoDeServicosAsync(cancellationToken);
        await SemearEstoqueAsync(cancellationToken);

        if (incluirDadosDeDemonstracao)
        {
            await SemearClientesEVeiculosAsync(cancellationToken);
        }

        await contexto.SaveChangesAsync(cancellationToken);
    }

    private async Task SemearUsuarioAdministradorAsync(string senha, CancellationToken cancellationToken)
    {
        if (await contexto.Usuarios.AnyAsync(cancellationToken))
        {
            return;
        }

        var administrador = Usuario.Criar(
            "Administrador do Sistema",
            "admin@automecanic.com.br",
            senha,
            PerfilUsuario.Administrador,
            hasher.GerarHash);

        administrador.LimparEventos();

        await contexto.Usuarios.AddAsync(administrador, cancellationToken);

        logger.LogInformation("Usuário administrador padrão criado: {Email}", administrador.Email.Endereco);
    }

    private async Task SemearCatalogoDeServicosAsync(CancellationToken cancellationToken)
    {
        if (await contexto.Servicos.AnyAsync(cancellationToken))
        {
            return;
        }

        var servicos = new[]
        {
            Servico.Cadastrar("Troca de óleo do motor", "Troca de óleo lubrificante e filtro de óleo.", CategoriaServico.ManutencaoPreventiva, 120.00m, 45),
            Servico.Cadastrar("Alinhamento e balanceamento", "Alinhamento de direção e balanceamento das quatro rodas.", CategoriaServico.Suspensao, 180.00m, 60),
            Servico.Cadastrar("Revisão de freios", "Inspeção de pastilhas, discos, fluido e cilindros.", CategoriaServico.Suspensao, 250.00m, 90),
            Servico.Cadastrar("Troca de pastilhas de freio", "Substituição do jogo de pastilhas dianteiras ou traseiras.", CategoriaServico.Suspensao, 160.00m, 75),
            Servico.Cadastrar("Diagnóstico eletrônico", "Leitura de códigos de falha via scanner automotivo.", CategoriaServico.Diagnostico, 150.00m, 40),
            Servico.Cadastrar("Troca de correia dentada", "Substituição da correia dentada e tensionador.", CategoriaServico.ManutencaoPreventiva, 680.00m, 240),
            Servico.Cadastrar("Revisão do sistema elétrico", "Teste de bateria, alternador e motor de partida.", CategoriaServico.Eletrica, 190.00m, 70),
            Servico.Cadastrar("Higienização do ar-condicionado", "Limpeza do evaporador e troca do filtro de cabine.", CategoriaServico.ManutencaoPreventiva, 210.00m, 80),
            Servico.Cadastrar("Troca de embreagem", "Substituição do kit de embreagem completo.", CategoriaServico.ManutencaoCorretiva, 1450.00m, 420),
            Servico.Cadastrar("Polimento e cristalização", "Correção de pintura com polimento técnico.", CategoriaServico.Funilaria, 550.00m, 300)
        };

        foreach (var servico in servicos)
        {
            servico.LimparEventos();
        }

        await contexto.Servicos.AddRangeAsync(servicos, cancellationToken);

        logger.LogInformation("Catálogo semeado com {Total} serviços.", servicos.Length);
    }

    private async Task SemearEstoqueAsync(CancellationToken cancellationToken)
    {
        if (await contexto.Pecas.AnyAsync(cancellationToken))
        {
            return;
        }

        var pecas = new[]
        {
            Peca.Cadastrar("OL-5W30-1L", "Óleo sintético 5W30", "Óleo lubrificante sintético para motor, 1 litro.", UnidadeMedida.Litro, 48.90m, 120, 24),
            Peca.Cadastrar("FIL-OLEO-001", "Filtro de óleo", "Filtro de óleo para motores 1.0 a 1.6.", UnidadeMedida.Unidade, 32.50m, 60, 15),
            Peca.Cadastrar("FIL-AR-001", "Filtro de ar do motor", "Elemento filtrante de ar.", UnidadeMedida.Unidade, 45.00m, 40, 10),
            Peca.Cadastrar("FIL-CABINE-001", "Filtro de cabine", "Filtro de ar-condicionado com carvão ativado.", UnidadeMedida.Unidade, 58.00m, 35, 10),
            Peca.Cadastrar("PAST-FRE-DIA", "Pastilha de freio dianteira", "Jogo de pastilhas dianteiras cerâmicas.", UnidadeMedida.Jogo, 189.90m, 25, 6),
            Peca.Cadastrar("PAST-FRE-TRA", "Pastilha de freio traseira", "Jogo de pastilhas traseiras.", UnidadeMedida.Jogo, 164.90m, 20, 6),
            Peca.Cadastrar("DISC-FRE-DIA", "Disco de freio dianteiro", "Par de discos ventilados.", UnidadeMedida.Jogo, 420.00m, 12, 4),
            Peca.Cadastrar("VELA-IGN-001", "Vela de ignição", "Vela de ignição de níquel.", UnidadeMedida.Unidade, 28.00m, 80, 20),
            Peca.Cadastrar("BAT-60AH", "Bateria 60Ah", "Bateria automotiva selada, 60 amperes-hora.", UnidadeMedida.Unidade, 549.00m, 8, 3),
            Peca.Cadastrar("COR-DENT-001", "Correia dentada", "Correia dentada com tensionador.", UnidadeMedida.Jogo, 380.00m, 10, 3),
            Peca.Cadastrar("FLU-FREIO-DOT4", "Fluido de freio DOT 4", "Fluido de freio DOT 4, 500 ml.", UnidadeMedida.Frasco, 39.90m, 30, 8),
            Peca.Cadastrar("ADIT-RAD-1L", "Aditivo de radiador", "Aditivo orgânico concentrado, 1 litro.", UnidadeMedida.Litro, 42.00m, 25, 8)
        };

        foreach (var peca in pecas)
        {
            peca.LimparEventos();
        }

        await contexto.Pecas.AddRangeAsync(pecas, cancellationToken);

        logger.LogInformation("Estoque semeado com {Total} peças.", pecas.Length);
    }

    private async Task SemearClientesEVeiculosAsync(CancellationToken cancellationToken)
    {
        if (await contexto.Clientes.AnyAsync(cancellationToken))
        {
            return;
        }

        // Documentos e placas fictícios, porém válidos segundo as regras de formação —
        // é isso que permite exercitar os fluxos completos sem burlar as validações.
        var maria = Cliente.Cadastrar("Maria Aparecida Souza", "52998224725", "maria.souza@exemplo.com.br", "11987654321");
        var joao = Cliente.Cadastrar("João Carlos Pereira", "16899535009", "joao.pereira@exemplo.com.br", "21976543210");
        var transportadora = Cliente.Cadastrar("Transportadora Rota Certa LTDA", "34028316000103", "contato@rotacerta.com.br", "1133224455");

        var veiculos = new[]
        {
            Veiculo.Cadastrar(maria.Id, "ABC1D23", "Volkswagen", "Gol 1.0", 2019, 2020, "Branco", 68_500),
            Veiculo.Cadastrar(joao.Id, "XYZ4567", "Fiat", "Argo Drive 1.3", 2021, 2021, "Prata", 41_200),
            Veiculo.Cadastrar(transportadora.Id, "BRA2E19", "Mercedes-Benz", "Sprinter 416", 2022, 2023, "Branco", 112_800)
        };

        foreach (var cliente in new[] { maria, joao, transportadora })
        {
            cliente.LimparEventos();
        }

        foreach (var veiculo in veiculos)
        {
            veiculo.LimparEventos();
        }

        await contexto.Clientes.AddRangeAsync([maria, joao, transportadora], cancellationToken);
        await contexto.Veiculos.AddRangeAsync(veiculos, cancellationToken);

        logger.LogInformation("Dados de demonstração criados: 3 clientes e 3 veículos.");
    }
}
