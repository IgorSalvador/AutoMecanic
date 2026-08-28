using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.Domain.OrdensServico;

/// <summary>
/// <b>Entidade filha</b> do agregado Ordem de Serviço que representa o orçamento apresentado
/// ao cliente.
/// <para>
/// O orçamento é <b>gerado automaticamente</b> a partir dos itens de serviço e de peça da OS
/// — nunca digitado. Uma vez enviado ao cliente, os valores ficam congelados: alterar itens
/// exige voltar a OS para diagnóstico e gerar um novo orçamento, de modo que o cliente jamais
/// aprove um valor diferente do que viu.
/// </para>
/// </summary>
public sealed class Orcamento : Entity
{
    /// <summary>Prazo padrão de validade do orçamento, em dias corridos.</summary>
    public const int ValidadePadraoEmDias = 7;

    private Orcamento()
    {
        ValorServicos = null!;
        ValorPecas = null!;
        ValorTotal = null!;
    }

    private Orcamento(
        Guid id,
        Guid ordemServicoId,
        Dinheiro valorServicos,
        Dinheiro valorPecas,
        decimal percentualDesconto,
        Dinheiro valorTotal)
        : base(id)
    {
        OrdemServicoId = ordemServicoId;
        ValorServicos = valorServicos;
        ValorPecas = valorPecas;
        PercentualDesconto = percentualDesconto;
        ValorTotal = valorTotal;
        Status = StatusOrcamento.EmElaboracao;
        GeradoEm = DateTimeOffset.UtcNow;
    }

    public Guid OrdemServicoId { get; private set; }

    /// <summary>Soma dos subtotais dos itens de serviço.</summary>
    public Dinheiro ValorServicos { get; private set; }

    /// <summary>Soma dos subtotais dos itens de peça.</summary>
    public Dinheiro ValorPecas { get; private set; }

    /// <summary>Desconto comercial concedido, de 0 a 100.</summary>
    public decimal PercentualDesconto { get; private set; }

    /// <summary>(Serviços + Peças) com o desconto aplicado. É o valor que o cliente aprova.</summary>
    public Dinheiro ValorTotal { get; private set; }

    public StatusOrcamento Status { get; private set; }

    public DateTimeOffset GeradoEm { get; private set; }

    public DateTimeOffset? EnviadoEm { get; private set; }

    /// <summary>Data-limite para o cliente responder. Depois dela o orçamento pode ser expirado.</summary>
    public DateTimeOffset? ValidoAte { get; private set; }

    public DateTimeOffset? RespondidoEm { get; private set; }

    /// <summary>Preenchido quando o cliente reprova o orçamento.</summary>
    public string? MotivoReprovacao { get; private set; }

    /// <summary>Valor bruto, antes do desconto.</summary>
    public Dinheiro ValorBruto => ValorServicos.Somar(ValorPecas);

    /// <summary>Valor em reais abatido pelo desconto.</summary>
    public Dinheiro ValorDesconto => ValorBruto.Subtrair(ValorTotal);

    public bool FoiRespondido => Status is StatusOrcamento.Aprovado or StatusOrcamento.Reprovado;

    public bool EstaVencido(DateTimeOffset agora) =>
        Status == StatusOrcamento.AguardandoAprovacao && ValidoAte is not null && agora > ValidoAte;

    internal static Orcamento Gerar(
        Guid ordemServicoId,
        Dinheiro valorServicos,
        Dinheiro valorPecas,
        decimal percentualDesconto)
    {
        if (percentualDesconto is < 0 or > 100)
        {
            throw new DomainException("DESCONTO_INVALIDO", "O percentual de desconto deve estar entre 0 e 100.");
        }

        var bruto = valorServicos.Somar(valorPecas);

        if (bruto.EhZero)
        {
            throw new DomainException(
                "ORCAMENTO_SEM_ITENS",
                "Não é possível gerar um orçamento sem serviços ou peças.");
        }

        return new Orcamento(
            NovoId(),
            ordemServicoId,
            valorServicos,
            valorPecas,
            percentualDesconto,
            bruto.AplicarDescontoPercentual(percentualDesconto));
    }

