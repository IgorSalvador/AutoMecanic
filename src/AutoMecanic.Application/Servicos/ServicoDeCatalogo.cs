using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Domain.Servicos;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.Servicos;

/// <inheritdoc cref="IServicoDeCatalogo"/>
public sealed class ServicoDeCatalogo(
    IRepositorioDeServicos repositorio,
    IUnitOfWork unitOfWork,
    ILogger<ServicoDeCatalogo> logger) : IServicoDeCatalogo
{
    public async Task<ServicoResponse> CadastrarAsync(
        CriarServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        if (await repositorio.ExisteComNomeAsync(requisicao.Nome, cancellationToken: cancellationToken))
        {
            throw new ConflitoException(
                "SERVICO_DUPLICADO",
                $"Já existe um serviço cadastrado com o nome '{requisicao.Nome}'.");
        }

        var servico = Servico.Cadastrar(
            requisicao.Nome,
            requisicao.Descricao,
            requisicao.Categoria,
            requisicao.Preco,
            requisicao.TempoEstimadoEmMinutos);

        await repositorio.AdicionarAsync(servico, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Serviço {ServicoId} '{Nome}' cadastrado.", servico.Id, servico.Nome);

        return ServicoResponse.De(servico);
    }

    public async Task<ServicoResponse> AtualizarAsync(
        Guid id,
        AtualizarServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var servico = await ExigirServicoAsync(id, cancellationToken);

        if (await repositorio.ExisteComNomeAsync(requisicao.Nome, id, cancellationToken))
        {
            throw new ConflitoException(
                "SERVICO_DUPLICADO",
                $"Já existe outro serviço cadastrado com o nome '{requisicao.Nome}'.");
        }

        servico.AtualizarDados(
            requisicao.Nome,
            requisicao.Descricao,
            requisicao.Categoria,
            requisicao.TempoEstimadoEmMinutos);

        repositorio.Atualizar(servico);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return ServicoResponse.De(servico);
    }

    public async Task<ServicoResponse> ReajustarPrecoAsync(
        Guid id,
        ReajustarPrecoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var servico = await ExigirServicoAsync(id, cancellationToken);
        var precoAnterior = servico.Preco.Valor;

        servico.ReajustarPreco(requisicao.NovoPreco);

        repositorio.Atualizar(servico);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Preço do serviço {ServicoId} reajustado de {Anterior} para {Novo}.",
            servico.Id, precoAnterior, servico.Preco.Valor);

        return ServicoResponse.De(servico);
    }

    public async Task<ServicoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ServicoResponse.De(await ExigirServicoAsync(id, cancellationToken));

    public async Task<ResultadoPaginado<ServicoResponse>> ListarAsync(
        string? termoDeBusca,
        CategoriaServico? categoria,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorio.ListarAsync(termoDeBusca, categoria, apenasAtivos, paginacao, cancellationToken);

        return ResultadoPaginado<ServicoResponse>.Criar(
            [.. pagina.Itens.Select(ServicoResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }

    public async Task InativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var servico = await ExigirServicoAsync(id, cancellationToken);

        servico.Inativar();

        repositorio.Atualizar(servico);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var servico = await ExigirServicoAsync(id, cancellationToken);

        servico.Reativar();

        repositorio.Atualizar(servico);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    private async Task<Servico> ExigirServicoAsync(Guid id, CancellationToken cancellationToken) =>
        await repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Serviço", id);
}
