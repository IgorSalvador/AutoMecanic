using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Clientes.ValueObjects;

namespace AutoMecanic.Application.Clientes.Dtos;

/// <summary>Endereço do cliente. Todos os campos são obrigatórios quando o bloco é informado.</summary>
/// <param name="Logradouro">Nome da rua, avenida ou estrada.</param>
/// <param name="Numero">Número do imóvel.</param>
/// <param name="Complemento">Apartamento, bloco, sala. Opcional.</param>
/// <param name="Bairro">Bairro.</param>
/// <param name="Cidade">Município.</param>
/// <param name="Uf">Sigla da unidade federativa (SP, RJ…).</param>
/// <param name="Cep">CEP, com ou sem máscara.</param>
public sealed record EnderecoDto(
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Uf,
    string Cep);

/// <summary>Dados para cadastrar um novo cliente.</summary>
/// <param name="Nome">Nome civil ou razão social.</param>
/// <param name="Documento">CPF (11 dígitos) ou CNPJ (14 caracteres), com ou sem máscara.</param>
/// <param name="Email">E-mail para envio do orçamento.</param>
/// <param name="Telefone">Telefone com DDD.</param>
/// <param name="Endereco">Endereço completo. Opcional.</param>
public sealed record CriarClienteRequest(
    string Nome,
    string Documento,
    string Email,
    string Telefone,
    EnderecoDto? Endereco = null);

/// <summary>
/// Dados para atualizar um cliente. O documento não aparece aqui: trocá-lo significaria
/// outra pessoa, e não a mesma sob outro número.
/// </summary>
/// <param name="Nome">Nome civil ou razão social.</param>
/// <param name="Email">E-mail para envio do orçamento.</param>
/// <param name="Telefone">Telefone com DDD.</param>
/// <param name="Endereco">Endereço completo. Opcional.</param>
public sealed record AtualizarClienteRequest(
    string Nome,
    string Email,
    string Telefone,
    EnderecoDto? Endereco = null);

/// <summary>Representação completa do cliente devolvida pela API.</summary>
public sealed record ClienteResponse
{
    public required Guid Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Documento normalizado, sem máscara.</summary>
    public required string Documento { get; init; }

    /// <summary>Documento com máscara, pronto para exibição.</summary>
    public required string DocumentoFormatado { get; init; }

    public required TipoPessoa TipoPessoa { get; init; }

    public required string Email { get; init; }

    public required string Telefone { get; init; }

    public required string TelefoneFormatado { get; init; }

    public EnderecoDto? Endereco { get; init; }

    public required bool Ativo { get; init; }

    public required DateTimeOffset CadastradoEm { get; init; }

    public DateTimeOffset? AtualizadoEm { get; init; }

    /// <summary>Projeta o agregado para o DTO de saída.</summary>
    public static ClienteResponse De(Cliente cliente) => new()
    {
        Id = cliente.Id,
        Nome = cliente.Nome,
        Documento = cliente.Documento.Numero,
        DocumentoFormatado = cliente.Documento.Formatado,
        TipoPessoa = cliente.TipoPessoa,
        Email = cliente.Email.Endereco,
        Telefone = cliente.Telefone.Numero,
        TelefoneFormatado = cliente.Telefone.Formatado,
        Endereco = cliente.Endereco is null
            ? null
            : new EnderecoDto(
                cliente.Endereco.Logradouro,
                cliente.Endereco.Numero,
                cliente.Endereco.Complemento,
                cliente.Endereco.Bairro,
                cliente.Endereco.Cidade,
                cliente.Endereco.Uf,
                cliente.Endereco.Cep),
        Ativo = cliente.Ativo,
        CadastradoEm = cliente.CadastradoEm,
        AtualizadoEm = cliente.AtualizadoEm
    };
}

/// <summary>Projeção enxuta usada em listagens e em referências dentro da Ordem de Serviço.</summary>
public sealed record ClienteResumoResponse(
    Guid Id,
    string Nome,
    string DocumentoFormatado,
    string Email,
    string TelefoneFormatado,
    bool Ativo)
{
    public static ClienteResumoResponse De(Cliente cliente) => new(
        cliente.Id,
        cliente.Nome,
        cliente.Documento.Formatado,
        cliente.Email.Endereco,
        cliente.Telefone.Formatado,
        cliente.Ativo);
}
