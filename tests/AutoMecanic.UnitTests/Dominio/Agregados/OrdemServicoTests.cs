using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.Events;
using AutoMecanic.Domain.OrdensServico.ValueObjects;

namespace AutoMecanic.UnitTests.Dominio.Agregados;

/// <summary>
/// A Ordem de Serviço é o agregado central. Estes testes cobrem a máquina de estados
/// completa exigida pelo requisito, o congelamento de itens após o envio do orçamento e o
/// cálculo automático de valores — as três regras cuja quebra teria efeito direto sobre o
/// que o cliente vê e aprova.
/// </summary>
public sealed class OrdemServicoTests
{
    private static readonly Guid ClienteId = Guid.CreateVersion7();
    private static readonly Guid VeiculoId = Guid.CreateVersion7();
    private static readonly Guid ServicoId = Guid.CreateVersion7();
    private static readonly Guid PecaId = Guid.CreateVersion7();
    private static readonly Guid ResponsavelId = Guid.CreateVersion7();

    // -----------------------------------------------------------------
    // Abertura
    // -----------------------------------------------------------------

    [Fact]
    public void Abrir_ComDadosValidos_NasceRecebidaComHistoricoEEvento()
    {
        var ordem = Abrir();

        ordem.Status.ShouldBe(StatusOrdemServico.Recebida);
        ordem.Numero.Valor.ShouldBe("OS-2026-000001");
        ordem.Orcamento.ShouldBeNull();
        ordem.ItensServico.ShouldBeEmpty();
        ordem.ItensPeca.ShouldBeEmpty();

        var registro = ordem.Historico.ShouldHaveSingleItem();
        registro.StatusAnterior.ShouldBeNull();
        registro.StatusAtual.ShouldBe(StatusOrdemServico.Recebida);

        ordem.EventosDeDominio.OfType<OrdemDeServicoAberta>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Abrir_SemRelatoDoProblema_Rejeita(string? descricao) =>
        Should.Throw<DomainException>(() =>
            OrdemServico.Abrir(NumeroPadrao(), ClienteId, VeiculoId, descricao))
            .Codigo.ShouldBe("DESCRICAO_PROBLEMA_OBRIGATORIA");

    [Fact]
    public void Abrir_SemCliente_Rejeita() =>
        Should.Throw<DomainException>(() =>
            OrdemServico.Abrir(NumeroPadrao(), Guid.Empty, VeiculoId, "Barulho"))
            .Codigo.ShouldBe("CLIENTE_OBRIGATORIO");

    [Fact]
    public void Abrir_SemVeiculo_Rejeita() =>
        Should.Throw<DomainException>(() =>
            OrdemServico.Abrir(NumeroPadrao(), ClienteId, Guid.Empty, "Barulho"))
            .Codigo.ShouldBe("VEICULO_OBRIGATORIO");

    // -----------------------------------------------------------------
    // Máquina de estados
    // -----------------------------------------------------------------

    [Fact]
    public void FluxoCompleto_DaRecepcaoAEntrega_PercorreTodosOsStatus()
    {
        var ordem = Abrir();

        ordem.IniciarDiagnostico(ResponsavelId);
        ordem.Status.ShouldBe(StatusOrdemServico.EmDiagnostico);

        ordem.RegistrarDiagnostico("Pastilhas de freio no limite de desgaste.");
        ordem.AdicionarServico(ServicoId, "Troca de pastilhas", 160m, 1, 75);
        ordem.AdicionarPeca(PecaId, "PAST-FRE-DIA", "Pastilha dianteira", 189.90m, 1);

        ordem.GerarOrcamento();
        ordem.Status.ShouldBe(StatusOrdemServico.EmDiagnostico); // gerar não muda o status

        ordem.EnviarOrcamentoParaAprovacao();
        ordem.Status.ShouldBe(StatusOrdemServico.AguardandoAprovacao);

        ordem.AprovarOrcamento();
        ordem.Status.ShouldBe(StatusOrdemServico.EmExecucao);
        ordem.ExecucaoIniciadaEm.ShouldNotBeNull();

        ordem.FinalizarServico("Serviço concluído.");
        ordem.Status.ShouldBe(StatusOrdemServico.Finalizada);
        ordem.FinalizadaEm.ShouldNotBeNull();
        ordem.DuracaoDaExecucao.ShouldNotBeNull();

        ordem.EntregarVeiculo();
        ordem.Status.ShouldBe(StatusOrdemServico.Entregue);
        ordem.EntregueEm.ShouldNotBeNull();
        ordem.TempoTotalDeAtendimento.ShouldNotBeNull();

        // A linha do tempo registra cada uma das 6 transições.
        ordem.Historico.Count.ShouldBe(6);
        ordem.Historico.Select(h => h.StatusAtual).ShouldBe(
        [
            StatusOrdemServico.Recebida,
            StatusOrdemServico.EmDiagnostico,
            StatusOrdemServico.AguardandoAprovacao,
            StatusOrdemServico.EmExecucao,
            StatusOrdemServico.Finalizada,
            StatusOrdemServico.Entregue
        ]);
    }

    [Fact]
    public void IniciarDiagnostico_ForaDeRecebida_Rejeita()
    {
        var ordem = Abrir();
        ordem.IniciarDiagnostico();

        Should.Throw<DomainException>(() => ordem.IniciarDiagnostico())
            .Codigo.ShouldBe("TRANSICAO_INVALIDA");
    }

    [Fact]
    public void AprovarOrcamento_SemEnvioPrevio_Rejeita()
    {
        var ordem = ComItens();

        // Pular a etapa de envio significaria o cliente "aprovar" algo que nunca viu.
        Should.Throw<DomainException>(ordem.AprovarOrcamento)
            .Codigo.ShouldBe("TRANSICAO_INVALIDA");
    }

    [Fact]
    public void FinalizarServico_SemEstarEmExecucao_Rejeita() =>
        Should.Throw<DomainException>(() => ComItens().FinalizarServico())
            .Codigo.ShouldBe("TRANSICAO_INVALIDA");

    [Fact]
    public void EntregarVeiculo_SemFinalizar_Rejeita()
    {
        var ordem = ComOrcamentoAprovado();

        Should.Throw<DomainException>(() => ordem.EntregarVeiculo())
            .Codigo.ShouldBe("TRANSICAO_INVALIDA");
    }

    [Fact]
    public void QualquerTransicao_AposEntrega_Rejeita()
    {
        var ordem = ComOrcamentoAprovado();
        ordem.FinalizarServico();
        ordem.EntregarVeiculo();

        Should.Throw<DomainException>(() => ordem.IniciarDiagnostico())
            .Codigo.ShouldBe("ORDEM_ENCERRADA");

        Should.Throw<DomainException>(() => ordem.Cancelar("tardio"))
            .Codigo.ShouldBe("ORDEM_ENCERRADA");
    }

    // -----------------------------------------------------------------
    // Composição de itens
    // -----------------------------------------------------------------

    [Fact]
    public void AdicionarServico_CongelaODescritivoEOPreco()
    {
        var ordem = Abrir();

        var item = ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 2, 45);

        item.Descricao.ShouldBe("Troca de óleo");
        item.PrecoUnitario.Valor.ShouldBe(120m);
        item.Subtotal.Valor.ShouldBe(240m);
        item.TempoTotalEstimadoEmMinutos.ShouldBe(90);

        ordem.ValorTotalServicos.Valor.ShouldBe(240m);
        ordem.TempoEstimadoTotalEmMinutos.ShouldBe(90);
    }

