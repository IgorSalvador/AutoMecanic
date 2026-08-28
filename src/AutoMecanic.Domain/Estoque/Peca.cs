using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Estoque.Events;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.Domain.Estoque;

/// <summary>
/// <b>Raiz de Agregado</b> do contexto Estoque: uma peça ou insumo controlado pela oficina.
/// <para>
/// O agregado é a <b>fronteira de consistência do saldo</b>. Toda entrada, saída, reserva e
/// ajuste passa por aqui, de modo que é impossível vender uma peça que não existe.
/// </para>
/// <para>
/// O saldo é modelado em duas parcelas: <see cref="QuantidadeEmEstoque"/> (o que está
/// fisicamente na prateleira) e <see cref="QuantidadeReservada"/> (o que já está comprometido
/// com orçamentos aguardando aprovação). O que o atendente pode prometer é a diferença entre
/// as duas — <see cref="QuantidadeDisponivel"/>. Sem essa distinção, duas OS simultâneas
/// poderiam prometer a mesma última peça ao cliente.
/// </para>
/// <para><b>Invariantes:</b> código único; preço maior que zero; saldo nunca negativo;
/// reservado nunca maior que o saldo físico.</para>
/// </summary>
public sealed class Peca : AggregateRoot
{
    private const int ComprimentoMaximoCodigo = 40;
    private const int ComprimentoMinimoNome = 2;
    private const int ComprimentoMaximoNome = 150;
    private const int QuantidadeMaxima = 1_000_000;

    private Peca()
    {
        Codigo = null!;
        Nome = null!;
        PrecoUnitario = null!;
    }

    private Peca(
        Guid id,
        string codigo,
        string nome,
        string? descricao,
        UnidadeMedida unidadeMedida,
        Dinheiro precoUnitario,
        int quantidadeEmEstoque,
        int estoqueMinimo)
        : base(id)
    {
        Codigo = codigo;
        Nome = nome;
        Descricao = descricao;
        UnidadeMedida = unidadeMedida;
        PrecoUnitario = precoUnitario;
        QuantidadeEmEstoque = quantidadeEmEstoque;
        QuantidadeReservada = 0;
        EstoqueMinimo = estoqueMinimo;
        Ativo = true;
        CadastradoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Código interno / SKU. Chave natural usada pelo almoxarifado.</summary>
    public string Codigo { get; private set; }

    public string Nome { get; private set; }

    public string? Descricao { get; private set; }

    public UnidadeMedida UnidadeMedida { get; private set; }

    /// <summary>Preço de venda vigente. Copiado para o item da OS no momento da inclusão.</summary>
    public Dinheiro PrecoUnitario { get; private set; }

    /// <summary>Saldo físico na prateleira.</summary>
    public int QuantidadeEmEstoque { get; private set; }

    /// <summary>Parcela do saldo físico já comprometida com orçamentos pendentes de aprovação.</summary>
    public int QuantidadeReservada { get; private set; }

    /// <summary>Ponto de ressuprimento: abaixo dele, o alerta de compra é disparado.</summary>
    public int EstoqueMinimo { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset CadastradoEm { get; private set; }

    public DateTimeOffset? AtualizadoEm { get; private set; }

    /// <summary>Quantidade que pode ser efetivamente prometida a uma nova Ordem de Serviço.</summary>
    public int QuantidadeDisponivel => QuantidadeEmEstoque - QuantidadeReservada;

    /// <summary>Indica que o disponível está no ponto de ressuprimento ou abaixo dele.</summary>
    public bool AbaixoDoEstoqueMinimo => QuantidadeDisponivel <= EstoqueMinimo;

    public static Peca Cadastrar(
        string? codigo,
        string? nome,
        string? descricao,
        UnidadeMedida unidadeMedida,
        decimal precoUnitario,
        int quantidadeInicial,
        int estoqueMinimo)
    {
        ValidarQuantidade(quantidadeInicial, nameof(quantidadeInicial));
        ValidarQuantidade(estoqueMinimo, nameof(estoqueMinimo));

        var peca = new Peca(
            NovoId(),
            ValidarCodigo(codigo),
            ValidarNome(nome),
            NormalizarDescricao(descricao),
            unidadeMedida,
            ValidarPreco(precoUnitario),
            quantidadeInicial,
            estoqueMinimo);

        peca.RegistrarEvento(new PecaCadastrada(peca.Id, peca.Codigo, peca.Nome));

        if (quantidadeInicial > 0)
        {
            peca.RegistrarEvento(new EstoqueMovimentado(
                peca.Id, TipoMovimentoEstoque.Entrada, quantidadeInicial, 0, quantidadeInicial,
                "Saldo inicial de cadastro", null));
        }

        peca.AvaliarNivelMinimo();

        return peca;
    }

    public void AtualizarDados(string? nome, string? descricao, UnidadeMedida unidadeMedida, int estoqueMinimo)
    {
        GarantirPecaAtiva();
        ValidarQuantidade(estoqueMinimo, nameof(estoqueMinimo));

        Nome = ValidarNome(nome);
        Descricao = NormalizarDescricao(descricao);
        UnidadeMedida = unidadeMedida;
        EstoqueMinimo = estoqueMinimo;
        AtualizadoEm = DateTimeOffset.UtcNow;

        AvaliarNivelMinimo();
    }

    public void ReajustarPreco(decimal novoPreco)
    {
        GarantirPecaAtiva();

        var precoValidado = ValidarPreco(novoPreco);

        if (precoValidado == PrecoUnitario)
        {
            return;
        }

        var anterior = PrecoUnitario;
        PrecoUnitario = precoValidado;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new PrecoDaPecaReajustado(Id, anterior.Valor, precoValidado.Valor));
    }

    /// <summary>Entrada de mercadoria: recebimento de fornecedor ou devolução ao estoque.</summary>
    public void RegistrarEntrada(int quantidade, string motivo, Guid? ordemServicoId = null)
    {
        GarantirPecaAtiva();
        ExigirQuantidadePositiva(quantidade);

        var saldoAnterior = QuantidadeEmEstoque;

        if (saldoAnterior + quantidade > QuantidadeMaxima)
        {
            throw new DomainException(
                "ESTOQUE_EXCEDIDO",
                $"A entrada faria o saldo de '{Codigo}' ultrapassar o limite de {QuantidadeMaxima:N0}.");
        }

        QuantidadeEmEstoque += quantidade;
        AtualizadoEm = DateTimeOffset.UtcNow;

        var tipo = ordemServicoId is null ? TipoMovimentoEstoque.Entrada : TipoMovimentoEstoque.Estorno;

        RegistrarEvento(new EstoqueMovimentado(
            Id, tipo, quantidade, saldoAnterior, QuantidadeEmEstoque, ExigirMotivo(motivo), ordemServicoId));
    }

    /// <summary>
    /// Separa quantidade para um orçamento em elaboração. A peça continua fisicamente no
    /// estoque, mas deixa de ser prometível a outra OS.
    /// </summary>
    /// <exception cref="DomainException">Quando não há saldo disponível suficiente.</exception>
    public void Reservar(int quantidade, Guid ordemServicoId)
    {
        GarantirPecaAtiva();
        ExigirQuantidadePositiva(quantidade);

        if (quantidade > QuantidadeDisponivel)
        {
            throw new DomainException(
                "ESTOQUE_INSUFICIENTE",
                $"Estoque insuficiente para '{Nome}' ({Codigo}): disponível {QuantidadeDisponivel}, solicitado {quantidade}.");
        }

        QuantidadeReservada += quantidade;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new Events.QuantidadeReservada(Id, ordemServicoId, quantidade));

        AvaliarNivelMinimo();
    }

