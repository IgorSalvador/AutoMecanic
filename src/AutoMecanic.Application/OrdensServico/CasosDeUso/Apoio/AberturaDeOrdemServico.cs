using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.ValueObjects;
using AutoMecanic.Domain.Veiculos;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;

/// <summary>
/// Abre a Ordem de Serviço para um cliente e um veículo já resolvidos. Comum à abertura direta
/// (cliente e veículo informados por identidade) e à recepção (localiza ou cadastra ambos).
/// </summary>
public sealed class AberturaDeOrdemServico(
    IRepositorioDeOrdensServico repositorio,
    IGeradorDeNumeroDeOrdemServico geradorDeNumero,
    IUsuarioAtual usuarioAtual,
    IProvedorDeDataHora relogio,
    ILogger<AberturaDeOrdemServico> logger)
{
    public async Task<OrdemServico> AbrirAsync(
        Cliente cliente,
        Veiculo veiculo,
        string descricaoProblema,
        int? quilometragemEntrada,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(veiculo);

        cliente.GarantirClienteAtivo();
        veiculo.GarantirVeiculoAtivo();

        // Consistência entre agregados: a OS é sempre do dono atual do veículo.
        if (veiculo.ClienteId != cliente.Id)
        {
            throw new ConflitoException(
                "VEICULO_DE_OUTRO_CLIENTE",
                $"O veículo {veiculo.Placa.Formatada} não pertence ao cliente {cliente.Nome}.");
        }

        var ano = relogio.Agora.Year;
        var sequencial = await geradorDeNumero.ProximoSequencialAsync(ano, cancellationToken);
        var numero = NumeroOrdemServico.Gerar(ano, sequencial);

        var ordem = OrdemServico.Abrir(
            numero,
            cliente.Id,
            veiculo.Id,
            descricaoProblema,
            quilometragemEntrada,
            usuarioAtual.Id);

        await repositorio.AdicionarAsync(ordem, cancellationToken);

        logger.LogInformation("Ordem de Serviço {Numero} aberta para o veículo {Placa}.", numero.Valor, veiculo.Placa.Valor);

        return ordem;
    }
}
