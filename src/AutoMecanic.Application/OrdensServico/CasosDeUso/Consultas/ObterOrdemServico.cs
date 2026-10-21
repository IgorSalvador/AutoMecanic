using AutoMecanic.Application.Abstractions;
using AutoMecanic.Application.Common;
using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Domain.OrdensServico.ValueObjects;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>Detalhamento de uma Ordem de Serviço, pela identidade ou pelo número legível.</summary>
public sealed class ObterOrdemServico(
    OperacaoSobreOrdemServico operacao,
    IRepositorioDeOrdensServico repositorio)
{
    public async Task<OrdemServicoResponse> PorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await operacao.ProjetarAsync(await operacao.ExigirAsync(id, cancellationToken), cancellationToken);

    public async Task<OrdemServicoResponse> PorNumeroAsync(string numero, CancellationToken cancellationToken = default)
    {
        var ordem = await repositorio.ObterPorNumeroAsync(NumeroOrdemServico.Analisar(numero).Valor, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Ordem de Serviço", numero);

        return await operacao.ProjetarAsync(ordem, cancellationToken);
    }
}
