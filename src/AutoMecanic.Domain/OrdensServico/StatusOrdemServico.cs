namespace AutoMecanic.Domain.OrdensServico;

/// <summary>
/// Estados do ciclo de vida da Ordem de Serviço, exatamente como definidos pelo negócio.
/// <para>
/// A transição entre estados <b>nunca</b> é atribuída diretamente: ela é sempre consequência
/// de uma ação executada no sistema (registrar diagnóstico, aprovar orçamento, finalizar
/// serviço). É isso que o requisito chama de "alteração automática dos status conforme ações".
/// </para>
/// <code>
/// Recebida ──▶ EmDiagnostico ──▶ AguardandoAprovacao ──▶ EmExecucao ──▶ Finalizada ──▶ Entregue
///     │              │                    │
///     └──────────────┴────────────────────┴──────────▶ Cancelada
/// </code>
/// </summary>
public enum StatusOrdemServico
{
    /// <summary>O veículo foi recebido na oficina e a OS foi aberta. Aguarda diagnóstico.</summary>
    Recebida = 1,

    /// <summary>Um mecânico está avaliando o veículo e montando a lista de serviços e peças.</summary>
    EmDiagnostico = 2,

    /// <summary>O orçamento foi enviado ao cliente e aguarda aprovação ou reprovação.</summary>
    AguardandoAprovacao = 3,

    /// <summary>O cliente aprovou o orçamento e os serviços estão sendo executados.</summary>
    EmExecucao = 4,

    /// <summary>Todos os serviços foram concluídos. O veículo aguarda retirada.</summary>
    Finalizada = 5,

    /// <summary>O veículo foi entregue ao cliente. Estado terminal de sucesso.</summary>
    Entregue = 6,

    /// <summary>A OS foi encerrada sem execução — orçamento reprovado ou desistência. Estado terminal.</summary>
    Cancelada = 7
}

/// <summary>Situação do orçamento dentro da Ordem de Serviço.</summary>
public enum StatusOrcamento
{
    /// <summary>Gerado, mas ainda não enviado ao cliente.</summary>
    EmElaboracao = 1,

    /// <summary>Enviado ao cliente, aguardando decisão.</summary>
    AguardandoAprovacao = 2,

    /// <summary>Aprovado pelo cliente. Autoriza a execução e o consumo de peças.</summary>
    Aprovado = 3,

    /// <summary>Reprovado pelo cliente. Libera as reservas de peças e cancela a OS.</summary>
    Reprovado = 4,

    /// <summary>Expirou sem decisão do cliente.</summary>
    Expirado = 5
}

/// <summary>Métodos de apoio à máquina de estados da Ordem de Serviço.</summary>
public static class StatusOrdemServicoExtensions
{
    /// <summary>Estados a partir dos quais nenhuma transição é mais possível.</summary>
    public static bool EhTerminal(this StatusOrdemServico status) =>
        status is StatusOrdemServico.Entregue or StatusOrdemServico.Cancelada;

    /// <summary>
    /// Estados em que a composição de serviços e peças ainda pode ser alterada. Depois do
    /// envio do orçamento, mexer nos itens mudaria o valor já apresentado ao cliente.
    /// </summary>
    public static bool PermiteAlterarItens(this StatusOrdemServico status) =>
        status is StatusOrdemServico.Recebida or StatusOrdemServico.EmDiagnostico;

    /// <summary>Estados em que a OS ainda pode ser cancelada (nenhuma peça foi consumida).</summary>
    public static bool PermiteCancelamento(this StatusOrdemServico status) =>
        status is StatusOrdemServico.Recebida
            or StatusOrdemServico.EmDiagnostico
            or StatusOrdemServico.AguardandoAprovacao;

    /// <summary>Nome legível do status para exibição ao cliente.</summary>
    public static string Descricao(this StatusOrdemServico status) => status switch
    {
        StatusOrdemServico.Recebida => "Recebida",
        StatusOrdemServico.EmDiagnostico => "Em diagnóstico",
        StatusOrdemServico.AguardandoAprovacao => "Aguardando aprovação",
        StatusOrdemServico.EmExecucao => "Em execução",
        StatusOrdemServico.Finalizada => "Finalizada",
        StatusOrdemServico.Entregue => "Entregue",
        StatusOrdemServico.Cancelada => "Cancelada",
        _ => status.ToString()
    };
}
