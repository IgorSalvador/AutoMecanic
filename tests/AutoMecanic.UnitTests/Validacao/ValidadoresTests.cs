using AutoMecanic.Application.Clientes.Dtos;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Application.Validacao;
using AutoMecanic.Application.Veiculos.Dtos;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.Servicos;
using FluentValidation;

namespace AutoMecanic.UnitTests.Validacao;

/// <summary>
/// Os validadores são a primeira barreira da API: transformam entrada malformada em
/// <c>400</c> com a lista completa de campos problemáticos, antes que o caso de uso rode.
/// Eles não substituem as invariantes do domínio — melhoram a mensagem devolvida.
/// </summary>
public sealed class ValidadoresTests
{
    // -----------------------------------------------------------------
    // Cadastros
    // -----------------------------------------------------------------

    [Fact]
    public void ValidadorDeCriarCliente_ComRequisicaoValida_NaoAcusaErro() =>
        Validar(new ValidadorDeCriarCliente(),
            new CriarClienteRequest("Maria Souza", "52998224725", "maria@exemplo.com", "11987654321"))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeCriarCliente_ComVariosCamposInvalidos_AcusaTodosDeUmaVez()
    {
        var erros = Validar(new ValidadorDeCriarCliente(),
            new CriarClienteRequest("", "invalido", "", ""));

        // Corrigir um campo por requisição seria uma péssima experiência de integração.
        erros.Count.ShouldBeGreaterThanOrEqualTo(3);
        erros.ShouldContain(e => e.Contains("Nome"));
        erros.ShouldContain(e => e.Contains("Documento"));
    }

    [Fact]
    public void ValidadorDeCriarCliente_ComDigitoVerificadorErrado_AcusaODocumento() =>
        Validar(new ValidadorDeCriarCliente(),
            new CriarClienteRequest("Maria Souza", "52998224726", "maria@exemplo.com", "11987654321"))
            .ShouldContain(e => e.Contains("Documento"));

    [Fact]
    public void ValidadorDeCriarCliente_ComEnderecoIncompleto_AcusaOsCamposDoEndereco()
    {
        var erros = Validar(new ValidadorDeCriarCliente(),
            new CriarClienteRequest("Maria Souza", "52998224725", "maria@exemplo.com", "11987654321",
                new EnderecoDto("", "", null, "", "", "XX", "123")));

        erros.ShouldContain(e => e.Contains("Endereco"));
    }

    [Fact]
    public void ValidadorDeAtualizarCliente_ComDadosValidos_NaoAcusaErro() =>
        Validar(new ValidadorDeAtualizarCliente(),
            new AtualizarClienteRequest("Maria Souza", "maria@exemplo.com", "11987654321"))
            .ShouldBeEmpty();

    [Theory]
    [InlineData("ABC1234")]
    [InlineData("ABC1D23")]
    public void ValidadorDeCriarVeiculo_ComPlacaValida_NaoAcusaErro(string placa) =>
        Validar(new ValidadorDeCriarVeiculo(),
            new CriarVeiculoRequest(Guid.CreateVersion7(), placa, "Fiat", "Argo", 2022))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeCriarVeiculo_SemCliente_AcusaOCampo() =>
        Validar(new ValidadorDeCriarVeiculo(),
            new CriarVeiculoRequest(Guid.Empty, "ABC1234", "Fiat", "Argo", 2022))
            .ShouldContain(e => e.Contains("ClienteId"));

    [Fact]
    public void ValidadorDeCriarVeiculo_ComAnoNoFuturo_AcusaOCampo() =>
        Validar(new ValidadorDeCriarVeiculo(),
            new CriarVeiculoRequest(Guid.CreateVersion7(), "ABC1234", "Fiat", "Argo", DateTimeOffset.UtcNow.Year + 5))
            .ShouldContain(e => e.Contains("AnoFabricacao"));

    [Fact]
    public void ValidadorDeAtualizarVeiculo_ComDadosValidos_NaoAcusaErro() =>
        Validar(new ValidadorDeAtualizarVeiculo(),
            new AtualizarVeiculoRequest("Fiat", "Argo", 2022, 2023, "Prata"))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeRegistrarQuilometragem_ComValorNegativo_AcusaOCampo() =>
        Validar(new ValidadorDeRegistrarQuilometragem(), new RegistrarQuilometragemRequest(-1))
            .ShouldNotBeEmpty();

    // -----------------------------------------------------------------
    // Catálogo e estoque
    // -----------------------------------------------------------------

