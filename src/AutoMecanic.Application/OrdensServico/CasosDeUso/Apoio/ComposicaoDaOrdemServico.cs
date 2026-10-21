using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.Servicos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;

/// <summary>
/// Inclusão de serviços e peças na Ordem de Serviço, comum à composição item a item e à
/// abertura completa na recepção.
/// <para>
/// Para serviços, copia preço e tempo do catálogo, que ficam congelados no item. Para peças,
/// reserva o saldo <b>antes</b> de alterar a OS: faltando estoque, a operação falha sem ter
/// mexido em nenhum dos dois agregados.
/// </para>
/// </summary>
public sealed class ComposicaoDaOrdemServico(
    IRepositorioDeServicos repositorioDeServicos,
    IRepositorioDePecas repositorioDePecas)
{
    /// <summary>Inclui um serviço do catálogo já carregado.</summary>
    public static void IncluirServico(OrdemServico ordem, Servico servico, int quantidade)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(servico);

        servico.GarantirServicoAtivo();

        ordem.AdicionarServico(
            servico.Id,
            servico.Nome,
            servico.Preco.Valor,
            quantidade,
            servico.TempoEstimadoEmMinutos);
    }

    /// <summary>Reserva a peça já carregada e a inclui na OS.</summary>
    public void IncluirPeca(OrdemServico ordem, Peca peca, int quantidade)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(peca);

        peca.GarantirPecaAtiva();
        peca.Reservar(quantidade, ordem.Id);

        var item = ordem.AdicionarPeca(
            peca.Id,
            peca.Codigo,
            peca.Nome,
            peca.PrecoUnitario.Valor,
            quantidade);

        ordem.ConfirmarReservaDePeca(item.Id);

        repositorioDePecas.Atualizar(peca);
    }

    /// <summary>Carrega em uma consulta os serviços pedidos e os inclui na OS.</summary>
    public async Task IncluirServicosAsync(
        OrdemServico ordem,
        IReadOnlyCollection<AdicionarServicoRequest> itens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itens);

        if (itens.Count == 0)
        {
            return;
        }

        var servicos = (await repositorioDeServicos.ObterPorIdsAsync([.. itens.Select(i => i.ServicoId)], cancellationToken))
            .ToDictionary(servico => servico.Id);

        foreach (var item in itens)
        {
            var servico = servicos.GetValueOrDefault(item.ServicoId)
                ?? throw new RecursoNaoEncontradoException("Serviço", item.ServicoId);

            IncluirServico(ordem, servico, item.Quantidade);
        }
    }

    /// <summary>Carrega em uma consulta as peças pedidas, reserva cada uma e as inclui na OS.</summary>
    public async Task IncluirPecasAsync(
        OrdemServico ordem,
        IReadOnlyCollection<AdicionarPecaRequest> itens,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itens);

        if (itens.Count == 0)
        {
            return;
        }

        var pecas = (await repositorioDePecas.ObterPorIdsAsync([.. itens.Select(i => i.PecaId)], cancellationToken))
            .ToDictionary(peca => peca.Id);

        foreach (var item in itens)
        {
            var peca = pecas.GetValueOrDefault(item.PecaId)
                ?? throw new RecursoNaoEncontradoException("Peça", item.PecaId);

            IncluirPeca(ordem, peca, item.Quantidade);
        }
    }
}
