using AutoMecanic.Application.Common;
using AutoMecanic.Application.Veiculos.Dtos;

namespace AutoMecanic.Application.Veiculos;

/// <summary>Casos de uso do cadastro de veículos.</summary>
public interface IServicoDeVeiculos
{
    /// <summary>Cadastra um veículo para um cliente ativo, rejeitando placa duplicada.</summary>
    Task<VeiculoResponse> CadastrarAsync(CriarVeiculoRequest requisicao, CancellationToken cancellationToken = default);

    Task<VeiculoResponse> AtualizarAsync(Guid id, AtualizarVeiculoRequest requisicao, CancellationToken cancellationToken = default);

    Task<VeiculoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca pela placa — o caminho natural na recepção da oficina.</summary>
    Task<VeiculoResponse> ObterPorPlacaAsync(string placa, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VeiculoResumoResponse>> ListarPorClienteAsync(Guid clienteId, CancellationToken cancellationToken = default);

    Task<ResultadoPaginado<VeiculoResumoResponse>> ListarAsync(
        string? termoDeBusca,
        Guid? clienteId,
        bool? apenasAtivos,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default);

    /// <summary>Registra nova leitura do odômetro, tipicamente na recepção do veículo.</summary>
    Task<VeiculoResponse> RegistrarQuilometragemAsync(
        Guid id,
        RegistrarQuilometragemRequest requisicao,
        CancellationToken cancellationToken = default);

    /// <summary>Transfere a titularidade para outro cliente da base.</summary>
    Task<VeiculoResponse> TransferirAsync(Guid id, TransferirVeiculoRequest requisicao, CancellationToken cancellationToken = default);

    Task InativarAsync(Guid id, string? motivo, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}