    [Fact]
    public void ValidadorDeCriarServico_ComDadosValidos_NaoAcusaErro() =>
        Validar(new ValidadorDeCriarServico(),
            new CriarServicoRequest("Troca de óleo", "Óleo e filtro", CategoriaServico.ManutencaoPreventiva, 120m, 45))
            .ShouldBeEmpty();

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ValidadorDeCriarServico_ComPrecoNaoPositivo_AcusaOCampo(decimal preco) =>
        Validar(new ValidadorDeCriarServico(),
            new CriarServicoRequest("Serviço", null, CategoriaServico.Outros, preco, 30))
            .ShouldContain(e => e.Contains("Preco"));

    [Fact]
    public void ValidadorDeCriarServico_ComCategoriaInexistente_AcusaOCampo() =>
        Validar(new ValidadorDeCriarServico(),
            new CriarServicoRequest("Serviço", null, (CategoriaServico)999, 100m, 30))
            .ShouldContain(e => e.Contains("Categoria"));

    [Fact]
    public void ValidadorDeAtualizarServico_ComTempoZero_AcusaOCampo() =>
        Validar(new ValidadorDeAtualizarServico(),
            new AtualizarServicoRequest("Serviço", null, CategoriaServico.Outros, 0))
            .ShouldContain(e => e.Contains("TempoEstimado"));

    [Fact]
    public void ValidadorDeReajustarPreco_ComValorNaoPositivo_AcusaOCampo() =>
        Validar(new ValidadorDeReajustarPreco(), new ReajustarPrecoRequest(0m))
            .ShouldNotBeEmpty();

    [Fact]
    public void ValidadorDeCriarPeca_ComDadosValidos_NaoAcusaErro() =>
        Validar(new ValidadorDeCriarPeca(),
            new CriarPecaRequest("OL-5W30", "Óleo", null, UnidadeMedida.Litro, 48.90m, 10, 2))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeCriarPeca_SemCodigo_AcusaOCampo() =>
        Validar(new ValidadorDeCriarPeca(),
            new CriarPecaRequest("", "Óleo", null, UnidadeMedida.Litro, 48.90m, 10, 2))
            .ShouldContain(e => e.Contains("Codigo"));

    [Fact]
    public void ValidadorDeAtualizarPeca_ComEstoqueMinimoNegativo_AcusaOCampo() =>
        Validar(new ValidadorDeAtualizarPeca(),
            new AtualizarPecaRequest("Óleo", null, UnidadeMedida.Litro, -1))
            .ShouldContain(e => e.Contains("EstoqueMinimo"));

    [Fact]
    public void ValidadorDeRegistrarEntrada_SemMotivo_AcusaOCampo() =>
        Validar(new ValidadorDeRegistrarEntrada(), new RegistrarEntradaRequest(10, ""))
            .ShouldContain(e => e.Contains("Motivo"));

    [Fact]
    public void ValidadorDeRegistrarEntrada_ComQuantidadeZero_AcusaOCampo() =>
        Validar(new ValidadorDeRegistrarEntrada(), new RegistrarEntradaRequest(0, "NF 123"))
            .ShouldContain(e => e.Contains("Quantidade"));

    [Fact]
    public void ValidadorDeRegistrarPerda_ComDadosValidos_NaoAcusaErro() =>
        Validar(new ValidadorDeRegistrarPerda(), new RegistrarPerdaRequest(2, "Avaria no transporte"))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeAjustarEstoque_SemMotivo_AcusaOCampo() =>
        Validar(new ValidadorDeAjustarEstoque(), new AjustarEstoqueRequest(10, " "))
            .ShouldContain(e => e.Contains("Motivo"));

    // -----------------------------------------------------------------
    // Ordem de Serviço
    // -----------------------------------------------------------------

    [Fact]
    public void ValidadorDeAbrirOrdemServico_ComDadosValidos_NaoAcusaErro() =>
        Validar(new ValidadorDeAbrirOrdemServico(),
            new AbrirOrdemServicoRequest(Guid.CreateVersion7(), Guid.CreateVersion7(), "Barulho ao frear", 50_000))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeAbrirOrdemServico_SemRelatoDoProblema_AcusaOCampo() =>
        Validar(new ValidadorDeAbrirOrdemServico(),
            new AbrirOrdemServicoRequest(Guid.CreateVersion7(), Guid.CreateVersion7(), ""))
            .ShouldContain(e => e.Contains("DescricaoProblema"));

    [Fact]
    public void ValidadorDeReceberVeiculo_ComDocumentoEPlacaValidos_NaoAcusaErro() =>
        Validar(new ValidadorDeReceberVeiculo(),
            new ReceberVeiculoRequest("52998224725", "Maria", "m@e.com", "11987654321",
                "ABC1D23", "VW", "Gol", 2020, 2021, "Branco", "Barulho", 50_000))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeReceberVeiculo_ComDocumentoEPlacaInvalidos_AcusaOsDois()
    {
        var erros = Validar(new ValidadorDeReceberVeiculo(),
            new ReceberVeiculoRequest("00000000000", null, null, null,
                "PLACA-RUIM", null, null, null, null, null, "Barulho", null));

        erros.ShouldContain(e => e.Contains("DocumentoCliente"));
        erros.ShouldContain(e => e.Contains("Placa"));
    }