    /// <summary>Desfaz uma reserva sem consumir saldo (orçamento reprovado ou item removido).</summary>
    public void LiberarReserva(int quantidade, Guid ordemServicoId)
    {
        ExigirQuantidadePositiva(quantidade);

        if (quantidade > QuantidadeReservada)
        {
            throw new DomainException(
                "RESERVA_INVALIDA",
                $"Não é possível liberar {quantidade} de '{Codigo}': há apenas {QuantidadeReservada} reservado(s).");
        }

        QuantidadeReservada -= quantidade;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new ReservaLiberada(Id, ordemServicoId, quantidade));
    }

    /// <summary>
    /// Converte uma reserva em consumo efetivo: a peça sai da prateleira para o veículo.
    /// Chamado quando o orçamento é aprovado e o serviço entra em execução.
    /// </summary>
    public void ConsumirReserva(int quantidade, Guid ordemServicoId)
    {
        GarantirPecaAtiva();
        ExigirQuantidadePositiva(quantidade);

        if (quantidade > QuantidadeReservada)
        {
            throw new DomainException(
                "RESERVA_INVALIDA",
                $"Não é possível consumir {quantidade} de '{Codigo}': há apenas {QuantidadeReservada} reservado(s).");
        }

        var saldoAnterior = QuantidadeEmEstoque;

        QuantidadeReservada -= quantidade;
        QuantidadeEmEstoque -= quantidade;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new EstoqueMovimentado(
            Id, TipoMovimentoEstoque.Saida, quantidade, saldoAnterior, QuantidadeEmEstoque,
            "Consumo em Ordem de Serviço", ordemServicoId));

        AvaliarNivelMinimo();
    }

    /// <summary>Baixa direta, sem reserva prévia — usada em perdas, avarias e vencimentos.</summary>
    public void RegistrarPerda(int quantidade, string motivo)
    {
        GarantirPecaAtiva();
        ExigirQuantidadePositiva(quantidade);

        if (quantidade > QuantidadeDisponivel)
        {
            throw new DomainException(
                "ESTOQUE_INSUFICIENTE",
                $"Não é possível baixar {quantidade} de '{Codigo}': disponível {QuantidadeDisponivel}.");
        }

        var saldoAnterior = QuantidadeEmEstoque;

        QuantidadeEmEstoque -= quantidade;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new EstoqueMovimentado(
            Id, TipoMovimentoEstoque.Perda, quantidade, saldoAnterior, QuantidadeEmEstoque, ExigirMotivo(motivo), null));

        AvaliarNivelMinimo();
    }

    /// <summary>
    /// Acerta o saldo para a quantidade apurada em contagem física. O ajuste não pode
    /// deixar o saldo abaixo do que já está reservado — isso quebraria promessas já feitas.
    /// </summary>
    public void AjustarSaldo(int quantidadeApurada, string motivo)
    {
        GarantirPecaAtiva();
        ValidarQuantidade(quantidadeApurada, nameof(quantidadeApurada));

        if (quantidadeApurada < QuantidadeReservada)
        {
            throw new DomainException(
                "AJUSTE_INVALIDO",
                $"O saldo apurado ({quantidadeApurada}) é menor que a quantidade reservada ({QuantidadeReservada}) de '{Codigo}'.");
        }

        if (quantidadeApurada == QuantidadeEmEstoque)
        {
            return;
        }

        var saldoAnterior = QuantidadeEmEstoque;
        var diferenca = Math.Abs(quantidadeApurada - saldoAnterior);

        QuantidadeEmEstoque = quantidadeApurada;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new EstoqueMovimentado(
            Id, TipoMovimentoEstoque.Ajuste, diferenca, saldoAnterior, QuantidadeEmEstoque, ExigirMotivo(motivo), null));

        AvaliarNivelMinimo();
    }

    public void Inativar()
    {
        if (!Ativo)
        {
            return;
        }

        if (QuantidadeReservada > 0)
        {
            throw new DomainException(
                "PECA_RESERVADA",
                $"A peça '{Codigo}' possui {QuantidadeReservada} unidade(s) reservada(s) e não pode ser inativada.");
        }

        Ativo = false;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new PecaInativada(Id, Codigo));
    }

    public void Reativar()
    {
        if (Ativo)
        {
            return;
        }

        Ativo = true;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    public void GarantirPecaAtiva()
    {
        if (!Ativo)
        {
            throw new DomainException(
                "PECA_INATIVA",
                $"A peça '{Nome}' ({Codigo}) está inativa e não pode ser movimentada.");
        }
    }

    /// <summary>
    /// Emite o alerta de ressuprimento quando o disponível cruza o mínimo. Só emite se a
    /// peça estiver ativa: alertar sobre item fora de linha seria ruído para a compra.
    /// </summary>
    private void AvaliarNivelMinimo()
    {
        if (Ativo && AbaixoDoEstoqueMinimo)
        {
            RegistrarEvento(new EstoqueAtingiuNivelMinimo(Id, Codigo, Nome, QuantidadeDisponivel, EstoqueMinimo));
        }
    }

    private static string ValidarCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new DomainException("CODIGO_OBRIGATORIO", "O código da peça é obrigatório.");
        }

        var limpo = codigo.Trim().ToUpperInvariant();

        if (limpo.Length > ComprimentoMaximoCodigo)
        {
            throw new DomainException("CODIGO_INVALIDO", $"O código da peça excede {ComprimentoMaximoCodigo} caracteres.");
        }

        return limpo;
    }

    private static string ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("NOME_OBRIGATORIO", "O nome da peça é obrigatório.");
        }

        var limpo = nome.Trim();

        if (limpo.Length is < ComprimentoMinimoNome or > ComprimentoMaximoNome)
        {
            throw new DomainException(
                "NOME_INVALIDO",
                $"O nome da peça deve ter entre {ComprimentoMinimoNome} e {ComprimentoMaximoNome} caracteres.");
        }

        return limpo;
    }

    private static Dinheiro ValidarPreco(decimal preco)
    {
        if (preco <= 0)
        {
            throw new DomainException("PRECO_INVALIDO", "O preço unitário da peça deve ser maior que zero.");
        }

        return Dinheiro.De(preco);
    }

    private static void ValidarQuantidade(int quantidade, string campo)
    {
        if (quantidade is < 0 or > QuantidadeMaxima)
        {
            throw new DomainException(
                "QUANTIDADE_INVALIDA",
                $"O campo '{campo}' deve estar entre 0 e {QuantidadeMaxima:N0}.");
        }
    }

    private static void ExigirQuantidadePositiva(int quantidade)
    {
        if (quantidade <= 0)
        {
            throw new DomainException("QUANTIDADE_INVALIDA", "A quantidade movimentada deve ser maior que zero.");
        }

        if (quantidade > QuantidadeMaxima)
        {
            throw new DomainException("QUANTIDADE_INVALIDA", $"A quantidade excede o limite de {QuantidadeMaxima:N0}.");
        }
    }

    private static string ExigirMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new DomainException("MOTIVO_OBRIGATORIO", "O motivo da movimentação de estoque é obrigatório.");
        }

        return motivo.Trim();
    }

    private static string? NormalizarDescricao(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return null;
        }

        var limpa = descricao.Trim();

        if (limpa.Length > 500)
        {
            throw new DomainException("DESCRICAO_INVALIDA", "A descrição da peça excede 500 caracteres.");
        }

        return limpa;
    }
}
