using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.Domain.OrdensServico;

/// <summary>
/// <b>Entidade filha</b> do agregado Ordem de Serviço: um serviço solicitado nesta OS
/// (troca de óleo, alinhamento…).
/// <para>
/// Os campos <see cref="Descricao"/>, <see cref="PrecoUnitario"/> e
/// <see cref="TempoEstimadoEmMinutos"/> são <b>cópias congeladas</b> do catálogo no momento
/// da inclusão. Isso é deliberado: um reajuste de tabela feito depois não pode alterar o
/// valor de um orçamento já apresentado ao cliente.
/// </para>
/// </summary>
public sealed class ItemServico : Entity
{
    private ItemServico()
    {
        Descricao = null!;
        PrecoUnitario = null!;
    }

    private ItemServico(
        Guid id,
        Guid ordemServicoId,
        Guid servicoId,
        string descricao,
        Dinheiro precoUnitario,
        int quantidade,
        int tempoEstimadoEmMinutos)
        : base(id)
    {
        OrdemServicoId = ordemServicoId;
        ServicoId = servicoId;
        Descricao = descricao;
        PrecoUnitario = precoUnitario;
        Quantidade = quantidade;
        TempoEstimadoEmMinutos = tempoEstimadoEmMinutos;
        AdicionadoEm = DateTimeOffset.UtcNow;
    }

    public Guid OrdemServicoId { get; private set; }

    /// <summary>Referência ao serviço no catálogo (agregado <c>Servico</c>), por identidade.</summary>
    public Guid ServicoId { get; private set; }

    /// <summary>Nome do serviço no momento da inclusão.</summary>
    public string Descricao { get; private set; }

    /// <summary>Preço de tabela congelado no momento da inclusão.</summary>
    public Dinheiro PrecoUnitario { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Tempo estimado unitário, congelado. Base da previsão de entrega.</summary>
    public int TempoEstimadoEmMinutos { get; private set; }

    public DateTimeOffset AdicionadoEm { get; private set; }

    /// <summary>Valor do item: preço unitário × quantidade.</summary>
    public Dinheiro Subtotal => PrecoUnitario.Multiplicar(Quantidade);

    /// <summary>Tempo total estimado do item, em minutos.</summary>
    public int TempoTotalEstimadoEmMinutos => TempoEstimadoEmMinutos * Quantidade;

    internal static ItemServico Criar(
        Guid ordemServicoId,
        Guid servicoId,
        string descricao,
        decimal precoUnitario,
        int quantidade,
        int tempoEstimadoEmMinutos)
    {
        if (servicoId == Guid.Empty)
        {
            throw new DomainException("SERVICO_OBRIGATORIO", "O serviço do item é obrigatório.");
        }

        ValidarQuantidade(quantidade);

        return new ItemServico(
            NovoId(),
            ordemServicoId,
            servicoId,
            string.IsNullOrWhiteSpace(descricao)
                ? throw new DomainException("DESCRICAO_OBRIGATORIA", "A descrição do serviço é obrigatória.")
                : descricao.Trim(),
            Dinheiro.De(precoUnitario),
            quantidade,
            tempoEstimadoEmMinutos < 0 ? 0 : tempoEstimadoEmMinutos);
    }

    /// <summary>Altera a quantidade do item. Só é acessível através da raiz do agregado.</summary>
    internal void AlterarQuantidade(int novaQuantidade)
    {
        ValidarQuantidade(novaQuantidade);
        Quantidade = novaQuantidade;
    }

    private static void ValidarQuantidade(int quantidade)
    {
        if (quantidade is < 1 or > 999)
        {
            throw new DomainException("QUANTIDADE_INVALIDA", "A quantidade do serviço deve estar entre 1 e 999.");
        }
    }
}
