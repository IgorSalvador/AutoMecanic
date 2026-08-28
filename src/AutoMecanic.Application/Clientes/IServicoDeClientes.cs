using AutoMecanic.Application.Clientes.Dtos;
using AutoMecanic.Application.Common;

namespace AutoMecanic.Application.Clientes;

/// <summary>
/// Casos de uso do contexto Clientes. A camada de aplicação orquestra: carrega agregados,
/// invoca comportamento de domínio e delega a persistência à Unidade de Trabalho — sem
/// conter regra de negócio própria.
/// </summary>
public interface IServicoDeClientes
{
    /// <summary>Cadastra um cliente, rejeitando CPF/CNPJ já existente.</summary>
    Task<ClienteResponse> CadastrarAsync(CriarClienteRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Atualiza os dados cadastrais de um cliente ativo.</summary>
    Task<ClienteResponse> AtualizarAsync(Guid id, AtualizarClienteRequest requisicao, CancellationToken cancellationToken = default);

    Task<ClienteResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca pela chave natural usada na recepção da oficina.</summary>
    Task<ClienteResponse> ObterPorDocumentoAsync(string documento, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<ClienteResumoResponse>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inativa o cliente. Não há exclusão física: o histórico de Ordens de Serviço precisa
    /// permanecer íntegro e auditável.
    /// </summary>
    Task InativarAsync(Guid id, string? motivo, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}
