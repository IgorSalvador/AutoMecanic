using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Domain.Veiculos.Events;

namespace AutoMecanic.UnitTests.Dominio.Agregados;

public sealed class VeiculoTests
{
    private static readonly Guid ClienteId = Guid.CreateVersion7();

    [Fact]
    public void Cadastrar_ComDadosValidos_CriaVeiculoAtivoEPublicaEvento()
    {
        var veiculo = Veiculo.Cadastrar(ClienteId, "ABC1D23", "Volkswagen", "Gol", 2020, 2021, "Branco", 50_000);

        veiculo.ClienteId.ShouldBe(ClienteId);
        veiculo.Placa.Valor.ShouldBe("ABC1D23");
        veiculo.AnoModelo.ShouldBe(2021);
        veiculo.Quilometragem.ShouldBe(50_000);
        veiculo.Ativo.ShouldBeTrue();

        veiculo.EventosDeDominio.OfType<VeiculoCadastrado>().ShouldHaveSingleItem()
            .Placa.ShouldBe("ABC1D23");
    }

    [Fact]
    public void Cadastrar_SemAnoModelo_AssumeOAnoDeFabricacao() =>
        Veiculo.Cadastrar(ClienteId, "ABC1234", "Fiat", "Argo", 2022).AnoModelo.ShouldBe(2022);

    [Fact]
    public void Cadastrar_SemCliente_Rejeita() =>
        Should.Throw<DomainException>(() => Veiculo.Cadastrar(Guid.Empty, "ABC1234", "Fiat", "Argo", 2022))
            .Codigo.ShouldBe("CLIENTE_OBRIGATORIO");

    [Theory]
    [InlineData(1899)]
    [InlineData(1800)]
    public void Cadastrar_ComAnoAnteriorAoMinimo_Rejeita(int ano) =>
        Should.Throw<DomainException>(() => Veiculo.Cadastrar(ClienteId, "ABC1234", "Fiat", "Argo", ano))
            .Codigo.ShouldBe("ANO_INVALIDO");

    [Fact]
    public void Cadastrar_ComAnoNoFuturoDistante_Rejeita() =>
        Should.Throw<DomainException>(() =>
            Veiculo.Cadastrar(ClienteId, "ABC1234", "Fiat", "Argo", DateTimeOffset.UtcNow.Year + 5))
            .Codigo.ShouldBe("ANO_INVALIDO");

    [Fact]
    public void Cadastrar_ComAnoModeloIncoerente_Rejeita() =>
        // O ano-modelo acompanha o de fabricação ou é o seguinte; 2 anos depois é erro de cadastro.
        Should.Throw<DomainException>(() =>
            Veiculo.Cadastrar(ClienteId, "ABC1234", "Fiat", "Argo", 2020, 2023))
            .Codigo.ShouldBe("ANO_MODELO_INVALIDO");

    [Theory]
    [InlineData(-1)]
    [InlineData(3_000_001)]
    public void Cadastrar_ComQuilometragemImplausivel_Rejeita(int km) =>
        Should.Throw<DomainException>(() =>
            Veiculo.Cadastrar(ClienteId, "ABC1234", "Fiat", "Argo", 2022, null, null, km))
            .Codigo.ShouldBe("QUILOMETRAGEM_INVALIDA");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Cadastrar_SemMarca_Rejeita(string? marca) =>
        Should.Throw<DomainException>(() => Veiculo.Cadastrar(ClienteId, "ABC1234", marca, "Argo", 2022))
            .Codigo.ShouldBe("CAMPO_OBRIGATORIO");

    [Fact]
    public void RegistrarQuilometragem_ComValorMaior_AtualizaEPublicaEvento()
    {
        var veiculo = CriarVeiculo(quilometragem: 50_000);
        veiculo.LimparEventos();

        veiculo.RegistrarQuilometragem(55_000);

        veiculo.Quilometragem.ShouldBe(55_000);

        var evento = veiculo.EventosDeDominio.OfType<QuilometragemAtualizada>().ShouldHaveSingleItem();
        evento.QuilometragemAnterior.ShouldBe(50_000);
        evento.QuilometragemAtual.ShouldBe(55_000);
    }

    [Fact]
    public void RegistrarQuilometragem_ComValorMenor_Rejeita()
    {
        var veiculo = CriarVeiculo(quilometragem: 50_000);

        // Odômetro que anda para trás indica erro de digitação ou adulteração;
        // em nenhum dos dois casos o dado deve ser aceito.
        Should.Throw<DomainException>(() => veiculo.RegistrarQuilometragem(49_999))
            .Codigo.ShouldBe("QUILOMETRAGEM_RETROATIVA");
    }

    [Fact]
    public void RegistrarQuilometragem_ComMesmoValor_NaoPublicaEvento()
    {
        var veiculo = CriarVeiculo(quilometragem: 50_000);
        veiculo.LimparEventos();

        veiculo.RegistrarQuilometragem(50_000);

        veiculo.EventosDeDominio.ShouldBeEmpty();
    }

    [Fact]
    public void TransferirPara_OutroCliente_TrocaOProprietarioEPublicaEvento()
    {
        var veiculo = CriarVeiculo();
        var novoDono = Guid.CreateVersion7();
        veiculo.LimparEventos();

        veiculo.TransferirPara(novoDono);

        veiculo.ClienteId.ShouldBe(novoDono);

        var evento = veiculo.EventosDeDominio.OfType<VeiculoTransferido>().ShouldHaveSingleItem();
        evento.ClienteAnteriorId.ShouldBe(ClienteId);
        evento.NovoClienteId.ShouldBe(novoDono);
    }

    [Fact]
    public void TransferirPara_MesmoCliente_NaoPublicaEvento()
    {
        var veiculo = CriarVeiculo();
        veiculo.LimparEventos();

        veiculo.TransferirPara(ClienteId);

        veiculo.EventosDeDominio.ShouldBeEmpty();
    }

    [Fact]
    public void TransferirPara_ClienteVazio_Rejeita() =>
        Should.Throw<DomainException>(() => CriarVeiculo().TransferirPara(Guid.Empty))
            .Codigo.ShouldBe("CLIENTE_OBRIGATORIO");

    [Fact]
    public void AtualizarDados_ComVeiculoInativo_Rejeita()
    {
        var veiculo = CriarVeiculo();
        veiculo.Inativar("Vendido");

        Should.Throw<DomainException>(() => veiculo.AtualizarDados("Fiat", "Uno", 2020, 2020, "Azul"))
            .Codigo.ShouldBe("VEICULO_INATIVO");
    }

    [Fact]
    public void Inativar_TornaInativoEPublicaEvento()
    {
        var veiculo = CriarVeiculo();
        veiculo.LimparEventos();

        veiculo.Inativar("Veículo vendido");

        veiculo.Ativo.ShouldBeFalse();
        veiculo.EventosDeDominio.OfType<VeiculoInativado>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Descricao_ReuneMarcaModeloAnoEPlaca() =>
        CriarVeiculo().Descricao.ShouldBe("Volkswagen Gol 2021 - ABC1D23");

    private static Veiculo CriarVeiculo(int quilometragem = 0) =>
        Veiculo.Cadastrar(ClienteId, "ABC1D23", "Volkswagen", "Gol", 2020, 2021, "Branco", quilometragem);
}
