using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Estoque.Events;

namespace AutoMecanic.UnitTests.Dominio.Agregados;

/// <summary>
/// O agregado Peça é a fronteira de consistência do saldo. Estes testes cobrem o modelo de
/// reserva — a separação entre o que está na prateleira e o que já foi prometido a um
/// orçamento — que é o que impede duas OS de venderem a mesma última peça.
/// </summary>
public sealed class PecaTests
{
    private static readonly Guid OrdemId = Guid.CreateVersion7();

    [Fact]
    public void Cadastrar_ComSaldoInicial_RegistraLancamentoDeEntrada()
    {
        var peca = CriarPeca(quantidadeInicial: 100, estoqueMinimo: 10);

        peca.QuantidadeEmEstoque.ShouldBe(100);
        peca.QuantidadeReservada.ShouldBe(0);
        peca.QuantidadeDisponivel.ShouldBe(100);
        peca.Ativo.ShouldBeTrue();

        var movimento = peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldHaveSingleItem();
        movimento.Tipo.ShouldBe(TipoMovimentoEstoque.Entrada);
        movimento.SaldoAnterior.ShouldBe(0);
        movimento.SaldoAtual.ShouldBe(100);
    }

    [Fact]
    public void Cadastrar_SemSaldoInicial_NaoGeraLancamentoMasAlertaOMinimo()
    {
        var peca = CriarPeca(quantidadeInicial: 0, estoqueMinimo: 5);

        peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldBeEmpty();
        peca.EventosDeDominio.OfType<EstoqueAtingiuNivelMinimo>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Cadastrar_SemCodigo_Rejeita(string? codigo) =>
        Should.Throw<DomainException>(() =>
            Peca.Cadastrar(codigo, "Filtro", null, UnidadeMedida.Unidade, 10m, 1, 1))
            .Codigo.ShouldBe("CODIGO_OBRIGATORIO");

    [Fact]
    public void Cadastrar_NormalizaOCodigoParaCaixaAlta() =>
        CriarPeca(codigo: "fil-oleo-001").Codigo.ShouldBe("FIL-OLEO-001");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cadastrar_ComPrecoNaoPositivo_Rejeita(decimal preco) =>
        Should.Throw<DomainException>(() =>
            Peca.Cadastrar("COD", "Filtro", null, UnidadeMedida.Unidade, preco, 1, 1))
            .Codigo.ShouldBe("PRECO_INVALIDO");

    // -----------------------------------------------------------------
    // Reserva
    // -----------------------------------------------------------------

    [Fact]
    public void Reservar_ReduzODisponivelSemMexerNoSaldoFisico()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.LimparEventos();

        peca.Reservar(3, OrdemId);

        peca.QuantidadeEmEstoque.ShouldBe(10); // continua na prateleira
        peca.QuantidadeReservada.ShouldBe(3);
        peca.QuantidadeDisponivel.ShouldBe(7);  // mas não pode ser prometida de novo

        peca.EventosDeDominio.OfType<QuantidadeReservada>().ShouldHaveSingleItem()
            .Quantidade.ShouldBe(3);
    }

    [Fact]
    public void Reservar_AcimaDoDisponivel_Rejeita()
    {
        var peca = CriarPeca(quantidadeInicial: 5, estoqueMinimo: 0);

        Should.Throw<DomainException>(() => peca.Reservar(6, OrdemId))
            .Codigo.ShouldBe("ESTOQUE_INSUFICIENTE");
    }

