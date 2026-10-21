using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Domain.Veiculos.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Recepção do veículo no balcão: identifica o cliente pelo CPF/CNPJ, localiza o veículo pela
/// placa — cadastrando o que faltar —, abre a Ordem de Serviço e já inclui os serviços e as
/// peças combinados, reservando as peças no estoque. Tudo em uma transação: faltando saldo de
/// uma única peça, nada é gravado, nem o número da OS.
/// </summary>
public sealed class ReceberVeiculo(
    IRepositorioDeClientes repositorioDeClientes,
    IRepositorioDeVeiculos repositorioDeVeiculos,
    AberturaDeOrdemServico abertura,
    ComposicaoDaOrdemServico composicao,
    IUnitOfWork unitOfWork,
    ILogger<ReceberVeiculo> logger)
{
    public async Task<OrdemServicoResponse> ExecutarAsync(
        ReceberVeiculoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            // Documento e placa são validados antes de qualquer consulta ou cadastro.
            var documento = Documento.Criar(requisicao.DocumentoCliente);
            var placa = Placa.Criar(requisicao.Placa);

            var cliente = await ObterOuCadastrarClienteAsync(requisicao, documento, ct);
            var veiculo = await ObterOuCadastrarVeiculoAsync(requisicao, placa, cliente, ct);

            var ordem = await abertura.AbrirAsync(cliente, veiculo, requisicao.DescricaoProblema, requisicao.QuilometragemEntrada, ct);

            await composicao.IncluirServicosAsync(ordem, requisicao.Servicos ?? [], ct);
            await composicao.IncluirPecasAsync(ordem, requisicao.Pecas ?? [], ct);

            await unitOfWork.SalvarAlteracoesAsync(ct);

            return OrdemServicoResponse.De(ordem, cliente.Nome, cliente.Documento.Formatado, veiculo.Descricao);
        }, cancellationToken);
    }

    private async Task<Cliente> ObterOuCadastrarClienteAsync(
        ReceberVeiculoRequest requisicao,
        Documento documento,
        CancellationToken cancellationToken)
    {
        var cliente = await repositorioDeClientes.ObterPorDocumentoAsync(documento, cancellationToken);

        if (cliente is not null)
        {
            cliente.GarantirClienteAtivo();
            return cliente;
        }

        // Cliente novo: o cadastro exige os dados de contato para que o orçamento possa ser
        // enviado depois. Sem eles, a recepção não pode prosseguir.
        cliente = Cliente.Cadastrar(
            requisicao.NomeCliente,
            requisicao.DocumentoCliente,
            requisicao.EmailCliente,
            requisicao.TelefoneCliente);

        await repositorioDeClientes.AdicionarAsync(cliente, cancellationToken);

        logger.LogInformation("Cliente {ClienteId} cadastrado durante a recepção do veículo.", cliente.Id);

        return cliente;
    }

    private async Task<Veiculo> ObterOuCadastrarVeiculoAsync(
        ReceberVeiculoRequest requisicao,
        Placa placa,
        Cliente cliente,
        CancellationToken cancellationToken)
    {
        var veiculo = await repositorioDeVeiculos.ObterPorPlacaAsync(placa, cancellationToken);

        if (veiculo is null)
        {
            if (requisicao.AnoFabricacao is null)
            {
                throw new ValidacaoException(
                    nameof(requisicao.AnoFabricacao),
                    "O veículo ainda não está cadastrado: informe marca, modelo e ano de fabricação.");
            }

            veiculo = Veiculo.Cadastrar(
                cliente.Id,
                requisicao.Placa,
                requisicao.Marca,
                requisicao.Modelo,
                requisicao.AnoFabricacao.Value,
                requisicao.AnoModelo,
                requisicao.Cor,
                requisicao.QuilometragemEntrada ?? 0);

            await repositorioDeVeiculos.AdicionarAsync(veiculo, cancellationToken);

            logger.LogInformation("Veículo {Placa} cadastrado durante a recepção.", placa.Valor);

            return veiculo;
        }

        veiculo.GarantirVeiculoAtivo();

        // O veículo pode ter trocado de dono desde o último atendimento.
        if (veiculo.ClienteId != cliente.Id)
        {
            veiculo.TransferirPara(cliente.Id);
        }

        if (requisicao.QuilometragemEntrada is int km && km > veiculo.Quilometragem)
        {
            veiculo.RegistrarQuilometragem(km);
        }

        repositorioDeVeiculos.Atualizar(veiculo);

        return veiculo;
    }
}
