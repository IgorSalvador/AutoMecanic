using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Domain.Veiculos.ValueObjects;

namespace AutoMecanic.Application.Veiculos.Dtos;

/// <summary>Dados para cadastrar um veículo, conforme exigido no fluxo de abertura da OS.</summary>
/// <param name="ClienteId">Proprietário do veículo.</param>
/// <param name="Placa">Placa no padrão brasileiro (ABC1234) ou Mercosul (ABC1D23).</param>
/// <param name="Marca">Fabricante do veículo.</param>
/// <param name="Modelo">Modelo do veículo.</param>
/// <param name="AnoFabricacao">Ano de fabricação.</param>
/// <param name="AnoModelo">Ano-modelo. Quando omitido, assume o ano de fabricação.</param>
/// <param name="Cor">Cor predominante. Opcional.</param>
/// <param name="Quilometragem">Leitura do odômetro no cadastro.</param>
public sealed record CriarVeiculoRequest(
    Guid ClienteId,
    string Placa,
    string Marca,
    string Modelo,
    int AnoFabricacao,
    int? AnoModelo = null,
    string? Cor = null,
    int Quilometragem = 0);

/// <summary>Dados atualizáveis do veículo. A placa é imutável por definição do domínio.</summary>
/// <param name="Marca">Fabricante do veículo.</param>
/// <param name="Modelo">Modelo do veículo.</param>
/// <param name="AnoFabricacao">Ano de fabricação.</param>
/// <param name="AnoModelo">Ano-modelo.</param>
/// <param name="Cor">Cor predominante. Opcional.</param>
public sealed record AtualizarVeiculoRequest(
    string Marca,
    string Modelo,
    int AnoFabricacao,
    int AnoModelo,
    string? Cor = null);

/// <summary>Nova leitura do odômetro. Só aceita valores iguais ou maiores que o último registrado.</summary>
/// <param name="Quilometragem">Leitura atual do odômetro, em quilômetros.</param>
public sealed record RegistrarQuilometragemRequest(int Quilometragem);

/// <summary>Transferência de titularidade do veículo entre clientes da oficina.</summary>
/// <param name="NovoClienteId">Identificador do novo proprietário.</param>
public sealed record TransferirVeiculoRequest(Guid NovoClienteId);

/// <summary>Representação completa do veículo devolvida pela API.</summary>
public sealed record VeiculoResponse
{
    public required Guid Id { get; init; }

    public required Guid ClienteId { get; init; }

    /// <summary>Nome do proprietário. Preenchido quando a consulta o resolve.</summary>
    public string? NomeCliente { get; init; }

    public required string Placa { get; init; }

    public required string PlacaFormatada { get; init; }

    public required PadraoPlaca PadraoPlaca { get; init; }

    public required string Marca { get; init; }

    public required string Modelo { get; init; }

    public required int AnoFabricacao { get; init; }

    public required int AnoModelo { get; init; }

    public string? Cor { get; init; }

    public required int Quilometragem { get; init; }

    public required bool Ativo { get; init; }

    public required DateTimeOffset CadastradoEm { get; init; }

    public DateTimeOffset? AtualizadoEm { get; init; }

    public static VeiculoResponse De(Veiculo veiculo, string? nomeCliente = null) => new()
    {
        Id = veiculo.Id,
        ClienteId = veiculo.ClienteId,
        NomeCliente = nomeCliente,
        Placa = veiculo.Placa.Valor,
        PlacaFormatada = veiculo.Placa.Formatada,
        PadraoPlaca = veiculo.Placa.Padrao,
        Marca = veiculo.Marca,
        Modelo = veiculo.Modelo,
        AnoFabricacao = veiculo.AnoFabricacao,
        AnoModelo = veiculo.AnoModelo,
        Cor = veiculo.Cor,
        Quilometragem = veiculo.Quilometragem,
        Ativo = veiculo.Ativo,
        CadastradoEm = veiculo.CadastradoEm,
        AtualizadoEm = veiculo.AtualizadoEm
    };
}

/// <summary>Projeção enxuta do veículo para listagens e para o cabeçalho da OS.</summary>
public sealed record VeiculoResumoResponse(
    Guid Id,
    Guid ClienteId,
    string PlacaFormatada,
    string Descricao,
    int Quilometragem,
    bool Ativo)
{
    public static VeiculoResumoResponse De(Veiculo veiculo) => new(
        veiculo.Id,
        veiculo.ClienteId,
        veiculo.Placa.Formatada,
        veiculo.Descricao,
        veiculo.Quilometragem,
        veiculo.Ativo);
}