    [Fact]
    public void Reservar_DuasVezesAlemDoSaldo_RejeitaASegunda()
    {
        var peca = CriarPeca(quantidadeInicial: 5, estoqueMinimo: 0);
        peca.Reservar(3, OrdemId);

        // Este é exatamente o cenário que o modelo de reserva existe para impedir:
        // duas Ordens de Serviço prometendo peças que somadas não existem.
        Should.Throw<DomainException>(() => peca.Reservar(3, Guid.CreateVersion7()))
            .Codigo.ShouldBe("ESTOQUE_INSUFICIENTE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Reservar_ComQuantidadeNaoPositiva_Rejeita(int quantidade) =>
        Should.Throw<DomainException>(() => CriarPeca().Reservar(quantidade, OrdemId))
            .Codigo.ShouldBe("QUANTIDADE_INVALIDA");

    [Fact]
    public void Reservar_ComPecaInativa_Rejeita()
    {
        var peca = CriarPeca(quantidadeInicial: 10);
        peca.Inativar();

        Should.Throw<DomainException>(() => peca.Reservar(1, OrdemId))
            .Codigo.ShouldBe("PECA_INATIVA");
    }

    [Fact]
    public void LiberarReserva_DevolveOSaldoAoDisponivel()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.Reservar(4, OrdemId);
        peca.LimparEventos();

        peca.LiberarReserva(4, OrdemId);

        peca.QuantidadeReservada.ShouldBe(0);
        peca.QuantidadeDisponivel.ShouldBe(10);
        peca.QuantidadeEmEstoque.ShouldBe(10);

        peca.EventosDeDominio.OfType<ReservaLiberada>().ShouldHaveSingleItem();
    }

    [Fact]
    public void LiberarReserva_AcimaDoReservado_Rejeita()
    {
        var peca = CriarPeca(quantidadeInicial: 10);
        peca.Reservar(2, OrdemId);

        Should.Throw<DomainException>(() => peca.LiberarReserva(3, OrdemId))
            .Codigo.ShouldBe("RESERVA_INVALIDA");
    }

    [Fact]
    public void ConsumirReserva_BaixaOSaldoFisicoERegistraSaida()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.Reservar(4, OrdemId);
        peca.LimparEventos();

        peca.ConsumirReserva(4, OrdemId);

        peca.QuantidadeEmEstoque.ShouldBe(6);
        peca.QuantidadeReservada.ShouldBe(0);
        peca.QuantidadeDisponivel.ShouldBe(6);

        var movimento = peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldHaveSingleItem();
        movimento.Tipo.ShouldBe(TipoMovimentoEstoque.Saida);
        movimento.SaldoAnterior.ShouldBe(10);
        movimento.SaldoAtual.ShouldBe(6);
        movimento.OrdemServicoId.ShouldBe(OrdemId);
    }

    [Fact]
    public void ConsumirReserva_SemReservaPrevia_Rejeita() =>
        Should.Throw<DomainException>(() => CriarPeca(quantidadeInicial: 10).ConsumirReserva(1, OrdemId))
            .Codigo.ShouldBe("RESERVA_INVALIDA");

    // -----------------------------------------------------------------
    // Entradas, perdas e ajustes
    // -----------------------------------------------------------------

    [Fact]
    public void RegistrarEntrada_AumentaOSaldoERegistraLancamento()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.LimparEventos();

        peca.RegistrarEntrada(50, "Nota fiscal 1234");

        peca.QuantidadeEmEstoque.ShouldBe(60);

