namespace AutoMecanic.Domain.Estoque;

/// <summary>Unidade em que a peça ou insumo é controlado no estoque.</summary>
public enum UnidadeMedida
{
    /// <summary>Item contável avulso (uma vela, um filtro).</summary>
    Unidade = 1,

    /// <summary>Insumo líquido (óleo, fluido de freio).</summary>
    Litro = 2,

    /// <summary>Insumo vendido por comprimento (mangueira, cabo).</summary>
    Metro = 3,

    /// <summary>Insumo vendido por peso (graxa, massa).</summary>
    Quilograma = 4,

    /// <summary>Conjunto vendido fechado (jogo de pastilhas, kit de embreagem).</summary>
    Jogo = 5,

    /// <summary>Frasco ou embalagem fechada (aditivo, spray).</summary>
    Frasco = 6
}

/// <summary>
/// Natureza de um lançamento no razão de estoque. O razão é <b>append-only</b>: nenhum
/// movimento é alterado ou excluído, o que torna o saldo auditável e reconstituível.
/// </summary>
public enum TipoMovimentoEstoque
{
    /// <summary>Compra/recebimento de fornecedor: aumenta o saldo.</summary>
    Entrada = 1,

    /// <summary>Consumo em uma Ordem de Serviço: reduz o saldo.</summary>
    Saida = 2,

    /// <summary>Correção após contagem física: acerta o saldo para o valor real.</summary>
    Ajuste = 3,

    /// <summary>Devolução de peça não utilizada ao estoque: aumenta o saldo.</summary>
    Estorno = 4,

    /// <summary>Baixa por perda, avaria ou vencimento: reduz o saldo.</summary>
    Perda = 5
}
