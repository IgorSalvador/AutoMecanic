using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.Domain.OrdensServico;

/// <summary>
/// <b>Entidade filha</b> do agregado Ordem de Serviço: uma peça ou insumo necessário
/// para executar os serviços desta OS.
/// <para>
/// Assim como o item de serviço, guarda uma <b>cópia congelada</b> do código, nome e preço
/// da peça no momento da inclusão. <see cref="Reservada"/> registra se a quantidade já foi
/// separada no estoque, mantendo a Ordem de Serviço e o razão de estoque coerentes.
/// </para>
/// </summary>
public sealed class ItemPeca : Entity
{
    private ItemPeca()
    {
        CodigoPeca = null!;
        NomePeca = null!;
        PrecoUnitario = null!;
    }

    private ItemPeca(
        Guid id,
        Guid ordemServicoId,
        Guid pecaId,
        string codigoPeca,
        string nomePeca,
        Dinheiro precoUnitario,
        int quantidade)
        : base(id)
    {
        OrdemServicoId = ordemServicoId;
        PecaId = pecaId;
        CodigoPeca = codigoPeca;
        NomePeca = nomePeca;
        PrecoUnitario = precoUnitario;
        Quantidade = quantidade;
        Reservada = false;
        Consumida = false;
        AdicionadoEm = DateTimeOffset.UtcNow;
    }

    public Guid OrdemServicoId { get; private set; }

    /// <summary>Referência à peça no estoque (agregado <c>Peca</c>), por identidade.</summary>
    public Guid PecaId { get; private set; }

    /// <summary>Código/SKU congelado no momento da inclusão.</summary>
    public string CodigoPeca { get; private set; }

    /// <summary>Nome congelado no momento da inclusão.</summary>
    public string NomePeca { get; private set; }

    /// <summary>Preço de venda congelado no momento da inclusão.</summary>
    public Dinheiro PrecoUnitario { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Quantidade já separada no estoque para esta OS.</summary>
    public bool Reservada { get; private set; }

    /// <summary>Quantidade já baixada do estoque (peça efetivamente aplicada no veículo).</summary>
    public bool Consumida { get; private set; }

    public DateTimeOffset AdicionadoEm { get; private set; }

    public Dinheiro Subtotal => PrecoUnitario.Multiplicar(Quantidade);

    internal static ItemPeca Criar(
        Guid ordemServicoId,
        Guid pecaId,
        string codigoPeca,
        string nomePeca,
        decimal precoUnitario,
        int quantidade)
    {
        if (pecaId == Guid.Empty)
        {
            throw new DomainException("PECA_OBRIGATORIA", "A peça do item é obrigatória.");
        }

        ValidarQuantidade(quantidade);

        return new ItemPeca(
            NovoId(),
            ordemServicoId,
            pecaId,
            string.IsNullOrWhiteSpace(codigoPeca)
                ? throw new DomainException("CODIGO_OBRIGATORIO", "O código da peça é obrigatório.")
                : codigoPeca.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(nomePeca)
                ? throw new DomainException("NOME_OBRIGATORIO", "O nome da peça é obrigatório.")
                : nomePeca.Trim(),
            Dinheiro.De(precoUnitario),
            quantidade);
    }

    internal void AlterarQuantidade(int novaQuantidade)
    {
        if (Consumida)
        {
            throw new DomainException(
                "PECA_JA_CONSUMIDA",
                $"A peça '{CodigoPeca}' já foi aplicada no veículo e não pode ter a quantidade alterada.");
        }

        ValidarQuantidade(novaQuantidade);
        Quantidade = novaQuantidade;
    }

    internal void MarcarComoReservada() => Reservada = true;

    internal void MarcarComoLiberada() => Reservada = false;

    internal void MarcarComoConsumida()
    {
        Reservada = false;
        Consumida = true;
    }

    private static void ValidarQuantidade(int quantidade)
    {
        if (quantidade is < 1 or > 9_999)
        {
            throw new DomainException("QUANTIDADE_INVALIDA", "A quantidade da peça deve estar entre 1 e 9.999.");
        }
    }
}