        var movimento = peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldHaveSingleItem();
        movimento.Tipo.ShouldBe(TipoMovimentoEstoque.Entrada);
        movimento.Motivo.ShouldBe("Nota fiscal 1234");
    }

    [Fact]
    public void RegistrarEntrada_VinculadaAUmaOrdem_ClassificaComoEstorno()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.LimparEventos();

        peca.RegistrarEntrada(2, "Peça não utilizada", OrdemId);

        peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldHaveSingleItem()
            .Tipo.ShouldBe(TipoMovimentoEstoque.Estorno);
    }

    [Fact]
    public void RegistrarEntrada_SemMotivo_Rejeita() =>
        Should.Throw<DomainException>(() => CriarPeca().RegistrarEntrada(1, "  "))
            .Codigo.ShouldBe("MOTIVO_OBRIGATORIO");

    [Fact]
    public void RegistrarPerda_ReduzOSaldoERegistraLancamento()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.LimparEventos();

        peca.RegistrarPerda(3, "Avaria no transporte");

        peca.QuantidadeEmEstoque.ShouldBe(7);
        peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldHaveSingleItem()
            .Tipo.ShouldBe(TipoMovimentoEstoque.Perda);
    }

    [Fact]
    public void RegistrarPerda_AlemDoDisponivel_Rejeita()
    {
        var peca = CriarPeca(quantidadeInicial: 10);
        peca.Reservar(8, OrdemId);

        // Só 2 estão livres: a perda não pode consumir o que já foi prometido.
        Should.Throw<DomainException>(() => peca.RegistrarPerda(3, "Avaria"))
            .Codigo.ShouldBe("ESTOQUE_INSUFICIENTE");
    }

    [Fact]
    public void AjustarSaldo_ParaMenos_RegistraADiferenca()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.LimparEventos();

        peca.AjustarSaldo(7, "Inventário de janeiro");

        peca.QuantidadeEmEstoque.ShouldBe(7);

        var movimento = peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldHaveSingleItem();
        movimento.Tipo.ShouldBe(TipoMovimentoEstoque.Ajuste);
        movimento.Quantidade.ShouldBe(3);
    }

    [Fact]
    public void AjustarSaldo_ParaMais_RegistraADiferenca()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.LimparEventos();

        peca.AjustarSaldo(15, "Inventário");

        peca.QuantidadeEmEstoque.ShouldBe(15);
        peca.EventosDeDominio.OfType<EstoqueMovimentado>().ShouldHaveSingleItem()
            .Quantidade.ShouldBe(5);
    }

    [Fact]
    public void AjustarSaldo_AbaixoDoReservado_Rejeita()
    {
        var peca = CriarPeca(quantidadeInicial: 10);
        peca.Reservar(6, OrdemId);

        // Ajustar para 4 deixaria 6 unidades prometidas sem lastro físico.
        Should.Throw<DomainException>(() => peca.AjustarSaldo(4, "Inventário"))
            .Codigo.ShouldBe("AJUSTE_INVALIDO");
    }

    [Fact]
    public void AjustarSaldo_ComOMesmoValor_NaoGeraLancamento()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 0);
        peca.LimparEventos();

        peca.AjustarSaldo(10, "Inventário");

        peca.EventosDeDominio.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------
    // Ponto de ressuprimento
    // -----------------------------------------------------------------

    [Fact]
    public void AoCruzarOEstoqueMinimo_EmiteAlertaDeRessuprimento()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 5);
        peca.LimparEventos();

        peca.RegistrarPerda(5, "Avaria");

        peca.AbaixoDoEstoqueMinimo.ShouldBeTrue();

        var alerta = peca.EventosDeDominio.OfType<EstoqueAtingiuNivelMinimo>().ShouldHaveSingleItem();
        alerta.SaldoDisponivel.ShouldBe(5);
        alerta.EstoqueMinimo.ShouldBe(5);
    }

    [Fact]
    public void AcimaDoEstoqueMinimo_NaoEmiteAlerta()
    {
        var peca = CriarPeca(quantidadeInicial: 100, estoqueMinimo: 5);
        peca.LimparEventos();

        peca.RegistrarPerda(1, "Avaria");

        peca.AbaixoDoEstoqueMinimo.ShouldBeFalse();
        peca.EventosDeDominio.OfType<EstoqueAtingiuNivelMinimo>().ShouldBeEmpty();
    }

    [Fact]
    public void ReservaTambemContaParaOAlertaDeRessuprimento()
    {
        var peca = CriarPeca(quantidadeInicial: 10, estoqueMinimo: 5);
        peca.LimparEventos();

        // O saldo físico continua 10, mas só 4 podem ser prometidos: o alerta
        // precisa considerar o disponível, não a prateleira.
        peca.Reservar(6, OrdemId);

        peca.EventosDeDominio.OfType<EstoqueAtingiuNivelMinimo>().ShouldHaveSingleItem()
            .SaldoDisponivel.ShouldBe(4);
    }

    // -----------------------------------------------------------------
    // Ciclo de vida
    // -----------------------------------------------------------------

    [Fact]
    public void Inativar_ComQuantidadeReservada_Rejeita()
    {
        var peca = CriarPeca(quantidadeInicial: 10);
        peca.Reservar(1, OrdemId);

        Should.Throw<DomainException>(peca.Inativar)
            .Codigo.ShouldBe("PECA_RESERVADA");
    }

    [Fact]
    public void Inativar_SemReservas_TornaInativaEPublicaEvento()
    {
        var peca = CriarPeca(quantidadeInicial: 10);
        peca.LimparEventos();

        peca.Inativar();

        peca.Ativo.ShouldBeFalse();
        peca.EventosDeDominio.OfType<PecaInativada>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ReajustarPreco_ComValorDiferente_PublicaEvento()
    {
        var peca = CriarPeca();
        peca.LimparEventos();

        peca.ReajustarPreco(59.90m);

        peca.PrecoUnitario.Valor.ShouldBe(59.90m);

        var evento = peca.EventosDeDominio.OfType<PrecoDaPecaReajustado>().ShouldHaveSingleItem();
        evento.PrecoAnterior.ShouldBe(48.90m);
        evento.PrecoNovo.ShouldBe(59.90m);
    }

    [Fact]
    public void ReajustarPreco_ComOMesmoValor_NaoPublicaEvento()
    {
        var peca = CriarPeca();
        peca.LimparEventos();

        peca.ReajustarPreco(48.90m);

        peca.EventosDeDominio.ShouldBeEmpty();
    }

    private static Peca CriarPeca(
        string codigo = "OL-5W30-1L",
        int quantidadeInicial = 100,
        int estoqueMinimo = 10) =>
        Peca.Cadastrar(codigo, "Óleo sintético 5W30", "Lubrificante", UnidadeMedida.Litro, 48.90m, quantidadeInicial, estoqueMinimo);
}
