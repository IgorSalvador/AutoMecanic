using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Domain.Estoque;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.Estoque;

/// <inheritdoc cref="IServicoDeEstoque"/>
public sealed class ServicoDeEstoque(
    IRepositorioDePecas repositorio,
    IRepositorioDeMovimentosDeEstoque repositorioDeMovimentos,
    IUnitOfWork unitOfWork,
    ILogger<ServicoDeEstoque> logger) : IServicoDeEstoque
{
    public async Task<PecaResponse> CadastrarAsync(
        CriarPecaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var codigo = (requisicao.Codigo ?? string.Empty).Trim().ToUpperInvariant();

        if (await repositorio.ExisteComCodigoAsync(codigo, cancellationToken: cancellationToken))
        {
            throw new ConflitoException("CODIGO_DUPLICADO", $"Já existe uma peça cadastrada com o código '{codigo}'.");
        }

        var peca = Peca.Cadastrar(
            requisicao.Codigo,
            requisicao.Nome,
            requisicao.Descricao,
            requisicao.UnidadeMedida,
            requisicao.PrecoUnitario,
            requisicao.QuantidadeInicial,
            requisicao.EstoqueMinimo);

        await repositorio.AdicionarAsync(peca, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Peça {PecaId} ({Codigo}) cadastrada com saldo inicial {Saldo}.",
            peca.Id, peca.Codigo, peca.QuantidadeEmEstoque);

        return PecaResponse.De(peca);
    }

    public async Task<PecaResponse> AtualizarAsync(
        Guid id,
        AtualizarPecaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var peca = await ExigirPecaAsync(id, cancellationToken);

        peca.AtualizarDados(requisicao.Nome, requisicao.Descricao, requisicao.UnidadeMedida, requisicao.EstoqueMinimo);

        repositorio.Atualizar(peca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return PecaResponse.De(peca);
    }

    public async Task<PecaResponse> ReajustarPrecoAsync(
        Guid id,
        decimal novoPreco,
        CancellationToken cancellationToken = default)
    {
        var peca = await ExigirPecaAsync(id, cancellationToken);

        peca.ReajustarPreco(novoPreco);

        repositorio.Atualizar(peca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return PecaResponse.De(peca);
    }

    public async Task<PecaResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        PecaResponse.De(await ExigirPecaAsync(id, cancellationToken));

    public async Task<PecaResponse> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ValidacaoException(nameof(codigo), "Informe o código da peça.");
        }

        var peca = await repositorio.ObterPorCodigoAsync(codigo.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Peça", codigo);

        return PecaResponse.De(peca);
    }

    public async Task<ResultadoPaginado<PecaResponse>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivas,
        bool? apenasAbaixoDoMinimo,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorio.ListarAsync(termoDeBusca, apenasAtivas, apenasAbaixoDoMinimo, paginacao, cancellationToken);

        return ResultadoPaginado<PecaResponse>.Criar(
            [.. pagina.Itens.Select(PecaResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }

    public async Task<PecaResponse> RegistrarEntradaAsync(
        Guid id,
        RegistrarEntradaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var peca = await ExigirPecaAsync(id, cancellationToken);

        peca.RegistrarEntrada(requisicao.Quantidade, requisicao.Motivo);

        repositorio.Atualizar(peca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Entrada de {Quantidade} unidade(s) na peça {Codigo}. Saldo: {Saldo}.",
            requisicao.Quantidade, peca.Codigo, peca.QuantidadeEmEstoque);

        return PecaResponse.De(peca);
    }

    public async Task<PecaResponse> RegistrarPerdaAsync(
        Guid id,
        RegistrarPerdaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var peca = await ExigirPecaAsync(id, cancellationToken);

        peca.RegistrarPerda(requisicao.Quantidade, requisicao.Motivo);

        repositorio.Atualizar(peca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogWarning("Perda de {Quantidade} unidade(s) da peça {Codigo}. Motivo: {Motivo}.",
            requisicao.Quantidade, peca.Codigo, requisicao.Motivo);

        return PecaResponse.De(peca);
    }

    public async Task<PecaResponse> AjustarSaldoAsync(
        Guid id,
        AjustarEstoqueRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var peca = await ExigirPecaAsync(id, cancellationToken);

        peca.AjustarSaldo(requisicao.QuantidadeApurada, requisicao.Motivo);

        repositorio.Atualizar(peca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return PecaResponse.De(peca);
    }

    public async Task<IReadOnlyList<AlertaDeEstoqueResponse>> ListarAlertasDeEstoqueAsync(
        CancellationToken cancellationToken = default)
    {
        var pecas = await repositorio.ListarAbaixoDoEstoqueMinimoAsync(cancellationToken);

        return
        [
            .. pecas.Select(peca => new AlertaDeEstoqueResponse(
                peca.Id,
                peca.Codigo,
                peca.Nome,
                peca.QuantidadeDisponivel,
                peca.EstoqueMinimo,
                // Sugestão simples e previsível: repor até o dobro do ponto de ressuprimento,
                // criando uma folga de um ciclo de consumo acima do mínimo.
                Math.Max(0, (peca.EstoqueMinimo * 2) - peca.QuantidadeDisponivel)))
        ];
    }

    public async Task<ResultadoPaginado<MovimentoEstoqueResponse>> ListarMovimentosAsync(
        Guid? pecaId,
        Guid? ordemServicoId,
        TipoMovimentoEstoque? tipo,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorioDeMovimentos.ListarAsync(
            pecaId, ordemServicoId, tipo, de, ate, paginacao, cancellationToken);

        return ResultadoPaginado<MovimentoEstoqueResponse>.Criar(
            [.. pagina.Itens.Select(MovimentoEstoqueResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }

    public async Task InativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peca = await ExigirPecaAsync(id, cancellationToken);

        peca.Inativar();

        repositorio.Atualizar(peca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var peca = await ExigirPecaAsync(id, cancellationToken);

        peca.Reativar();

        repositorio.Atualizar(peca);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    private async Task<Peca> ExigirPecaAsync(Guid id, CancellationToken cancellationToken) =>
        await repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Peça", id);
}
