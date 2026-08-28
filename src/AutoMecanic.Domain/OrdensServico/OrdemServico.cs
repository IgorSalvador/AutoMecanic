using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.OrdensServico.Events;
using AutoMecanic.Domain.OrdensServico.ValueObjects;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.Domain.OrdensServico;

/// <summary>
/// <b>Raiz de Agregado</b> central do sistema: a Ordem de Serviço.
/// <para>
/// Reúne, sob uma única fronteira de consistência, o que precisa mudar junto ou não mudar:
/// os <see cref="ItensServico"/>, os <see cref="ItensPeca"/>, o <see cref="Orcamento"/> e o
/// <see cref="Historico"/> de transições. Cliente, veículo, serviços do catálogo e peças do
/// estoque são <b>outros agregados</b> e por isso aparecem aqui apenas como identidades.
/// </para>
/// <para>
/// Toda mudança de <see cref="Status"/> é consequência de uma ação de negócio
/// (<see cref="IniciarDiagnostico"/>, <see cref="AprovarOrcamento"/>,
/// <see cref="FinalizarServico"/>…), nunca de uma atribuição direta. Transições inválidas
/// resultam em <see cref="DomainException"/>.
/// </para>
/// <para><b>Invariantes principais:</b></para>
/// <list type="bullet">
///   <item>Itens só podem ser alterados antes do envio do orçamento ao cliente.</item>
///   <item>O orçamento é sempre a soma calculada dos itens — nunca um valor digitado.</item>
///   <item>A execução só começa com orçamento aprovado pelo cliente.</item>
///   <item>A entrega só ocorre após a finalização dos serviços.</item>
///   <item>Estados terminais (Entregue, Cancelada) não admitem novas transições.</item>
/// </list>
/// </summary>
public sealed class OrdemServico : AggregateRoot
{
    private readonly List<ItemServico> _itensServico = [];
    private readonly List<ItemPeca> _itensPeca = [];
    private readonly List<HistoricoStatus> _historico = [];

    private OrdemServico()
    {
        Numero = null!;
        DescricaoProblema = null!;
    }

