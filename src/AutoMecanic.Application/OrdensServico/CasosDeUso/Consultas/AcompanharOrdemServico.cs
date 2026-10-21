using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.OrdensServico.ValueObjects;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Consulta pública do cliente, sem autenticação. O número da OS e o CPF/CNPJ funcionam juntos
/// como prova de posse: nenhum dos dois, sozinho, revela dados de terceiros.
/// </summary>
public sealed class AcompanharOrdemServico(
    IRepositorioDeOrdensServico repositorio,
    IRepositorioDeClientes repositorioDeClientes,
    IRepositorioDeVeiculos repositorioDeVeiculos)
{
    public async Task<AcompanhamentoResponse> ExecutarAsync(
        string numero,
        string documentoCliente,
        CancellationToken cancellationToken = default)
    {
        var numeroValidado = NumeroOrdemServico.Analisar(numero);

        if (!Documento.TentarCriar(documentoCliente, out var documento))
        {
            throw new ValidacaoException(nameof(documentoCliente), "CPF ou CNPJ informado é inválido.");
        }

        var ordem = await repositorio.ObterPorNumeroAsync(numeroValidado.Valor, cancellationToken);

        var cliente = ordem is null
            ? null
            : await repositorioDeClientes.ObterPorIdAsync(ordem.ClienteId, cancellationToken);

        // Resposta deliberadamente idêntica para "OS inexistente" e "documento não confere":
        // a diferença permitiria descobrir quais números de OS existem.
        if (ordem is null || cliente is null || cliente.Documento != documento)
        {
            throw new RecursoNaoEncontradoException(
                "Nenhuma Ordem de Serviço foi encontrada para o número e documento informados.");
        }

        var veiculo = await repositorioDeVeiculos.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return AcompanhamentoResponse.De(ordem, veiculo?.Descricao ?? "Veículo não localizado");
    }
}
