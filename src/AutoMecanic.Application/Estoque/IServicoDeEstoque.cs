using AutoMecanic.Application.Common;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Domain.Estoque;

namespace AutoMecanic.Application.Estoque;

/// <summary>
/// Casos de uso da gestão de peças e insumos, incluindo o controle de saldo exigido pelo
/// requisito ("CRUD de peças e insumos, com controle de estoque").
/// </summary>
public interface IServicoDeEstoque
{
    Task<PecaResponse> CadastrarAsync(CriarPecaRequest requisicao, CancellationToken cancellationToken = default);

    Task<PecaResponse> AtualizarAsync(Guid id, AtualizarPecaRequest requisicao, CancellationToken cancellationToken = default);

    Task<PecaResponse> ReajustarPrecoAsync(Guid id, decimal novoPreco, CancellationToken cancellationToken = default);

    Task<PecaResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PecaResponse> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<PecaResponse>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivas,
        bool? apenasAbaixoDoMinimo,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    /// <summary>Recebimento de mercadoria: aumenta o saldo e gera lançamento no razão.</summary>
    Task<PecaResponse> RegistrarEntradaAsync(Guid id, RegistrarEntradaRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Baixa por perda, avaria ou vencimento.</summary>
    Task<PecaResponse> RegistrarPerdaAsync(Guid id, RegistrarPerdaRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Acerta o saldo para o valor apurado em contagem física.</summary>
    Task<PecaResponse> AjustarSaldoAsync(Guid id, AjustarEstoqueRequest requisicao, CancellationToken cancellationToken = default);

    /// <summary>Peças no ponto de ressuprimento ou abaixo dele, com sugestão de compra.</summary>
    Task<IReadOnlyList<AlertaDeEstoqueResponse>> ListarAlertasDeEstoqueAsync(CancellationToken cancellationToken = default);

    /// <summary>Extrato do razão de estoque, filtrável por peça, OS, tipo e período.</summary>
    Task<ResultadoPaginado<MovimentoEstoqueResponse>> ListarMovimentosAsync(
        Guid? pecaId,
        Guid? ordemServicoId,
        TipoMovimentoEstoque? tipo,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    Task InativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}