    private OrdemServico(
        Guid id,
        NumeroOrdemServico numero,
        Guid clienteId,
        Guid veiculoId,
        string descricaoProblema,
        int? quilometragemEntrada,
        Guid? responsavelId)
        : base(id)
    {
        Numero = numero;
        ClienteId = clienteId;
        VeiculoId = veiculoId;
        DescricaoProblema = descricaoProblema;
        QuilometragemEntrada = quilometragemEntrada;
        ResponsavelId = responsavelId;
        Status = StatusOrdemServico.Recebida;
        CriadaEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Número legível informado ao cliente (OS-AAAA-NNNNNN).</summary>
    public NumeroOrdemServico Numero { get; private set; }

    /// <summary>Identidade do agregado Cliente. Referência entre agregados é sempre por Id.</summary>
    public Guid ClienteId { get; private set; }

    /// <summary>Identidade do agregado Veículo.</summary>
    public Guid VeiculoId { get; private set; }

    public StatusOrdemServico Status { get; private set; }

    /// <summary>Relato do cliente na recepção ("está fazendo um barulho na frente").</summary>
    public string DescricaoProblema { get; private set; }

    /// <summary>Laudo do mecânico, preenchido durante o diagnóstico.</summary>
    public string? DiagnosticoTecnico { get; private set; }

    /// <summary>Odômetro na entrada do veículo.</summary>
    public int? QuilometragemEntrada { get; private set; }

    /// <summary>Mecânico ou atendente responsável pela OS.</summary>
    public Guid? ResponsavelId { get; private set; }

    /// <summary>Orçamento vigente. Só existe a partir da geração automática.</summary>
    public Orcamento? Orcamento { get; private set; }

    public string? MotivoCancelamento { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public DateTimeOffset? AtualizadaEm { get; private set; }

    /// <summary>Início da execução. Marco inicial do cálculo de tempo de execução.</summary>
    public DateTimeOffset? ExecucaoIniciadaEm { get; private set; }

    /// <summary>Conclusão dos serviços. Marco final do cálculo de tempo de execução.</summary>
    public DateTimeOffset? FinalizadaEm { get; private set; }

    public DateTimeOffset? EntregueEm { get; private set; }

    public IReadOnlyCollection<ItemServico> ItensServico => _itensServico.AsReadOnly();

    public IReadOnlyCollection<ItemPeca> ItensPeca => _itensPeca.AsReadOnly();

    /// <summary>Linha do tempo de transições, em ordem cronológica.</summary>
    public IReadOnlyCollection<HistoricoStatus> Historico => _historico.AsReadOnly();

    /// <summary>Soma dos itens de serviço.</summary>
    public Dinheiro ValorTotalServicos =>
        _itensServico.Aggregate(Dinheiro.Zero, (total, item) => total.Somar(item.Subtotal));

    /// <summary>Soma dos itens de peça.</summary>
    public Dinheiro ValorTotalPecas =>
        _itensPeca.Aggregate(Dinheiro.Zero, (total, item) => total.Somar(item.Subtotal));

    /// <summary>Tempo estimado de execução somando os itens de serviço.</summary>
    public int TempoEstimadoTotalEmMinutos =>
        _itensServico.Sum(item => item.TempoTotalEstimadoEmMinutos);

    /// <summary>
    /// Duração real da execução: do início ao fim dos serviços. É a métrica agregada pelo
    /// indicador de tempo médio de execução exigido pela gestão. Nula enquanto a OS não é finalizada.
    /// </summary>
    public TimeSpan? DuracaoDaExecucao =>
        ExecucaoIniciadaEm is not null && FinalizadaEm is not null
            ? FinalizadaEm.Value - ExecucaoIniciadaEm.Value
            : null;

    /// <summary>Tempo total de permanência do veículo na oficina, da abertura à entrega.</summary>
    public TimeSpan? TempoTotalDeAtendimento =>
        EntregueEm is not null ? EntregueEm.Value - CriadaEm : null;

    // ---------------------------------------------------------------------
    // Abertura
    // ---------------------------------------------------------------------

    /// <summary>
    /// Abre uma nova Ordem de Serviço. A OS nasce no status <c>Recebida</c>, sem itens e sem
    /// orçamento — eles surgem durante o diagnóstico.
    /// </summary>
    /// <param name="numero">Número sequencial legível, gerado pela camada de aplicação.</param>
    /// <param name="clienteId">Identidade do cliente, já validado como ativo.</param>
    /// <param name="veiculoId">Identidade do veículo, já validado como ativo e pertencente ao cliente.</param>
    /// <param name="descricaoProblema">Relato do cliente na recepção.</param>
    /// <param name="quilometragemEntrada">Odômetro na entrada, quando informado.</param>
    /// <param name="responsavelId">Usuário que abriu a OS.</param>
    public static OrdemServico Abrir(
        NumeroOrdemServico numero,
        Guid clienteId,
        Guid veiculoId,
        string? descricaoProblema,
        int? quilometragemEntrada = null,
        Guid? responsavelId = null)
    {
        ArgumentNullException.ThrowIfNull(numero);

        if (clienteId == Guid.Empty)
        {
            throw new DomainException("CLIENTE_OBRIGATORIO", "A Ordem de Serviço exige um cliente.");
        }

        if (veiculoId == Guid.Empty)
        {
            throw new DomainException("VEICULO_OBRIGATORIO", "A Ordem de Serviço exige um veículo.");
        }

        if (string.IsNullOrWhiteSpace(descricaoProblema))
        {
            throw new DomainException(
                "DESCRICAO_PROBLEMA_OBRIGATORIA",
                "É obrigatório registrar o relato do cliente sobre o problema.");
        }

        var descricao = descricaoProblema.Trim();

        if (descricao.Length > 2000)
        {
            throw new DomainException("DESCRICAO_PROBLEMA_INVALIDA", "O relato do problema excede 2000 caracteres.");
        }

        if (quilometragemEntrada is < 0 or > 3_000_000)
        {
            throw new DomainException("QUILOMETRAGEM_INVALIDA", "A quilometragem de entrada é inválida.");
        }

        var ordem = new OrdemServico(NovoId(), numero, clienteId, veiculoId, descricao, quilometragemEntrada, responsavelId);

        ordem.RegistrarTransicao(null, StatusOrdemServico.Recebida, "Veículo recebido na oficina.", responsavelId);
        ordem.RegistrarEvento(new OrdemDeServicoAberta(ordem.Id, numero.Valor, clienteId, veiculoId));

        return ordem;
    }

    // ---------------------------------------------------------------------
    // Diagnóstico
    // ---------------------------------------------------------------------

    /// <summary>Move a OS de <c>Recebida</c> para <c>EmDiagnostico</c>.</summary>
    public void IniciarDiagnostico(Guid? responsavelId = null)
    {
        ExigirStatus(
            StatusOrdemServico.EmDiagnostico,
            [StatusOrdemServico.Recebida]);

        if (responsavelId is not null)
        {
            ResponsavelId = responsavelId;
        }

        AlterarStatus(StatusOrdemServico.EmDiagnostico, "Diagnóstico iniciado.", responsavelId);
        RegistrarEvento(new DiagnosticoIniciado(Id, ResponsavelId));
    }

    /// <summary>
    /// Registra o laudo técnico. Aceito durante o diagnóstico e também durante a execução,
    /// quando o mecânico complementa o laudo com o que encontrou ao desmontar.
    /// </summary>
    public void RegistrarDiagnostico(string? diagnostico)
    {
        GarantirNaoTerminal();

        if (Status is not (StatusOrdemServico.EmDiagnostico or StatusOrdemServico.EmExecucao))
        {
            throw new DomainException(
                "TRANSICAO_INVALIDA",
                $"O diagnóstico só pode ser registrado com a OS em diagnóstico ou em execução. Situação atual: '{Status.Descricao()}'.");
        }

        if (string.IsNullOrWhiteSpace(diagnostico))
        {
            throw new DomainException("DIAGNOSTICO_OBRIGATORIO", "O diagnóstico técnico não pode ser vazio.");
        }

        var texto = diagnostico.Trim();

        if (texto.Length > 4000)
        {
            throw new DomainException("DIAGNOSTICO_INVALIDO", "O diagnóstico excede 4000 caracteres.");
        }

        DiagnosticoTecnico = texto;
        AtualizadaEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new DiagnosticoRegistrado(Id, texto));
    }

    // ---------------------------------------------------------------------
    // Composição de itens
    // ---------------------------------------------------------------------

    /// <summary>
    /// Inclui um serviço do catálogo. Os dados são copiados do catálogo pela camada de
    /// aplicação e congelados no item. Incluir o mesmo serviço duas vezes soma a quantidade,
    /// em vez de criar linhas duplicadas.
    /// </summary>
    public ItemServico AdicionarServico(
        Guid servicoId,
        string descricao,
        decimal precoUnitario,
        int quantidade,
        int tempoEstimadoEmMinutos)
    {
        GarantirItensAlteraveis();

        var existente = _itensServico.FirstOrDefault(item => item.ServicoId == servicoId);

        if (existente is not null)
        {
            existente.AlterarQuantidade(existente.Quantidade + quantidade);
            AtualizadaEm = DateTimeOffset.UtcNow;
            RecalcularOrcamentoSeExistir();

            RegistrarEvento(new ServicoIncluidoNaOrdem(Id, existente.Id, servicoId, quantidade, existente.Subtotal.Valor));

            return existente;
        }

        var item = ItemServico.Criar(Id, servicoId, descricao, precoUnitario, quantidade, tempoEstimadoEmMinutos);

        _itensServico.Add(item);
        AtualizadaEm = DateTimeOffset.UtcNow;
        RecalcularOrcamentoSeExistir();

        RegistrarEvento(new ServicoIncluidoNaOrdem(Id, item.Id, servicoId, quantidade, item.Subtotal.Valor));

        return item;
    }

    /// <summary>
    /// Inclui uma peça. A reserva no estoque é feita pela camada de aplicação, que confirma
    /// o sucesso chamando <see cref="ConfirmarReservaDePeca"/>.
    /// </summary>
    public ItemPeca AdicionarPeca(
        Guid pecaId,
        string codigoPeca,
        string nomePeca,
        decimal precoUnitario,
        int quantidade)
    {
        GarantirItensAlteraveis();

        var existente = _itensPeca.FirstOrDefault(item => item.PecaId == pecaId && !item.Consumida);

        if (existente is not null)
        {
            existente.AlterarQuantidade(existente.Quantidade + quantidade);
            AtualizadaEm = DateTimeOffset.UtcNow;
            RecalcularOrcamentoSeExistir();

            RegistrarEvento(new PecaIncluidaNaOrdem(Id, existente.Id, pecaId, quantidade, existente.Subtotal.Valor));

            return existente;
        }

        var item = ItemPeca.Criar(Id, pecaId, codigoPeca, nomePeca, precoUnitario, quantidade);

        _itensPeca.Add(item);
        AtualizadaEm = DateTimeOffset.UtcNow;
        RecalcularOrcamentoSeExistir();

        RegistrarEvento(new PecaIncluidaNaOrdem(Id, item.Id, pecaId, quantidade, item.Subtotal.Valor));

        return item;
    }

    /// <summary>Marca que a quantidade do item já foi separada no estoque.</summary>
    public void ConfirmarReservaDePeca(Guid itemPecaId)
    {
        var item = _itensPeca.FirstOrDefault(i => i.Id == itemPecaId)
            ?? throw new DomainException("ITEM_NAO_ENCONTRADO", "Item de peça não encontrado na Ordem de Serviço.");

        item.MarcarComoReservada();
    }

    /// <summary>
    /// Marca que a reserva do item foi desfeita no estoque. Chamado no cancelamento e na
    /// reprovação do orçamento, para que a OS não fique alegando reservas que já não existem.
    /// Não há guarda de status: liberar reserva é justamente o que se faz em uma OS encerrada.
    /// </summary>
    public void ConfirmarLiberacaoDeReservaDePeca(Guid itemPecaId)
    {
        var item = _itensPeca.FirstOrDefault(i => i.Id == itemPecaId)
            ?? throw new DomainException("ITEM_NAO_ENCONTRADO", "Item de peça não encontrado na Ordem de Serviço.");

        item.MarcarComoLiberada();
    }

    /// <summary>Remove um item de serviço da OS.</summary>
    public void RemoverServico(Guid itemServicoId)
    {
        GarantirItensAlteraveis();

        var item = _itensServico.FirstOrDefault(i => i.Id == itemServicoId)
            ?? throw new DomainException("ITEM_NAO_ENCONTRADO", "Item de serviço não encontrado na Ordem de Serviço.");

        _itensServico.Remove(item);
        AtualizadaEm = DateTimeOffset.UtcNow;
        RecalcularOrcamentoSeExistir();

        RegistrarEvento(new ItemRemovidoDaOrdem(Id, item.Id, false, item.ServicoId, item.Quantidade));
    }

    /// <summary>
    /// Remove um item de peça da OS. A liberação da reserva no estoque é feita pela camada
    /// de aplicação, a partir do evento <see cref="ItemRemovidoDaOrdem"/>.
    /// </summary>
    public void RemoverPeca(Guid itemPecaId)
    {
        GarantirItensAlteraveis();

        var item = _itensPeca.FirstOrDefault(i => i.Id == itemPecaId)
            ?? throw new DomainException("ITEM_NAO_ENCONTRADO", "Item de peça não encontrado na Ordem de Serviço.");

        if (item.Consumida)
        {
            throw new DomainException(
                "PECA_JA_CONSUMIDA",
                $"A peça '{item.CodigoPeca}' já foi aplicada no veículo e não pode ser removida.");
        }

        _itensPeca.Remove(item);
        AtualizadaEm = DateTimeOffset.UtcNow;
        RecalcularOrcamentoSeExistir();

        RegistrarEvento(new ItemRemovidoDaOrdem(Id, item.Id, true, item.PecaId, item.Quantidade));
    }

    /// <summary>Altera a quantidade de um item de serviço já incluído.</summary>
    public void AlterarQuantidadeDeServico(Guid itemServicoId, int novaQuantidade)
    {
        GarantirItensAlteraveis();

        var item = _itensServico.FirstOrDefault(i => i.Id == itemServicoId)
            ?? throw new DomainException("ITEM_NAO_ENCONTRADO", "Item de serviço não encontrado na Ordem de Serviço.");

        item.AlterarQuantidade(novaQuantidade);
        AtualizadaEm = DateTimeOffset.UtcNow;
        RecalcularOrcamentoSeExistir();
    }

    // ---------------------------------------------------------------------
    // Orçamento
    // ---------------------------------------------------------------------

    /// <summary>
    /// Gera (ou regenera) o orçamento a partir dos itens atuais. O valor <b>nunca</b> é
    /// informado por quem chama — é sempre a soma dos itens com o desconto aplicado.
    /// </summary>
    /// <param name="percentualDesconto">Desconto comercial de 0 a 100.</param>
    public Orcamento GerarOrcamento(decimal percentualDesconto = 0m)
    {
        GarantirNaoTerminal();

        if (!Status.PermiteAlterarItens())
        {
            throw new DomainException(
                "TRANSICAO_INVALIDA",
                $"O orçamento só pode ser gerado com a OS em '{StatusOrdemServico.Recebida.Descricao()}' ou '{StatusOrdemServico.EmDiagnostico.Descricao()}'. Situação atual: '{Status.Descricao()}'.");
        }

        if (_itensServico.Count == 0 && _itensPeca.Count == 0)
        {
            throw new DomainException(
                "ORCAMENTO_SEM_ITENS",
                "Inclua ao menos um serviço ou peça antes de gerar o orçamento.");
        }

        if (Orcamento is null)
        {
            Orcamento = Orcamento.Gerar(Id, ValorTotalServicos, ValorTotalPecas, percentualDesconto);
        }
        else
        {
            Orcamento.ReabrirParaEdicao();
            Orcamento.Recalcular(ValorTotalServicos, ValorTotalPecas, percentualDesconto);
        }

        AtualizadaEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new OrcamentoGerado(
            Id, Orcamento.Id, Orcamento.ValorServicos.Valor, Orcamento.ValorPecas.Valor, Orcamento.ValorTotal.Valor));

        return Orcamento;
    }

