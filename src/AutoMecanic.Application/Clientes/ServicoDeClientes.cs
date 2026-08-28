using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Clientes.Dtos;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.Clientes;

/// <inheritdoc cref="IServicoDeClientes"/>
public sealed class ServicoDeClientes(
    IRepositorioDeClientes repositorio,
    IUnitOfWork unitOfWork,
    ILogger<ServicoDeClientes> logger) : IServicoDeClientes
{
    public async Task<ClienteResponse> CadastrarAsync(
        CriarClienteRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // O objeto de valor valida o documento; aqui só resta checar a unicidade,
        // que é uma regra de conjunto e não cabe dentro do agregado.
        var documento = Documento.Criar(requisicao.Documento);

        if (await repositorio.ExisteComDocumentoAsync(documento, cancellationToken: cancellationToken))
        {
            throw new ConflitoException(
                "DOCUMENTO_DUPLICADO",
                $"Já existe um cliente cadastrado com o documento {documento.Formatado}.");
        }

        var cliente = Cliente.Cadastrar(
            requisicao.Nome,
            requisicao.Documento,
            requisicao.Email,
            requisicao.Telefone,
            MapearEndereco(requisicao.Endereco));

        await repositorio.AdicionarAsync(cliente, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Cliente {ClienteId} cadastrado.", cliente.Id);

        return ClienteResponse.De(cliente);
    }

    public async Task<ClienteResponse> AtualizarAsync(
        Guid id,
        AtualizarClienteRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var cliente = await ExigirClienteAsync(id, cancellationToken);

        cliente.AtualizarCadastro(
            requisicao.Nome,
            requisicao.Email,
            requisicao.Telefone,
            MapearEndereco(requisicao.Endereco));

        repositorio.Atualizar(cliente);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Cliente {ClienteId} atualizado.", cliente.Id);

        return ClienteResponse.De(cliente);
    }

    public async Task<ClienteResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ClienteResponse.De(await ExigirClienteAsync(id, cancellationToken));

    public async Task<ClienteResponse> ObterPorDocumentoAsync(string documento, CancellationToken cancellationToken = default)
    {
        if (!Documento.TentarCriar(documento, out var documentoValido))
        {
            throw new ValidacaoException(nameof(documento), "CPF ou CNPJ informado é inválido.");
        }

        var cliente = await repositorio.ObterPorDocumentoAsync(documentoValido!, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", documentoValido!.Formatado);

        return ClienteResponse.De(cliente);
    }

    public async Task<ResultadoPaginado<ClienteResumoResponse>> ListarAsync(
        string? termoDeBusca,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorio.ListarAsync(termoDeBusca, apenasAtivos, paginacao, cancellationToken);

        return ResultadoPaginado<ClienteResumoResponse>.Criar(
            [.. pagina.Itens.Select(ClienteResumoResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }

    public async Task InativarAsync(Guid id, string? motivo, CancellationToken cancellationToken = default)
    {
        var cliente = await ExigirClienteAsync(id, cancellationToken);

        cliente.Inativar(motivo);

        repositorio.Atualizar(cliente);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Cliente {ClienteId} inativado. Motivo: {Motivo}", cliente.Id, motivo ?? "não informado");
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cliente = await ExigirClienteAsync(id, cancellationToken);

        cliente.Reativar();

        repositorio.Atualizar(cliente);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    private async Task<Cliente> ExigirClienteAsync(Guid id, CancellationToken cancellationToken) =>
        await repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", id);

    private static Endereco? MapearEndereco(EnderecoDto? dto) =>
        dto is null
            ? null
            : Endereco.Criar(dto.Logradouro, dto.Numero, dto.Complemento, dto.Bairro, dto.Cidade, dto.Uf, dto.Cep);
}
