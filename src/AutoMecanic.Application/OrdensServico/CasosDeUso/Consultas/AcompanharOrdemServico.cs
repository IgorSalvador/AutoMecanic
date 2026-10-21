using AutoMecanic.Application.OrdensServico.CasosDeUso.Apoio;
using AutoMecanic.Application.OrdensServico.Dtos;

namespace AutoMecanic.Application.OrdensServico.CasosDeUso;

/// <summary>
/// Consulta pública do cliente, sem autenticação. O número da OS e o CPF/CNPJ funcionam juntos
/// como prova de posse: nenhum dos dois, sozinho, revela dados de terceiros.
/// </summary>
public sealed class AcompanharOrdemServico(AcessoDoTitular acesso)
{
    public async Task<AcompanhamentoResponse> ExecutarAsync(
        string numero,
        string documentoCliente,
        CancellationToken cancellationToken = default)
    {
        var ordem = await acesso.LocalizarAsync(numero, documentoCliente, cancellationToken);

        return await acesso.ProjetarAsync(ordem, cancellationToken);
    }
}