    /// <summary>
    /// Envia o orçamento ao cliente e move a OS para <c>AguardandoAprovacao</c>.
    /// A partir daqui os itens ficam congelados.
    /// </summary>
    public void EnviarOrcamentoParaAprovacao(int validadeEmDias = Orcamento.ValidadePadraoEmDias)
    {
        ExigirStatus(
            StatusOrdemServico.AguardandoAprovacao,
            [StatusOrdemServico.Recebida, StatusOrdemServico.EmDiagnostico]);

        var orcamento = Orcamento
            ?? throw new DomainException("ORCAMENTO_INEXISTENTE", "Gere o orçamento antes de enviá-lo ao cliente.");

        orcamento.EnviarParaAprovacao(validadeEmDias);

        AlterarStatus(
            StatusOrdemServico.AguardandoAprovacao,
            $"Orçamento de {orcamento.ValorTotal} enviado ao cliente.",
            ResponsavelId);

        RegistrarEvento(new OrcamentoEnviadoAoCliente(
            Id, orcamento.Id, ClienteId, orcamento.ValorTotal.Valor, orcamento.ValidoAte!.Value));
    }

    /// <summary>
    /// Registra a aprovação do cliente e move a OS diretamente para <c>EmExecucao</c>.
    /// Esta é a transição que autoriza o consumo das peças reservadas.
    /// </summary>
    public void AprovarOrcamento()
    {
        ExigirStatus(
            StatusOrdemServico.EmExecucao,
            [StatusOrdemServico.AguardandoAprovacao]);

        var orcamento = Orcamento
            ?? throw new DomainException("ORCAMENTO_INEXISTENTE", "Não há orçamento para aprovar.");

        orcamento.Aprovar();

        ExecucaoIniciadaEm = DateTimeOffset.UtcNow;

        AlterarStatus(StatusOrdemServico.EmExecucao, "Orçamento aprovado pelo cliente. Execução iniciada.", null);

        RegistrarEvento(new OrcamentoAprovadoPeloCliente(Id, orcamento.Id, orcamento.ValorTotal.Valor));
        RegistrarEvento(new ExecucaoIniciada(Id, ExecucaoIniciadaEm.Value));
    }