    /// <summary>
    /// Recalcula os valores quando os itens da OS mudam. Só é permitido antes do envio ao
    /// cliente — depois disso, o valor apresentado é imutável.
    /// </summary>
    internal void Recalcular(Dinheiro valorServicos, Dinheiro valorPecas, decimal? percentualDesconto = null)
    {
        GarantirEmElaboracao("recalculado");

        if (percentualDesconto is not null)
        {
            if (percentualDesconto is < 0 or > 100)
            {
                throw new DomainException("DESCONTO_INVALIDO", "O percentual de desconto deve estar entre 0 e 100.");
            }

            PercentualDesconto = percentualDesconto.Value;
        }

        ValorServicos = valorServicos;
        ValorPecas = valorPecas;
        ValorTotal = valorServicos.Somar(valorPecas).AplicarDescontoPercentual(PercentualDesconto);
    }

    /// <summary>Envia o orçamento ao cliente e inicia a contagem do prazo de validade.</summary>
    internal void EnviarParaAprovacao(int validadeEmDias = ValidadePadraoEmDias)
    {
        GarantirEmElaboracao("enviado");

        if (ValorTotal.EhZero)
        {
            throw new DomainException("ORCAMENTO_SEM_ITENS", "Não é possível enviar um orçamento de valor zero.");
        }

        if (validadeEmDias is < 1 or > 90)
        {
            throw new DomainException("VALIDADE_INVALIDA", "A validade do orçamento deve estar entre 1 e 90 dias.");
        }

        Status = StatusOrcamento.AguardandoAprovacao;
        EnviadoEm = DateTimeOffset.UtcNow;
        ValidoAte = EnviadoEm.Value.AddDays(validadeEmDias);
    }

    internal void Aprovar()
    {
        GarantirAguardandoAprovacao();

        Status = StatusOrcamento.Aprovado;
        RespondidoEm = DateTimeOffset.UtcNow;
    }

    internal void Reprovar(string? motivo)
    {
        GarantirAguardandoAprovacao();

        Status = StatusOrcamento.Reprovado;
        RespondidoEm = DateTimeOffset.UtcNow;
        MotivoReprovacao = string.IsNullOrWhiteSpace(motivo) ? "Não informado pelo cliente" : motivo.Trim();
    }

    internal void Expirar()
    {
        if (Status != StatusOrcamento.AguardandoAprovacao)
        {
            throw new DomainException(
                "ORCAMENTO_NAO_EXPIRAVEL",
                $"Um orçamento com situação '{Status}' não pode ser expirado.");
        }

        Status = StatusOrcamento.Expirado;
        RespondidoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Reabre o orçamento para edição quando a OS retorna ao diagnóstico.</summary>
    internal void ReabrirParaEdicao()
    {
        if (Status is StatusOrcamento.Aprovado)
        {
            throw new DomainException(
                "ORCAMENTO_APROVADO",
                "Um orçamento já aprovado pelo cliente não pode ser reaberto para edição.");
        }

        Status = StatusOrcamento.EmElaboracao;
        EnviadoEm = null;
        ValidoAte = null;
        RespondidoEm = null;
        MotivoReprovacao = null;
    }

    private void GarantirEmElaboracao(string acao)
    {
        if (Status != StatusOrcamento.EmElaboracao)
        {
            throw new DomainException(
                "ORCAMENTO_NAO_EDITAVEL",
                $"O orçamento já foi enviado ao cliente (situação '{Status}') e não pode ser {acao}.");
        }
    }

    private void GarantirAguardandoAprovacao()
    {
        if (Status != StatusOrcamento.AguardandoAprovacao)
        {
            throw new DomainException(
                "ORCAMENTO_NAO_RESPONDIVEL",
                $"O orçamento está com situação '{Status}' e não aguarda resposta do cliente.");
        }
    }
}
