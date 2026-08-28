using AutoMecanic.Domain.Servicos;

namespace AutoMecanic.Application.Servicos.Dtos;

/// <summary>Dados para incluir um serviço no catálogo da oficina.</summary>
/// <param name="Nome">Nome do serviço (ex.: "Troca de óleo do motor").</param>
/// <param name="Descricao">Detalhamento do que está incluído. Opcional.</param>
/// <param name="Categoria">Agrupamento do serviço.</param>
/// <param name="Preco">Preço de tabela, maior que zero.</param>
/// <param name="TempoEstimadoEmMinutos">Tempo padrão de execução, base do prazo prometido.</param>
public sealed record CriarServicoRequest(
    string Nome,
    string? Descricao,
    CategoriaServico Categoria,
    decimal Preco,
    int TempoEstimadoEmMinutos);

/// <summary>
/// Dados atualizáveis do serviço. O preço fica fora deste contrato de propósito: o reajuste
/// tem endpoint próprio para que a alteração de valor seja sempre auditável.
/// </summary>
/// <param name="Nome">Nome do serviço.</param>
/// <param name="Descricao">Detalhamento do que está incluído. Opcional.</param>
/// <param name="Categoria">Agrupamento do serviço.</param>
/// <param name="TempoEstimadoEmMinutos">Tempo padrão de execução.</param>
public sealed record AtualizarServicoRequest(
    string Nome,
    string? Descricao,
    CategoriaServico Categoria,
    int TempoEstimadoEmMinutos);

/// <summary>Reajuste do preço de tabela.</summary>
/// <param name="NovoPreco">Novo preço, maior que zero.</param>
public sealed record ReajustarPrecoRequest(decimal NovoPreco);

/// <summary>Representação do serviço do catálogo devolvida pela API.</summary>
public sealed record ServicoResponse
{
    public required Guid Id { get; init; }

    public required string Nome { get; init; }

    public string? Descricao { get; init; }

    public required CategoriaServico Categoria { get; init; }

    public required decimal Preco { get; init; }

    public required int TempoEstimadoEmMinutos { get; init; }

    public required bool Ativo { get; init; }

    public required DateTimeOffset CadastradoEm { get; init; }

    public DateTimeOffset? AtualizadoEm { get; init; }

    public static ServicoResponse De(Servico servico) => new()
    {
        Id = servico.Id,
        Nome = servico.Nome,
        Descricao = servico.Descricao,
        Categoria = servico.Categoria,
        Preco = servico.Preco.Valor,
        TempoEstimadoEmMinutos = servico.TempoEstimadoEmMinutos,
        Ativo = servico.Ativo,
        CadastradoEm = servico.CadastradoEm,
        AtualizadoEm = servico.AtualizadoEm
    };
}
