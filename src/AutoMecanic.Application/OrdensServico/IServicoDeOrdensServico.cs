using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.OrdensServico;

namespace AutoMecanic.Application.OrdensServico;

/// <summary>
/// Casos de uso da Ordem de Serviço. Cada método corresponde a um <b>comando</b> do Event
/// Storming e resulta em uma transição de status ou em uma alteração de composição da OS.
/// </summary>
public interface IServicoDeOrdensServico
{
    /// <summary>Abre a OS para um cliente e veículo já cadastrados.</summary>
    Task<OrdemServicoResponse> AbrirAsync(AbrirOrdemServicoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fluxo de balcão: identifica o cliente pelo CPF/CNPJ, localiza ou cadastra o veículo
    /// pela placa e abre a OS — tudo em uma única transação.
    /// </summary>
    Task<OrdemServicoResponse> ReceberVeiculoAsync(ReceberVeiculoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Move a OS para <c>Em diagnóstico</c>.</summary>
    Task<OrdemServicoResponse> IniciarDiagnosticoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registra o laudo técnico do mecânico.</summary>
    Task<OrdemServicoResponse> RegistrarDiagnosticoAsync(Guid id, RegistrarDiagnosticoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Inclui um serviço do catálogo, copiando preço e tempo estimado vigentes.</summary>
    Task<OrdemServicoResponse> AdicionarServicoAsync(Guid id, AdicionarServicoRequest requisicao, CancellationToken cancellationToken = default);

    Task<OrdemServicoResponse> AlterarQuantidadeDeServicoAsync(Guid id, Guid itemId, AlterarQuantidadeRequest requisicao, CancellationToken cancellationToken = default);

    Task<OrdemServicoResponse> RemoverServicoAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Inclui uma peça e reserva a quantidade no estoque na mesma transação.</summary>
    Task<OrdemServicoResponse> AdicionarPecaAsync(Guid id, AdicionarPecaRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Remove a peça da OS e devolve a quantidade reservada ao estoque.</summary>
    Task<OrdemServicoResponse> RemoverPecaAsync(Guid id, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Calcula o orçamento a partir dos itens da OS.</summary>
    Task<OrdemServicoResponse> GerarOrcamentoAsync(Guid id, GerarOrcamentoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Envia o orçamento ao cliente e move a OS para <c>Aguardando aprovação</c>.</summary>
    Task<OrdemServicoResponse> EnviarOrcamentoAsync(Guid id, EnviarOrcamentoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Aprova o orçamento, inicia a execução e consome as peças reservadas.</summary>
    Task<OrdemServicoResponse> AprovarOrcamentoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Reprova o orçamento, cancela a OS e devolve as peças reservadas ao estoque.</summary>
    Task<OrdemServicoResponse> ReprovarOrcamentoAsync(Guid id, ReprovarOrcamentoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Devolve a OS ao diagnóstico para revisão do escopo.</summary>
    Task<OrdemServicoResponse> RetornarParaDiagnosticoAsync(Guid id, string? motivo, CancellationToken cancellationToken = default);

    /// <summary>Conclui os serviços e move a OS para <c>Finalizada</c>.</summary>
    Task<OrdemServicoResponse> FinalizarServicoAsync(Guid id, FinalizarServicoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Registra a entrega do veículo ao cliente.</summary>
    Task<OrdemServicoResponse> EntregarVeiculoAsync(Guid id, EntregarVeiculoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Cancela a OS antes da execução e libera as reservas de peça.</summary>
    Task<OrdemServicoResponse> CancelarAsync(Guid id, CancelarOrdemServicoRequest requisicao, CancellationToken cancellationToken = default);

    Task<OrdemServicoResponse> AtribuirResponsavelAsync(Guid id, AtribuirResponsavelRequest requisicao, CancellationToken cancellationToken = default);

    Task<OrdemServicoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OrdemServicoResponse> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<OrdemServicoResumoResponse>> ListarAsync(
        StatusOrdemServico? status,
        Guid? clienteId,
        Guid? veiculoId,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta pública de acompanhamento. Exige número da OS <b>e</b> documento do cliente:
    /// os dois juntos funcionam como prova de posse, evitando que qualquer pessoa enumere
    /// números de OS e leia dados de terceiros.
    /// </summary>
    Task<AcompanhamentoResponse> AcompanharAsync(string numero, string documentoCliente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expira orçamentos vencidos e cancela as respectivas OS, liberando as reservas.
    /// Executado por rotina de manutenção.
    /// </summary>
    Task<int> ExpirarOrcamentosVencidosAsync(CancellationToken cancellationToken = default);
}
