using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.Veiculos.Dtos;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Domain.Veiculos.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.Veiculos;

/// <inheritdoc cref="IServicoDeVeiculos"/>
public sealed class ServicoDeVeiculos(
    IRepositorioDeVeiculos repositorio,
    IRepositorioDeClientes repositorioDeClientes,
    IUnitOfWork unitOfWork,
    ILogger<ServicoDeVeiculos> logger) : IServicoDeVeiculos
{
    public async Task<VeiculoResponse> CadastrarAsync(
        CriarVeiculoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        // Consistência entre agregados: o cliente precisa existir e estar ativo. Como esta
        // é uma regra que cruza fronteiras de agregado, ela vive aqui, na orquestração.
        var cliente = await repositorioDeClientes.ObterPorIdAsync(requisicao.ClienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", requisicao.ClienteId);

        cliente.GarantirClienteAtivo();

        var placa = Placa.Criar(requisicao.Placa);

        if (await repositorio.ExisteComPlacaAsync(placa, cancellationToken: cancellationToken))
        {
            throw new ConflitoException(
                "PLACA_DUPLICADA",
                $"Já existe um veículo cadastrado com a placa {placa.Formatada}.");
        }

        var veiculo = Veiculo.Cadastrar(
            requisicao.ClienteId,
            requisicao.Placa,
            requisicao.Marca,
            requisicao.Modelo,
            requisicao.AnoFabricacao,
            requisicao.AnoModelo,
            requisicao.Cor,
            requisicao.Quilometragem);

        await repositorio.AdicionarAsync(veiculo, cancellationToken);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Veículo {VeiculoId} ({Placa}) cadastrado para o cliente {ClienteId}.",
            veiculo.Id, veiculo.Placa.Valor, cliente.Id);

        return VeiculoResponse.De(veiculo, cliente.Nome);
    }

    public async Task<VeiculoResponse> AtualizarAsync(
        Guid id,
        AtualizarVeiculoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var veiculo = await ExigirVeiculoAsync(id, cancellationToken);

        veiculo.AtualizarDados(
            requisicao.Marca,
            requisicao.Modelo,
            requisicao.AnoFabricacao,
            requisicao.AnoModelo,
            requisicao.Cor);

        repositorio.Atualizar(veiculo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return VeiculoResponse.De(veiculo);
    }

    public async Task<VeiculoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var veiculo = await ExigirVeiculoAsync(id, cancellationToken);
        var cliente = await repositorioDeClientes.ObterPorIdAsync(veiculo.ClienteId, cancellationToken);

        return VeiculoResponse.De(veiculo, cliente?.Nome);
    }

    public async Task<VeiculoResponse> ObterPorPlacaAsync(string placa, CancellationToken cancellationToken = default)
    {
        if (!Placa.TentarCriar(placa, out var placaValida))
        {
            throw new ValidacaoException(nameof(placa), "A placa informada é inválida.");
        }

        var veiculo = await repositorio.ObterPorPlacaAsync(placaValida!, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Veículo", placaValida!.Formatada);

        var cliente = await repositorioDeClientes.ObterPorIdAsync(veiculo.ClienteId, cancellationToken);

        return VeiculoResponse.De(veiculo, cliente?.Nome);
    }

    public async Task<IReadOnlyList<VeiculoResumoResponse>> ListarPorClienteAsync(
        Guid clienteId,
        CancellationToken cancellationToken = default)
    {
        var veiculos = await repositorio.ListarPorClienteAsync(clienteId, cancellationToken);

        return [.. veiculos.Select(VeiculoResumoResponse.De)];
    }

    public async Task<ResultadoPaginado<VeiculoResumoResponse>> ListarAsync(
        string? termoDeBusca,
        Guid? clienteId,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorio.ListarAsync(termoDeBusca, clienteId, apenasAtivos, paginacao, cancellationToken);

        return ResultadoPaginado<VeiculoResumoResponse>.Criar(
            [.. pagina.Itens.Select(VeiculoResumoResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }

    public async Task<VeiculoResponse> RegistrarQuilometragemAsync(
        Guid id,
        RegistrarQuilometragemRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var veiculo = await ExigirVeiculoAsync(id, cancellationToken);

        veiculo.RegistrarQuilometragem(requisicao.Quilometragem);

        repositorio.Atualizar(veiculo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return VeiculoResponse.De(veiculo);
    }

    public async Task<VeiculoResponse> TransferirAsync(
        Guid id,
        TransferirVeiculoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var veiculo = await ExigirVeiculoAsync(id, cancellationToken);

        var novoCliente = await repositorioDeClientes.ObterPorIdAsync(requisicao.NovoClienteId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente", requisicao.NovoClienteId);

        novoCliente.GarantirClienteAtivo();

        veiculo.TransferirPara(novoCliente.Id);

        repositorio.Atualizar(veiculo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        logger.LogInformation("Veículo {VeiculoId} transferido para o cliente {ClienteId}.", veiculo.Id, novoCliente.Id);

        return VeiculoResponse.De(veiculo, novoCliente.Nome);
    }

    public async Task InativarAsync(Guid id, string? motivo, CancellationToken cancellationToken = default)
    {
        var veiculo = await ExigirVeiculoAsync(id, cancellationToken);

        veiculo.Inativar(motivo);

        repositorio.Atualizar(veiculo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var veiculo = await ExigirVeiculoAsync(id, cancellationToken);

        veiculo.Reativar();

        repositorio.Atualizar(veiculo);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);
    }

    private async Task<Veiculo> ExigirVeiculoAsync(Guid id, CancellationToken cancellationToken) =>
        await repositorio.ObterPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Veículo", id);
}
