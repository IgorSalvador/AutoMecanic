using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.ValueObjects;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;

/// <summary>
/// Acesso do cliente à própria Ordem de Serviço, sem autenticação. O número da OS e o CPF/CNPJ
/// funcionam juntos como prova de posse: nenhum dos dois, sozinho, revela ou altera a OS de
/// outra pessoa.
/// </summary>
public sealed class AcessoDoTitular(
    IRepositorioDeOrdensServico repositorio,
    IRepositorioDeClientes repositorioDeClientes,
    IRepositorioDeVeiculos repositorioDeVeiculos)
{
    /// <summary>Mensagem única para OS inexistente e para documento que não confere.</summary>
    public const string MensagemDeNaoEncontrada =
        "Nenhuma Ordem de Serviço foi encontrada para o número e documento informados.";

    /// <summary>
    /// Localiza a OS do titular. A resposta é deliberadamente idêntica para "OS inexistente" e
    /// "documento não confere": a diferença permitiria descobrir quais números de OS existem.
    /// </summary>
    public async Task<OrdemServico> LocalizarAsync(string numero, string documentoCliente, CancellationToken cancellationToken)
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

        if (ordem is null || cliente is null || cliente.Documento != documento)
        {
            throw new RecursoNaoEncontradoException(MensagemDeNaoEncontrada);
        }

        return ordem;
    }

    /// <summary>Visão pública da OS, sem dados internos da oficina.</summary>
    public async Task<AcompanhamentoResponse> ProjetarAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        var veiculo = await repositorioDeVeiculos.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return AcompanhamentoResponse.De(ordem, veiculo?.Descricao ?? "Veículo não localizado");
    }
}