    [Fact]
    public void ValidadorDeRegistrarDiagnostico_ComTextoVazio_AcusaOCampo() =>
        Validar(new ValidadorDeRegistrarDiagnostico(), new RegistrarDiagnosticoRequest(""))
            .ShouldNotBeEmpty();

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void ValidadorDeAdicionarServico_ComQuantidadeForaDaFaixa_AcusaOCampo(int quantidade) =>
        Validar(new ValidadorDeAdicionarServico(), new AdicionarServicoRequest(Guid.CreateVersion7(), quantidade))
            .ShouldContain(e => e.Contains("Quantidade"));

    [Fact]
    public void ValidadorDeAdicionarPeca_SemPeca_AcusaOCampo() =>
        Validar(new ValidadorDeAdicionarPeca(), new AdicionarPecaRequest(Guid.Empty, 1))
            .ShouldContain(e => e.Contains("PecaId"));

    [Fact]
    public void ValidadorDeAlterarQuantidade_ComZero_AcusaOCampo() =>
        Validar(new ValidadorDeAlterarQuantidade(), new AlterarQuantidadeRequest(0))
            .ShouldNotBeEmpty();

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ValidadorDeGerarOrcamento_ComDescontoForaDaFaixa_AcusaOCampo(decimal percentual) =>
        Validar(new ValidadorDeGerarOrcamento(), new GerarOrcamentoRequest(percentual))
            .ShouldNotBeEmpty();

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void ValidadorDeEnviarOrcamento_ComValidadeForaDaFaixa_AcusaOCampo(int dias) =>
        Validar(new ValidadorDeEnviarOrcamento(), new EnviarOrcamentoRequest(dias))
            .ShouldNotBeEmpty();

    [Fact]
    public void ValidadorDeCancelarOrdemServico_SemMotivo_AcusaOCampo() =>
        Validar(new ValidadorDeCancelarOrdemServico(), new CancelarOrdemServicoRequest(""))
            .ShouldContain(e => e.Contains("Motivo"));

    // -----------------------------------------------------------------
    // Identidade
    // -----------------------------------------------------------------

    [Fact]
    public void ValidadorDeLogin_ComCredenciaisPreenchidas_NaoAcusaErro() =>
        Validar(new ValidadorDeLogin(), new LoginRequest("admin@automecanic.com.br", "Senha@Forte1"))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeLogin_SemSenha_AcusaOCampo() =>
        Validar(new ValidadorDeLogin(), new LoginRequest("admin@automecanic.com.br", ""))
            .ShouldContain(e => e.Contains("Senha"));

    [Theory]
    [InlineData("curta1!")]
    [InlineData("semmaiuscula1!")]
    [InlineData("SEMMINUSCULA1!")]
    [InlineData("SemDigito!!")]
    [InlineData("SemSimbolo123")]
    public void ValidadorDeCriarUsuario_ComSenhaForaDaPolitica_AcusaOCampo(string senha) =>
        Validar(new ValidadorDeCriarUsuario(),
            new CriarUsuarioRequest("Fulano de Tal", "f@e.com", senha, PerfilUsuario.Atendente))
            .ShouldContain(e => e.Contains("Senha"));

    [Fact]
    public void ValidadorDeCriarUsuario_ComSenhaForte_NaoAcusaErro() =>
        Validar(new ValidadorDeCriarUsuario(),
            new CriarUsuarioRequest("Fulano de Tal", "fulano@automecanic.com.br", "Senha@Forte1", PerfilUsuario.Atendente))
            .ShouldBeEmpty();

    [Fact]
    public void ValidadorDeAlterarSenha_SemSenhaAtual_AcusaOCampo() =>
        Validar(new ValidadorDeAlterarSenha(), new AlterarSenhaRequest("", "Nova@Senha1"))
            .ShouldContain(e => e.Contains("SenhaAtual"));

    [Fact]
    public void ValidadorDeRedefinirSenha_ComSenhaFraca_AcusaOCampo() =>
        Validar(new ValidadorDeRedefinirSenha(), new RedefinirSenhaRequest("123456"))
            .ShouldNotBeEmpty();

    /// <summary>Executa o validador e devolve os erros como "Campo: mensagem".</summary>
    private static List<string> Validar<T>(IValidator<T> validador, T instancia) =>
        [.. validador.Validate(instancia).Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")];
}
