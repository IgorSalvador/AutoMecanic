using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.Events;
using AutoMecanic.Domain.Clientes.ValueObjects;

namespace AutoMecanic.UnitTests.Dominio.Agregados;

public sealed class ClienteTests
{
    private const string CpfValido = "52998224725";
    private const string CnpjValido = "34028316000103";

    [Fact]
    public void Cadastrar_ComDadosValidos_CriaClienteAtivoEPublicaEvento()
    {
        var cliente = Cliente.Cadastrar("Maria Souza", CpfValido, "maria@exemplo.com", "11987654321");

        cliente.Id.ShouldNotBe(Guid.Empty);
        cliente.Nome.ShouldBe("Maria Souza");
        cliente.Ativo.ShouldBeTrue();
        cliente.TipoPessoa.ShouldBe(TipoPessoa.Fisica);
        cliente.AtualizadoEm.ShouldBeNull();

        cliente.EventosDeDominio.OfType<ClienteCadastrado>().ShouldHaveSingleItem()
            .ClienteId.ShouldBe(cliente.Id);
    }

    [Fact]
    public void Cadastrar_ComCnpj_ClassificaComoPessoaJuridica() =>
        Cliente.Cadastrar("Transportadora LTDA", CnpjValido, "contato@t.com", "1133224455")
            .TipoPessoa.ShouldBe(TipoPessoa.Juridica);

    [Fact]
    public void Cadastrar_ComEndereco_ArmazenaOEndereco()
    {
        var endereco = Endereco.Criar("Rua A", "10", null, "Centro", "Santos", "SP", "11010000");

        var cliente = Cliente.Cadastrar("Maria", CpfValido, "m@e.com", "11987654321", endereco);

        cliente.Endereco.ShouldBe(endereco);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Cadastrar_SemNome_Rejeita(string? nome) =>
        Should.Throw<DomainException>(() => Cliente.Cadastrar(nome, CpfValido, "m@e.com", "11987654321"))
            .Codigo.ShouldBe("NOME_OBRIGATORIO");

    [Fact]
    public void Cadastrar_ComNomeMuitoCurto_Rejeita() =>
        Should.Throw<DomainException>(() => Cliente.Cadastrar("Jo", CpfValido, "m@e.com", "11987654321"))
            .Codigo.ShouldBe("NOME_INVALIDO");

    [Fact]
    public void Cadastrar_ComNomeMuitoLongo_Rejeita() =>
        Should.Throw<DomainException>(() =>
            Cliente.Cadastrar(new string('a', 151), CpfValido, "m@e.com", "11987654321"))
            .Codigo.ShouldBe("NOME_INVALIDO");

    [Fact]
    public void Cadastrar_ComDocumentoInvalido_Rejeita() =>
        Should.Throw<DomainException>(() => Cliente.Cadastrar("Maria", "11111111111", "m@e.com", "11987654321"));

    [Fact]
    public void AtualizarCadastro_AlteraContatoEPublicaEvento()
    {
        var cliente = CriarClientePadrao();
        cliente.LimparEventos();

        cliente.AtualizarCadastro("Maria Souza Silva", "novo@exemplo.com", "11912345678", null);

        cliente.Nome.ShouldBe("Maria Souza Silva");
        cliente.Email.Endereco.ShouldBe("novo@exemplo.com");
        cliente.AtualizadoEm.ShouldNotBeNull();

        cliente.EventosDeDominio.OfType<DadosDeContatoDoClienteAtualizados>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AtualizarCadastro_ComClienteInativo_Rejeita()
    {
        var cliente = CriarClientePadrao();
        cliente.Inativar("Solicitação do cliente");

        // Um cadastro inativo não deve voltar a ser editado sem antes ser reativado:
        // caso contrário, o motivo da inativação perde sentido.
        Should.Throw<DomainException>(() =>
            cliente.AtualizarCadastro("Outro Nome", "outro@e.com", "11987654321", null))
            .Codigo.ShouldBe("CLIENTE_INATIVO");
    }

    [Fact]
    public void Inativar_TornaOClienteInativoEPublicaEvento()
    {
        var cliente = CriarClientePadrao();
        cliente.LimparEventos();

        cliente.Inativar("Encerrou relacionamento");

        cliente.Ativo.ShouldBeFalse();
        cliente.EventosDeDominio.OfType<ClienteInativado>().ShouldHaveSingleItem()
            .Motivo.ShouldBe("Encerrou relacionamento");
    }

    [Fact]
    public void Inativar_SemMotivo_RegistraNaoInformado()
    {
        var cliente = CriarClientePadrao();
        cliente.LimparEventos();

        cliente.Inativar();

        cliente.EventosDeDominio.OfType<ClienteInativado>().ShouldHaveSingleItem()
            .Motivo.ShouldBe("Não informado");
    }

    [Fact]
    public void Inativar_DuasVezes_EhIdempotente()
    {
        var cliente = CriarClientePadrao();
        cliente.Inativar("Primeira");
        cliente.LimparEventos();

        cliente.Inativar("Segunda");

        cliente.Ativo.ShouldBeFalse();
        cliente.EventosDeDominio.ShouldBeEmpty();
    }

    [Fact]
    public void Reativar_RestauraOAcessoEPublicaEvento()
    {
        var cliente = CriarClientePadrao();
        cliente.Inativar();
        cliente.LimparEventos();

        cliente.Reativar();

        cliente.Ativo.ShouldBeTrue();
        cliente.EventosDeDominio.OfType<ClienteReativado>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Reativar_ClienteJaAtivo_EhIdempotente()
    {
        var cliente = CriarClientePadrao();
        cliente.LimparEventos();

        cliente.Reativar();

        cliente.EventosDeDominio.ShouldBeEmpty();
    }

    [Fact]
    public void GarantirClienteAtivo_ComClienteAtivo_NaoLanca() =>
        Should.NotThrow(() => CriarClientePadrao().GarantirClienteAtivo());

    [Fact]
    public void GarantirClienteAtivo_ComClienteInativo_Lanca()
    {
        var cliente = CriarClientePadrao();
        cliente.Inativar();

        Should.Throw<DomainException>(cliente.GarantirClienteAtivo)
            .Codigo.ShouldBe("CLIENTE_INATIVO");
    }

    [Fact]
    public void Identidade_ClientesComMesmoId_SaoIguais()
    {
        var cliente = CriarClientePadrao();

        // Igualdade de entidade é por identidade: a mesma instância é sempre igual a si.
        cliente.Equals(cliente).ShouldBeTrue();
        cliente.Equals(CriarClientePadrao()).ShouldBeFalse();
    }

    private static Cliente CriarClientePadrao() =>
        Cliente.Cadastrar("Maria Souza", CpfValido, "maria@exemplo.com", "11987654321");
}