    /// <summary>
    /// Registra a reprovação do cliente e cancela a OS. As reservas de peças são liberadas
    /// pela camada de aplicação a partir do evento emitido.
    /// </summary>
    public void ReprovarOrcamento(string? motivo)
    {
        ExigirStatus(
            StatusOrdemServico.Cancelada,
            [StatusOrdemServico.AguardandoAprovacao]);

        var orcamento = Orcamento
            ?? throw new DomainException("ORCAMENTO_INEXISTENTE", "Não há orçamento para reprovar.");

        orcamento.Reprovar(motivo);

        MotivoCancelamento = $"Orçamento reprovado pelo cliente: {orcamento.MotivoReprovacao}";

        AlterarStatus(StatusOrdemServico.Cancelada, MotivoCancelamento, null);

        RegistrarEvento(new OrcamentoReprovadoPeloCliente(Id, orcamento.Id, orcamento.MotivoReprovacao!));
        RegistrarEvento(new OrdemDeServicoCancelada(Id, MotivoCancelamento));
    }

    /// <summary>
    /// Expira o orçamento vencido e cancela a OS. Executado por rotina de manutenção, não
    /// por ação humana.
    /// </summary>
    public void ExpirarOrcamento(DateTimeOffset agora)
    {
        if (Status != StatusOrdemServico.AguardandoAprovacao || Orcamento is null)
        {
            return;
        }

        if (!Orcamento.EstaVencido(agora))
        {
            return;
        }

        Orcamento.Expirar();

        MotivoCancelamento = "Orçamento expirado sem resposta do cliente.";

        AlterarStatus(StatusOrdemServico.Cancelada, MotivoCancelamento, null);

        RegistrarEvento(new OrcamentoExpirado(Id, Orcamento.Id));
        RegistrarEvento(new OrdemDeServicoCancelada(Id, MotivoCancelamento));
    }

