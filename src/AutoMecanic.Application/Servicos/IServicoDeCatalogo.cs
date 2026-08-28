using AutoMecanic.Application.Common;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Domain.Servicos;

namespace AutoMecanic.Application.Servicos;

/// <summary>Casos de uso do catálogo de serviços prestados pela oficina.</summary>
public interface IServicoDeCatalogo
{
    Task<ServicoResponse> CadastrarAsync(CriarServicoRequest requisicao, CancellationToken cancellationToken = default);

    Task<ServicoResponse> AtualizarAsync(Guid id, AtualizarServicoRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Reajusta o preço de tabela, registrando o valor anterior para auditoria.</summary>
    Task<ServicoResponse> ReajustarPrecoAsync(Guid id, ReajustarPrecoRequest requisicao, CancellationToken cancellationToken = default);

    Task<ServicoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<ServicoResponse>> ListarAsync(
        string? termoDeBusca,
        CategoriaServico? categoria,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    Task InativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}
