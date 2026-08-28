using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.OrdensServico.ValueObjects;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Domain.Veiculos.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AutoMecanic.Application.OrdensServico;

/// <summary>
/// <inheritdoc cref="IServicoDeOrdensServico"/>
/// <para>
/// Este serviço é o principal <b>orquestrador entre agregados</b> do sistema: a Ordem de
/// Serviço decide o que pode acontecer, e o Estoque decide se há peça para isso. Quando as
/// duas decisões precisam valer juntas — incluir peça reservando saldo, aprovar orçamento
/// consumindo peça — a operação roda dentro de uma transação explícita.
/// </para>
/// </summary>
public sealed class ServicoDeOrdensServico(
    IRepositorioDeOrdensServico repositorio,
    IRepositorioDeClientes repositorioDeClientes,
    IRepositorioDeVeiculos repositorioDeVeiculos,
    IRepositorioDeServicos repositorioDeServicos,
    IRepositorioDePecas repositorioDePecas,
    IGeradorDeNumeroDeOrdemServico geradorDeNumero,
    IUsuarioAtual usuarioAtual,
    IProvedorDeDataHora relogio,
    IUnitOfWork unitOfWork,
    ILogger<ServicoDeOrdensServico> logger) : IServicoDeOrdensServico
{
    // -----------------------------------------------------------------
    // Abertura
    // -----------------------------------------------------------------

    public async Task<OrdemServicoResponse> AbrirAsync(
        AbrirOrdemServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var cliente = await repositorioDeClientes.ObterPorIdAsync(requisicao.ClienteId, ct)
                ?? throw new RecursoNaoEncontradoException("Cliente", requisicao.ClienteId);

            var veiculo = await repositorioDeVeiculos.ObterPorIdAsync(requisicao.VeiculoId, ct)
                ?? throw new RecursoNaoEncontradoException("Veículo", requisicao.VeiculoId);

            var ordem = await AbrirInternoAsync(cliente, veiculo, requisicao.DescricaoProblema, requisicao.QuilometragemEntrada, ct);

            await unitOfWork.SalvarAlteracoesAsync(ct);

            return OrdemServicoResponse.De(ordem, cliente.Nome, cliente.Documento.Formatado, veiculo.Descricao);
        }, cancellationToken);
    }

    public async Task<OrdemServicoResponse> ReceberVeiculoAsync(
        ReceberVeiculoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var documento = Documento.Criar(requisicao.DocumentoCliente);
            var placa = Placa.Criar(requisicao.Placa);

            var cliente = await repositorioDeClientes.ObterPorDocumentoAsync(documento, ct);

            if (cliente is null)
            {
                // Cliente novo: o cadastro exige os dados de contato para que o orçamento
                // possa ser enviado depois. Sem eles, a recepção não pode prosseguir.
                cliente = Cliente.Cadastrar(
                    requisicao.NomeCliente,
                    requisicao.DocumentoCliente,
                    requisicao.EmailCliente,
                    requisicao.TelefoneCliente);

                await repositorioDeClientes.AdicionarAsync(cliente, ct);

                logger.LogInformation("Cliente {ClienteId} cadastrado durante a recepção do veículo.", cliente.Id);
            }
            else
            {
                cliente.GarantirClienteAtivo();
            }

            var veiculo = await repositorioDeVeiculos.ObterPorPlacaAsync(placa, ct);

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

                await repositorioDeVeiculos.AdicionarAsync(veiculo, ct);

                logger.LogInformation("Veículo {Placa} cadastrado durante a recepção.", placa.Valor);
            }
            else
            {
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
            }

            var ordem = await AbrirInternoAsync(cliente, veiculo, requisicao.DescricaoProblema, requisicao.QuilometragemEntrada, ct);

            await unitOfWork.SalvarAlteracoesAsync(ct);

            return OrdemServicoResponse.De(ordem, cliente.Nome, cliente.Documento.Formatado, veiculo.Descricao);
        }, cancellationToken);
    }

    private async Task<OrdemServico> AbrirInternoAsync(
        Cliente cliente,
        Veiculo veiculo,
        string descricaoProblema,
        int? quilometragemEntrada,
        CancellationToken cancellationToken)
    {
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

    // -----------------------------------------------------------------
    // Diagnóstico
    // -----------------------------------------------------------------

    public async Task<OrdemServicoResponse> IniciarDiagnosticoAsync(Guid id, CancellationToken cancellationToken = default) =>
        await AplicarEPersistirAsync(id, ordem => ordem.IniciarDiagnostico(usuarioAtual.Id), cancellationToken);

    public async Task<OrdemServicoResponse> RegistrarDiagnosticoAsync(
        Guid id,
        RegistrarDiagnosticoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await AplicarEPersistirAsync(id, ordem => ordem.RegistrarDiagnostico(requisicao.Diagnostico), cancellationToken);
    }

    // -----------------------------------------------------------------
    // Composição de itens
    // -----------------------------------------------------------------

    public async Task<OrdemServicoResponse> AdicionarServicoAsync(
        Guid id,
        AdicionarServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        var ordem = await ExigirOrdemAsync(id, cancellationToken);

        var servico = await repositorioDeServicos.ObterPorIdAsync(requisicao.ServicoId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Serviço", requisicao.ServicoId);

        servico.GarantirServicoAtivo();

        // Preço e tempo são copiados agora e congelados no item: um reajuste posterior
        // no catálogo não altera esta OS.
        ordem.AdicionarServico(
            servico.Id,
            servico.Nome,
            servico.Preco.Valor,
            requisicao.Quantidade,
            servico.TempoEstimadoEmMinutos);

        repositorio.Atualizar(ordem);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return await ProjetarAsync(ordem, cancellationToken);
    }

    public async Task<OrdemServicoResponse> AlterarQuantidadeDeServicoAsync(
        Guid id,
        Guid itemId,
        AlterarQuantidadeRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await AplicarEPersistirAsync(
            id,
            ordem => ordem.AlterarQuantidadeDeServico(itemId, requisicao.Quantidade),
            cancellationToken);
    }

    public async Task<OrdemServicoResponse> RemoverServicoAsync(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken = default) =>
        await AplicarEPersistirAsync(id, ordem => ordem.RemoverServico(itemId), cancellationToken);

    public async Task<OrdemServicoResponse> AdicionarPecaAsync(
        Guid id,
        AdicionarPecaRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await ExigirOrdemAsync(id, ct);

            var peca = await repositorioDePecas.ObterPorIdAsync(requisicao.PecaId, ct)
                ?? throw new RecursoNaoEncontradoException("Peça", requisicao.PecaId);

            peca.GarantirPecaAtiva();

            // A ordem importa: reservar primeiro faz a operação inteira falhar por falta de
            // saldo antes de qualquer alteração na OS, mantendo os dois agregados coerentes.
            peca.Reservar(requisicao.Quantidade, ordem.Id);

            var item = ordem.AdicionarPeca(
                peca.Id,
                peca.Codigo,
                peca.Nome,
                peca.PrecoUnitario.Valor,
                requisicao.Quantidade);

            ordem.ConfirmarReservaDePeca(item.Id);

            repositorioDePecas.Atualizar(peca);
            repositorio.Atualizar(ordem);

            await unitOfWork.SalvarAlteracoesAsync(ct);

            logger.LogInformation("Peça {Codigo} x{Quantidade} reservada para a OS {Numero}.",
                peca.Codigo, requisicao.Quantidade, ordem.Numero.Valor);

            return await ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }

    public async Task<OrdemServicoResponse> RemoverPecaAsync(
        Guid id,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await ExigirOrdemAsync(id, ct);

            var item = ordem.ItensPeca.FirstOrDefault(i => i.Id == itemId)
                ?? throw new RecursoNaoEncontradoException("Item de peça", itemId);

            if (item.Reservada)
            {
                var peca = await repositorioDePecas.ObterPorIdAsync(item.PecaId, ct)
                    ?? throw new RecursoNaoEncontradoException("Peça", item.PecaId);

                peca.LiberarReserva(item.Quantidade, ordem.Id);
                repositorioDePecas.Atualizar(peca);
            }

            ordem.RemoverPeca(itemId);

            repositorio.Atualizar(ordem);
            await unitOfWork.SalvarAlteracoesAsync(ct);

            return await ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }

    // -----------------------------------------------------------------
    // Orçamento
    // -----------------------------------------------------------------

    public async Task<OrdemServicoResponse> GerarOrcamentoAsync(
        Guid id,
        GerarOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await AplicarEPersistirAsync(
            id,
            ordem => ordem.GerarOrcamento(requisicao.PercentualDesconto),
            cancellationToken);
    }

    public async Task<OrdemServicoResponse> EnviarOrcamentoAsync(
        Guid id,
        EnviarOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await AplicarEPersistirAsync(
            id,
            ordem => ordem.EnviarOrcamentoParaAprovacao(requisicao.ValidadeEmDias),
            cancellationToken);
    }

    public async Task<OrdemServicoResponse> AprovarOrcamentoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await ExigirOrdemAsync(id, ct);

            // A OS passa a EmExecucao aqui — é essa transição que autoriza a baixa das peças.
            ordem.AprovarOrcamento();

            await ConsumirPecasReservadasAsync(ordem, ct);

            repositorio.Atualizar(ordem);
            await unitOfWork.SalvarAlteracoesAsync(ct);

            logger.LogInformation("Orçamento da OS {Numero} aprovado. Execução iniciada.", ordem.Numero.Valor);

            return await ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }

    public async Task<OrdemServicoResponse> ReprovarOrcamentoAsync(
        Guid id,
        ReprovarOrcamentoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await ExigirOrdemAsync(id, ct);

            ordem.ReprovarOrcamento(requisicao.Motivo);

            await LiberarPecasReservadasAsync(ordem, ct);

            repositorio.Atualizar(ordem);
            await unitOfWork.SalvarAlteracoesAsync(ct);

            logger.LogInformation("Orçamento da OS {Numero} reprovado pelo cliente.", ordem.Numero.Valor);

            return await ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }

    public async Task<OrdemServicoResponse> RetornarParaDiagnosticoAsync(
        Guid id,
        string? motivo,
        CancellationToken cancellationToken = default) =>
        await AplicarEPersistirAsync(id, ordem => ordem.RetornarParaDiagnostico(motivo), cancellationToken);

    // -----------------------------------------------------------------
    // Execução e entrega
    // -----------------------------------------------------------------

    public async Task<OrdemServicoResponse> FinalizarServicoAsync(
        Guid id,
        FinalizarServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await AplicarEPersistirAsync(
            id,
            ordem => ordem.FinalizarServico(requisicao.Observacao, usuarioAtual.Id),
            cancellationToken);
    }

    public async Task<OrdemServicoResponse> EntregarVeiculoAsync(
        Guid id,
        EntregarVeiculoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await AplicarEPersistirAsync(
            id,
            ordem => ordem.EntregarVeiculo(requisicao.Observacao, usuarioAtual.Id),
            cancellationToken);
    }

    public async Task<OrdemServicoResponse> CancelarAsync(
        Guid id,
        CancelarOrdemServicoRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var ordem = await ExigirOrdemAsync(id, ct);

            ordem.Cancelar(requisicao.Motivo, usuarioAtual.Id);

            await LiberarPecasReservadasAsync(ordem, ct);

            repositorio.Atualizar(ordem);
            await unitOfWork.SalvarAlteracoesAsync(ct);

            return await ProjetarAsync(ordem, ct);
        }, cancellationToken);
    }

    public async Task<OrdemServicoResponse> AtribuirResponsavelAsync(
        Guid id,
        AtribuirResponsavelRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        return await AplicarEPersistirAsync(
            id,
            ordem => ordem.AtribuirResponsavel(requisicao.ResponsavelId),
            cancellationToken);
    }

    // -----------------------------------------------------------------
    // Consultas
    // -----------------------------------------------------------------

    public async Task<OrdemServicoResponse> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ProjetarAsync(await ExigirOrdemAsync(id, cancellationToken), cancellationToken);

    public async Task<OrdemServicoResponse> ObterPorNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        var ordem = await repositorio.ObterPorNumeroAsync(NumeroOrdemServico.Analisar(numero).Valor, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ordem de Serviço", numero);

        return await ProjetarAsync(ordem, cancellationToken);
    }

    public async Task<ResultadoPaginado<OrdemServicoResumoResponse>> ListarAsync(
        StatusOrdemServico? status,
        Guid? clienteId,
        Guid? veiculoId,
        DateTimeOffset? de,
        DateTimeOffset? ate,
        ParametrosDePaginacao paginacao,
        CancellationToken cancellationToken = default)
    {
        var pagina = await repositorio.ListarAsync(status, clienteId, veiculoId, de, ate, paginacao, cancellationToken);

        return ResultadoPaginado<OrdemServicoResumoResponse>.Criar(
            [.. pagina.Itens.Select(OrdemServicoResumoResponse.De)],
            pagina.TotalDeItens,
            paginacao);
    }

    public async Task<AcompanhamentoResponse> AcompanharAsync(
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

    public async Task<int> ExpirarOrcamentosVencidosAsync(CancellationToken cancellationToken = default)
    {
        var agora = relogio.Agora;
        var candidatas = await repositorio.ListarComOrcamentoVencidoAsync(agora, cancellationToken);

        if (candidatas.Count == 0)
        {
            return 0;
        }

        return await unitOfWork.ExecutarEmTransacaoAsync(async ct =>
        {
            var expiradas = 0;

            foreach (var ordem in candidatas)
            {
                var statusAnterior = ordem.Status;

                ordem.ExpirarOrcamento(agora);

                if (ordem.Status == statusAnterior)
                {
                    continue;
                }

                await LiberarPecasReservadasAsync(ordem, ct);

                repositorio.Atualizar(ordem);
                expiradas++;
            }

            if (expiradas > 0)
            {
                await unitOfWork.SalvarAlteracoesAsync(ct);
                logger.LogInformation("{Total} orçamento(s) expirado(s) por decurso de prazo.", expiradas);
            }

            return expiradas;
        }, cancellationToken);
    }

    // -----------------------------------------------------------------
    // Apoio
    // -----------------------------------------------------------------

    /// <summary>Baixa do estoque todas as peças reservadas para a OS.</summary>
    private async Task ConsumirPecasReservadasAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        var reservados = ordem.ItensPeca.Where(item => item.Reservada).ToList();

        if (reservados.Count == 0)
        {
            return;
        }

        var pecas = (await repositorioDePecas.ObterPorIdsAsync([.. reservados.Select(i => i.PecaId)], cancellationToken))
            .ToDictionary(p => p.Id);

        foreach (var item in reservados)
        {
            if (!pecas.TryGetValue(item.PecaId, out var peca))
            {
                throw new RecursoNaoEncontradoException("Peça", item.PecaId);
            }

            peca.ConsumirReserva(item.Quantidade, ordem.Id);
            ordem.ConfirmarConsumoDePeca(item.Id);

            repositorioDePecas.Atualizar(peca);
        }
    }

    /// <summary>Devolve ao estoque todas as reservas ainda pendentes da OS.</summary>
    private async Task LiberarPecasReservadasAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        var reservados = ordem.ItensPeca.Where(item => item.Reservada).ToList();

        if (reservados.Count == 0)
        {
            return;
        }

        var pecas = (await repositorioDePecas.ObterPorIdsAsync([.. reservados.Select(i => i.PecaId)], cancellationToken))
            .ToDictionary(p => p.Id);

        foreach (var item in reservados)
        {
            if (!pecas.TryGetValue(item.PecaId, out var peca))
            {
                continue;
            }

            peca.LiberarReserva(item.Quantidade, ordem.Id);
            ordem.ConfirmarLiberacaoDeReservaDePeca(item.Id);

            repositorioDePecas.Atualizar(peca);
        }
    }

    /// <summary>
    /// Carrega a OS, aplica um comportamento de domínio e persiste. Concentra o padrão comum
    /// aos casos de uso que tocam apenas o agregado Ordem de Serviço.
    /// </summary>
    private async Task<OrdemServicoResponse> AplicarEPersistirAsync(
        Guid id,
        Action<OrdemServico> acao,
        CancellationToken cancellationToken)
    {
        var ordem = await ExigirOrdemAsync(id, cancellationToken);

        acao(ordem);

        repositorio.Atualizar(ordem);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken);

        return await ProjetarAsync(ordem, cancellationToken);
    }

    private async Task<OrdemServico> ExigirOrdemAsync(Guid id, CancellationToken cancellationToken) =>
        await repositorio.ObterCompletaPorIdAsync(id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ordem de Serviço", id);

    /// <summary>Enriquece a resposta com nome do cliente e descrição do veículo.</summary>
    private async Task<OrdemServicoResponse> ProjetarAsync(OrdemServico ordem, CancellationToken cancellationToken)
    {
        var cliente = await repositorioDeClientes.ObterPorIdAsync(ordem.ClienteId, cancellationToken);
        var veiculo = await repositorioDeVeiculos.ObterPorIdAsync(ordem.VeiculoId, cancellationToken);

        return OrdemServicoResponse.De(
            ordem,
            cliente?.Nome,
            cliente?.Documento.Formatado,
            veiculo?.Descricao);
    }
}