    /// <summary>
    /// Devolve a OS ao diagnóstico para revisão do orçamento — usado quando o cliente pede
    /// alteração de escopo antes de decidir.
    /// </summary>
    public void RetornarParaDiagnostico(string? motivo = null)
    {
        ExigirStatus(
            StatusOrdemServico.EmDiagnostico,
            [StatusOrdemServico.AguardandoAprovacao]);

        Orcamento?.ReabrirParaEdicao();

        AlterarStatus(
            StatusOrdemServico.EmDiagnostico,
            motivo ?? "Orçamento devolvido para revisão.",
            ResponsavelId);
    }

    // ---------------------------------------------------------------------
    // Execução e entrega
    // ---------------------------------------------------------------------

    /// <summary>Marca a peça como efetivamente aplicada no veículo.</summary>
    public void ConfirmarConsumoDePeca(Guid itemPecaId)
    {
        if (Status != StatusOrdemServico.EmExecucao)
        {
            throw new DomainException(
                "TRANSICAO_INVALIDA",
                "Peças só podem ser consumidas com a Ordem de Serviço em execução.");
        }

        var item = _itensPeca.FirstOrDefault(i => i.Id == itemPecaId)
            ?? throw new DomainException("ITEM_NAO_ENCONTRADO", "Item de peça não encontrado na Ordem de Serviço.");

        item.MarcarComoConsumida();
        AtualizadaEm = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Conclui os serviços e move a OS para <c>Finalizada</c>. É aqui que a duração real da
    /// execução passa a existir, alimentando o indicador de tempo médio.
    /// </summary>
    public void FinalizarServico(string? observacao = null, Guid? responsavelId = null)
    {
        ExigirStatus(
            StatusOrdemServico.Finalizada,
            [StatusOrdemServico.EmExecucao]);

        FinalizadaEm = DateTimeOffset.UtcNow;

        AlterarStatus(StatusOrdemServico.Finalizada, observacao ?? "Serviços concluídos. Veículo pronto para retirada.", responsavelId);

        var duracao = (int)Math.Round((FinalizadaEm.Value - ExecucaoIniciadaEm!.Value).TotalMinutes);

        RegistrarEvento(new ServicoFinalizado(Id, FinalizadaEm.Value, duracao));
    }

    /// <summary>Registra a entrega do veículo. Estado terminal de sucesso.</summary>
    public void EntregarVeiculo(string? observacao = null, Guid? responsavelId = null)
    {
        ExigirStatus(
            StatusOrdemServico.Entregue,
            [StatusOrdemServico.Finalizada]);

        EntregueEm = DateTimeOffset.UtcNow;

        AlterarStatus(StatusOrdemServico.Entregue, observacao ?? "Veículo entregue ao cliente.", responsavelId);

        RegistrarEvento(new VeiculoEntregueAoCliente(Id, ClienteId, VeiculoId, EntregueEm.Value));
    }

    /// <summary>
    /// Cancela a OS antes da execução. Não é permitido após a aprovação do orçamento, pois
    /// nesse ponto peças já saíram do estoque e horas já foram trabalhadas.
    /// </summary>
    public void Cancelar(string? motivo, Guid? responsavelId = null)
    {
        GarantirNaoTerminal();

        if (!Status.PermiteCancelamento())
        {
            throw new DomainException(
                "CANCELAMENTO_NAO_PERMITIDO",
                $"Uma Ordem de Serviço em '{Status.Descricao()}' não pode ser cancelada.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException("MOTIVO_OBRIGATORIO", "Informe o motivo do cancelamento.");
        }

        Orcamento?.ReabrirParaEdicao();

        MotivoCancelamento = motivo.Trim();

        AlterarStatus(StatusOrdemServico.Cancelada, MotivoCancelamento, responsavelId);

        RegistrarEvento(new OrdemDeServicoCancelada(Id, MotivoCancelamento));
    }

    /// <summary>Troca o responsável técnico pela OS.</summary>
    public void AtribuirResponsavel(Guid responsavelId)
    {
        GarantirNaoTerminal();

        if (responsavelId == Guid.Empty)
        {
            throw new DomainException("RESPONSAVEL_OBRIGATORIO", "Informe o responsável pela Ordem de Serviço.");
        }

        ResponsavelId = responsavelId;
        AtualizadaEm = DateTimeOffset.UtcNow;
    }

    // ---------------------------------------------------------------------
    // Apoio à máquina de estados
    // ---------------------------------------------------------------------

    private void AlterarStatus(StatusOrdemServico novoStatus, string? observacao, Guid? usuarioId)
    {
        var anterior = Status;

        Status = novoStatus;
        AtualizadaEm = DateTimeOffset.UtcNow;

        RegistrarTransicao(anterior, novoStatus, observacao, usuarioId);
        RegistrarEvento(new StatusDaOrdemAlterado(Id, Numero.Valor, anterior, novoStatus));
    }

    private void RegistrarTransicao(
        StatusOrdemServico? anterior,
        StatusOrdemServico atual,
        string? observacao,
        Guid? usuarioId) =>
        _historico.Add(HistoricoStatus.Registrar(Id, anterior, atual, observacao, usuarioId));

    /// <summary>
    /// Valida a transição pretendida contra a lista de estados de origem permitidos,
    /// produzindo uma mensagem de erro que diz exatamente o que era esperado.
    /// </summary>
    private void ExigirStatus(StatusOrdemServico destino, StatusOrdemServico[] origensPermitidas)
    {
        GarantirNaoTerminal();

        if (!origensPermitidas.Contains(Status))
        {
            var esperados = string.Join("' ou '", origensPermitidas.Select(s => s.Descricao()));

            throw new DomainException(
                "TRANSICAO_INVALIDA",
                $"Não é possível mover a Ordem de Serviço para '{destino.Descricao()}' a partir de '{Status.Descricao()}'. Situação esperada: '{esperados}'.");
        }
    }

    private void GarantirNaoTerminal()
    {
        if (Status.EhTerminal())
        {
            throw new DomainException(
                "ORDEM_ENCERRADA",
                $"A Ordem de Serviço {Numero} está '{Status.Descricao()}' e não admite novas alterações.");
        }
    }

    private void GarantirItensAlteraveis()
    {
        GarantirNaoTerminal();

        if (!Status.PermiteAlterarItens())
        {
            throw new DomainException(
                "ITENS_CONGELADOS",
                $"Os itens não podem ser alterados com a OS em '{Status.Descricao()}'. Devolva-a para diagnóstico primeiro.");
        }
    }

    /// <summary>
    /// Mantém o orçamento sincronizado com os itens enquanto ele ainda está em elaboração.
    /// Se já foi enviado, nada acontece — os itens estarão congelados de qualquer forma.
    /// </summary>
    private void RecalcularOrcamentoSeExistir()
    {
        if (Orcamento is { Status: StatusOrcamento.EmElaboracao })
        {
            Orcamento.Recalcular(ValorTotalServicos, ValorTotalPecas);
        }
    }
}