    [Fact]
    public void AdicionarServico_MesmoServicoDuasVezes_SomaAQuantidade()
    {
        var ordem = Abrir();

        ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 1, 45);
        ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 2, 45);

        // Uma linha só, com quantidade 3 — e não três linhas idênticas no orçamento.
        ordem.ItensServico.ShouldHaveSingleItem().Quantidade.ShouldBe(3);
        ordem.ValorTotalServicos.Valor.ShouldBe(360m);
    }

    [Fact]
    public void AdicionarPeca_CongelaCodigoNomeEPreco()
    {
        var ordem = Abrir();

        var item = ordem.AdicionarPeca(PecaId, "fil-oleo", "Filtro de óleo", 32.50m, 2);

        item.CodigoPeca.ShouldBe("FIL-OLEO");
        item.NomePeca.ShouldBe("Filtro de óleo");
        item.Subtotal.Valor.ShouldBe(65m);
        item.Reservada.ShouldBeFalse();
        item.Consumida.ShouldBeFalse();

        ordem.ValorTotalPecas.Valor.ShouldBe(65m);
    }

    [Fact]
    public void RemoverServico_RetiraOItemERecalculaOTotal()
    {
        var ordem = Abrir();
        var item = ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 1, 45);

        ordem.RemoverServico(item.Id);

        ordem.ItensServico.ShouldBeEmpty();
        ordem.ValorTotalServicos.EhZero.ShouldBeTrue();
        ordem.EventosDeDominio.OfType<ItemRemovidoDaOrdem>().ShouldHaveSingleItem();
    }

    [Fact]
    public void RemoverServico_ComItemInexistente_Rejeita() =>
        Should.Throw<DomainException>(() => Abrir().RemoverServico(Guid.CreateVersion7()))
            .Codigo.ShouldBe("ITEM_NAO_ENCONTRADO");

    [Fact]
    public void RemoverPeca_JaConsumida_Rejeita()
    {
        var ordem = ComOrcamentoAprovado();
        var item = ordem.ItensPeca.First();

        ordem.ConfirmarConsumoDePeca(item.Id);

        // Já está no veículo: removê-la do orçamento seria cobrar a menos do que foi feito.
        Should.Throw<DomainException>(() => ordem.RemoverPeca(item.Id))
            .Codigo.ShouldBe("ITENS_CONGELADOS");
    }

    [Fact]
    public void AlterarItens_AposEnvioDoOrcamento_Rejeita()
    {
        var ordem = ComOrcamentoEnviado();

        // Esta é a regra que garante que o cliente aprove exatamente o valor que viu.
        Should.Throw<DomainException>(() => ordem.AdicionarServico(ServicoId, "Extra", 50m, 1, 10))
            .Codigo.ShouldBe("ITENS_CONGELADOS");
    }

    [Fact]
    public void AlterarQuantidadeDeServico_AtualizaOSubtotal()
    {
        var ordem = Abrir();
        var item = ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 1, 45);

        ordem.AlterarQuantidadeDeServico(item.Id, 3);

        ordem.ValorTotalServicos.Valor.ShouldBe(360m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void AlterarQuantidadeDeServico_ForaDaFaixa_Rejeita(int quantidade)
    {
        var ordem = Abrir();
        var item = ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 1, 45);

        Should.Throw<DomainException>(() => ordem.AlterarQuantidadeDeServico(item.Id, quantidade))
            .Codigo.ShouldBe("QUANTIDADE_INVALIDA");
    }

    // -----------------------------------------------------------------
    // Orçamento
    // -----------------------------------------------------------------

    [Fact]
    public void GerarOrcamento_SomaServicosEPecas()
    {
        var ordem = Abrir();
        ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 1, 45);
        ordem.AdicionarPeca(PecaId, "OL-5W30", "Óleo 5W30", 48.90m, 4);

        var orcamento = ordem.GerarOrcamento();

        orcamento.ValorServicos.Valor.ShouldBe(120m);
        orcamento.ValorPecas.Valor.ShouldBe(195.60m);
        orcamento.ValorTotal.Valor.ShouldBe(315.60m);
        orcamento.Status.ShouldBe(StatusOrcamento.EmElaboracao);

        ordem.EventosDeDominio.OfType<OrcamentoGerado>().ShouldHaveSingleItem();
    }

    [Fact]
    public void GerarOrcamento_ComDesconto_AplicaOPercentual()
    {
        var ordem = Abrir();
        ordem.AdicionarServico(ServicoId, "Troca de óleo", 200m, 1, 45);

        var orcamento = ordem.GerarOrcamento(10m);

        orcamento.ValorBruto.Valor.ShouldBe(200m);
        orcamento.ValorTotal.Valor.ShouldBe(180m);
        orcamento.ValorDesconto.Valor.ShouldBe(20m);
    }

    [Fact]
    public void GerarOrcamento_SemItens_Rejeita() =>
        Should.Throw<DomainException>(() => Abrir().GerarOrcamento())
            .Codigo.ShouldBe("ORCAMENTO_SEM_ITENS");

    [Fact]
    public void AdicionarItem_ComOrcamentoEmElaboracao_RecalculaAutomaticamente()
    {
        var ordem = Abrir();
        ordem.AdicionarServico(ServicoId, "Troca de óleo", 120m, 1, 45);
        ordem.GerarOrcamento();

        ordem.AdicionarPeca(PecaId, "OL", "Óleo", 50m, 1);

        // O orçamento acompanha os itens enquanto está em elaboração: nunca fica defasado.
        ordem.Orcamento!.ValorTotal.Valor.ShouldBe(170m);
    }

    [Fact]
    public void EnviarOrcamento_DefineValidadeEMudaOStatus()
    {
        var ordem = ComItens();
        ordem.GerarOrcamento();

        ordem.EnviarOrcamentoParaAprovacao(10);

        ordem.Status.ShouldBe(StatusOrdemServico.AguardandoAprovacao);
        ordem.Orcamento!.Status.ShouldBe(StatusOrcamento.AguardandoAprovacao);
        ordem.Orcamento.EnviadoEm.ShouldNotBeNull();
        ordem.Orcamento.ValidoAte.ShouldNotBeNull();

        ordem.EventosDeDominio.OfType<OrcamentoEnviadoAoCliente>().ShouldHaveSingleItem();
    }

    [Fact]
    public void EnviarOrcamento_SemGerar_Rejeita() =>
        Should.Throw<DomainException>(() => ComItens().EnviarOrcamentoParaAprovacao())
            .Codigo.ShouldBe("ORCAMENTO_INEXISTENTE");

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void EnviarOrcamento_ComValidadeForaDaFaixa_Rejeita(int dias)
    {
        var ordem = ComItens();
        ordem.GerarOrcamento();

        Should.Throw<DomainException>(() => ordem.EnviarOrcamentoParaAprovacao(dias))
            .Codigo.ShouldBe("VALIDADE_INVALIDA");
    }

    [Fact]
    public void AprovarOrcamento_IniciaAExecucaoEEmiteOsDoisEventos()
    {
        var ordem = ComOrcamentoEnviado();
        ordem.LimparEventos();

        ordem.AprovarOrcamento();

        ordem.Status.ShouldBe(StatusOrdemServico.EmExecucao);
        ordem.Orcamento!.Status.ShouldBe(StatusOrcamento.Aprovado);
        ordem.Orcamento.RespondidoEm.ShouldNotBeNull();

        ordem.EventosDeDominio.OfType<OrcamentoAprovadoPeloCliente>().ShouldHaveSingleItem();
        ordem.EventosDeDominio.OfType<ExecucaoIniciada>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ReprovarOrcamento_CancelaAOrdemERegistraOMotivo()
    {
        var ordem = ComOrcamentoEnviado();
        ordem.LimparEventos();

        ordem.ReprovarOrcamento("Valor acima do esperado");

        ordem.Status.ShouldBe(StatusOrdemServico.Cancelada);
        ordem.Orcamento!.Status.ShouldBe(StatusOrcamento.Reprovado);
        ordem.Orcamento.MotivoReprovacao.ShouldBe("Valor acima do esperado");
        ordem.MotivoCancelamento.ShouldNotBeNull().ShouldContain("Valor acima do esperado");

        ordem.EventosDeDominio.OfType<OrcamentoReprovadoPeloCliente>().ShouldHaveSingleItem();
        ordem.EventosDeDominio.OfType<OrdemDeServicoCancelada>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ReprovarOrcamento_SemMotivo_RegistraTextoPadrao()
    {
        var ordem = ComOrcamentoEnviado();

        ordem.ReprovarOrcamento(null);

        ordem.Orcamento!.MotivoReprovacao.ShouldBe("Não informado pelo cliente");
    }

    [Fact]
    public void ExpirarOrcamento_AposAValidade_CancelaAOrdem()
    {
        var ordem = ComOrcamentoEnviado(validadeEmDias: 1);

        ordem.ExpirarOrcamento(DateTimeOffset.UtcNow.AddDays(2));

        ordem.Status.ShouldBe(StatusOrdemServico.Cancelada);
        ordem.Orcamento!.Status.ShouldBe(StatusOrcamento.Expirado);
        ordem.EventosDeDominio.OfType<OrcamentoExpirado>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ExpirarOrcamento_AindaDentroDaValidade_NaoFazNada()
    {
        var ordem = ComOrcamentoEnviado(validadeEmDias: 7);

        ordem.ExpirarOrcamento(DateTimeOffset.UtcNow.AddDays(1));

        ordem.Status.ShouldBe(StatusOrdemServico.AguardandoAprovacao);
    }

    [Fact]
    public void RetornarParaDiagnostico_ReabreOOrcamentoEDescongelaOsItens()
    {
        var ordem = ComOrcamentoEnviado();

        ordem.RetornarParaDiagnostico("Cliente pediu para tirar um serviço");

        ordem.Status.ShouldBe(StatusOrdemServico.EmDiagnostico);
        ordem.Orcamento!.Status.ShouldBe(StatusOrcamento.EmElaboracao);

        Should.NotThrow(() => ordem.AdicionarServico(Guid.CreateVersion7(), "Outro", 10m, 1, 5));
    }

    [Fact]
    public void ReabrirOrcamentoJaAprovado_Rejeita()
    {
        var ordem = ComOrcamentoAprovado();

        // Depois de aprovado, o orçamento é um compromisso com o cliente.
        Should.Throw<DomainException>(() => ordem.RetornarParaDiagnostico())
            .Codigo.ShouldBe("TRANSICAO_INVALIDA");
    }

    // -----------------------------------------------------------------
    // Cancelamento
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(StatusOrdemServico.Recebida)]
    [InlineData(StatusOrdemServico.EmDiagnostico)]
    [InlineData(StatusOrdemServico.AguardandoAprovacao)]
    public void Cancelar_AntesDaExecucao_EhPermitido(StatusOrdemServico statusDesejado)
    {
        var ordem = LevarAte(statusDesejado);

        ordem.Cancelar("Cliente desistiu");

        ordem.Status.ShouldBe(StatusOrdemServico.Cancelada);
        ordem.MotivoCancelamento.ShouldBe("Cliente desistiu");
    }

    [Fact]
    public void Cancelar_ComOrdemEmExecucao_Rejeita()
    {
        var ordem = ComOrcamentoAprovado();

        // Peças já saíram do estoque e horas já foram trabalhadas.
        Should.Throw<DomainException>(() => ordem.Cancelar("tarde demais"))
            .Codigo.ShouldBe("CANCELAMENTO_NAO_PERMITIDO");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Cancelar_SemMotivo_Rejeita(string? motivo) =>
        Should.Throw<DomainException>(() => Abrir().Cancelar(motivo))
            .Codigo.ShouldBe("MOTIVO_OBRIGATORIO");

    // -----------------------------------------------------------------
    // Reservas e consumo de peças
    // -----------------------------------------------------------------

    [Fact]
    public void ConfirmarReservaDePeca_MarcaOItemComoReservado()
    {
        var ordem = Abrir();
        var item = ordem.AdicionarPeca(PecaId, "COD", "Peça", 10m, 1);

        ordem.ConfirmarReservaDePeca(item.Id);

        ordem.ItensPeca.First().Reservada.ShouldBeTrue();
    }

    [Fact]
    public void ConfirmarLiberacaoDeReserva_DesmarcaOItem()
    {
        var ordem = Abrir();
        var item = ordem.AdicionarPeca(PecaId, "COD", "Peça", 10m, 1);
        ordem.ConfirmarReservaDePeca(item.Id);

        ordem.ConfirmarLiberacaoDeReservaDePeca(item.Id);

        ordem.ItensPeca.First().Reservada.ShouldBeFalse();
    }

    [Fact]
    public void ConfirmarConsumoDePeca_ForaDaExecucao_Rejeita()
    {
        var ordem = Abrir();
        var item = ordem.AdicionarPeca(PecaId, "COD", "Peça", 10m, 1);

        Should.Throw<DomainException>(() => ordem.ConfirmarConsumoDePeca(item.Id))
            .Codigo.ShouldBe("TRANSICAO_INVALIDA");
    }

    [Fact]
    public void ConfirmarConsumoDePeca_EmExecucao_MarcaConsumida()
    {
        var ordem = ComOrcamentoAprovado();
        var item = ordem.ItensPeca.First();

        ordem.ConfirmarConsumoDePeca(item.Id);

        var atualizado = ordem.ItensPeca.First();
        atualizado.Consumida.ShouldBeTrue();
        atualizado.Reservada.ShouldBeFalse();
    }

    // -----------------------------------------------------------------
    // Diagnóstico e responsável
    // -----------------------------------------------------------------

    [Fact]
    public void RegistrarDiagnostico_EmDiagnostico_ArmazenaOLaudo()
    {
        var ordem = Abrir();
        ordem.IniciarDiagnostico();

        ordem.RegistrarDiagnostico("Disco de freio empenado.");

        ordem.DiagnosticoTecnico.ShouldBe("Disco de freio empenado.");
        ordem.EventosDeDominio.OfType<DiagnosticoRegistrado>().ShouldHaveSingleItem();
    }

    [Fact]
    public void RegistrarDiagnostico_EmExecucao_EhPermitido()
    {
        var ordem = ComOrcamentoAprovado();

        // O mecânico complementa o laudo com o que encontrou ao desmontar.
        Should.NotThrow(() => ordem.RegistrarDiagnostico("Também há folga no rolamento."));
    }

    [Fact]
    public void RegistrarDiagnostico_ComOrdemRecebida_Rejeita() =>
        Should.Throw<DomainException>(() => Abrir().RegistrarDiagnostico("Laudo"))
            .Codigo.ShouldBe("TRANSICAO_INVALIDA");

    [Fact]
    public void RegistrarDiagnostico_ComTextoVazio_Rejeita()
    {
        var ordem = Abrir();
        ordem.IniciarDiagnostico();

        Should.Throw<DomainException>(() => ordem.RegistrarDiagnostico("   "))
            .Codigo.ShouldBe("DIAGNOSTICO_OBRIGATORIO");
    }

    [Fact]
    public void AtribuirResponsavel_TrocaOResponsavel()
    {
        var ordem = Abrir();
        var novoResponsavel = Guid.CreateVersion7();

        ordem.AtribuirResponsavel(novoResponsavel);

        ordem.ResponsavelId.ShouldBe(novoResponsavel);
    }

    [Fact]
    public void AtribuirResponsavel_ComIdVazio_Rejeita() =>
        Should.Throw<DomainException>(() => Abrir().AtribuirResponsavel(Guid.Empty))
            .Codigo.ShouldBe("RESPONSAVEL_OBRIGATORIO");

    // -----------------------------------------------------------------
    // Métrica de tempo
    // -----------------------------------------------------------------

    [Fact]
    public void DuracaoDaExecucao_AntesDeFinalizar_EhNula()
    {
        var ordem = ComOrcamentoAprovado();

        ordem.DuracaoDaExecucao.ShouldBeNull();
    }

    [Fact]
    public void FinalizarServico_EmiteEventoComADuracaoEmMinutos()
    {
        var ordem = ComOrcamentoAprovado();
        ordem.LimparEventos();

        ordem.FinalizarServico();

        var evento = ordem.EventosDeDominio.OfType<ServicoFinalizado>().ShouldHaveSingleItem();
        evento.DuracaoEmMinutos.ShouldBeGreaterThanOrEqualTo(0);
    }

    // -----------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------

    private static NumeroOrdemServico NumeroPadrao() => NumeroOrdemServico.Gerar(2026, 1);

    private static OrdemServico Abrir() =>
        OrdemServico.Abrir(NumeroPadrao(), ClienteId, VeiculoId, "Barulho ao frear", 50_000, ResponsavelId);

    private static OrdemServico ComItens()
    {
        var ordem = Abrir();
        ordem.IniciarDiagnostico();
        ordem.AdicionarServico(ServicoId, "Troca de pastilhas", 160m, 1, 75);
        ordem.AdicionarPeca(PecaId, "PAST-FRE-DIA", "Pastilha dianteira", 189.90m, 1);

        return ordem;
    }

    private static OrdemServico ComOrcamentoEnviado(int validadeEmDias = 7)
    {
        var ordem = ComItens();
        ordem.GerarOrcamento();
        ordem.EnviarOrcamentoParaAprovacao(validadeEmDias);

        return ordem;
    }

    private static OrdemServico ComOrcamentoAprovado()
    {
        var ordem = ComItens();

        // Simula o que a camada de aplicação faz ao reservar a peça no estoque.
        ordem.ConfirmarReservaDePeca(ordem.ItensPeca.First().Id);
        ordem.GerarOrcamento();
        ordem.EnviarOrcamentoParaAprovacao();
        ordem.AprovarOrcamento();

        return ordem;
    }

    private static OrdemServico LevarAte(StatusOrdemServico destino)
    {
        var ordem = Abrir();

        if (destino == StatusOrdemServico.Recebida)
        {
            return ordem;
        }

        ordem.IniciarDiagnostico();

        if (destino == StatusOrdemServico.EmDiagnostico)
        {
            return ordem;
        }

        ordem.AdicionarServico(ServicoId, "Serviço", 100m, 1, 30);
        ordem.GerarOrcamento();
        ordem.EnviarOrcamentoParaAprovacao();

        return ordem;
    }
}
