using AutoMecanic.Application.Clientes.Dtos;
using AutoMecanic.Application.Veiculos.Dtos;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Veiculos.ValueObjects;
using FluentValidation;

namespace AutoMecanic.Application.Validacao;

/// <summary>
/// Validações de <b>formato de requisição</b>. São a primeira barreira, respondida com
/// <c>400 Bad Request</c> e a lista completa de campos com problema.
/// <para>
/// Não substituem as invariantes do domínio — elas continuam sendo verificadas dentro dos
/// agregados. A duplicação é intencional: a validação aqui melhora a mensagem devolvida ao
/// cliente da API; a do domínio garante que nenhum caminho de código crie estado inválido.
/// </para>
/// </summary>
public sealed class ValidadorDeEndereco : AbstractValidator<EnderecoDto>
{
    public ValidadorDeEndereco()
    {
        RuleFor(e => e.Logradouro).NotEmpty().MaximumLength(200);
        RuleFor(e => e.Numero).NotEmpty().MaximumLength(20);
        RuleFor(e => e.Complemento).MaximumLength(100);
        RuleFor(e => e.Bairro).NotEmpty().MaximumLength(100);
        RuleFor(e => e.Cidade).NotEmpty().MaximumLength(100);
        RuleFor(e => e.Uf).NotEmpty().Length(2).WithMessage("Informe a sigla da UF com 2 letras.");
        RuleFor(e => e.Cep)
            .NotEmpty()
            .Must(cep => cep is not null && cep.Count(char.IsDigit) == 8)
            .WithMessage("O CEP deve conter 8 dígitos.");
    }
}

/// <summary>Validação do cadastro de cliente.</summary>
public sealed class ValidadorDeCriarCliente : AbstractValidator<CriarClienteRequest>
{
    public ValidadorDeCriarCliente()
    {
        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Informe o nome do cliente.")
            .Length(3, 150);

        RuleFor(c => c.Documento)
            .NotEmpty().WithMessage("Informe o CPF ou CNPJ do cliente.")
            .Must(documento => Documento.TentarCriar(documento, out _))
            .WithMessage("CPF ou CNPJ inválido: verifique os dígitos verificadores.");

        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Informe o e-mail do cliente.")
            .MaximumLength(254);

        RuleFor(c => c.Telefone)
            .NotEmpty().WithMessage("Informe o telefone do cliente.");

        RuleFor(c => c.Endereco!)
            .SetValidator(new ValidadorDeEndereco())
            .When(c => c.Endereco is not null);
    }
}

/// <summary>Validação da atualização de cliente.</summary>
public sealed class ValidadorDeAtualizarCliente : AbstractValidator<AtualizarClienteRequest>
{
    public ValidadorDeAtualizarCliente()
    {
        RuleFor(c => c.Nome).NotEmpty().Length(3, 150);
        RuleFor(c => c.Email).NotEmpty().MaximumLength(254);
        RuleFor(c => c.Telefone).NotEmpty();

        RuleFor(c => c.Endereco!)
            .SetValidator(new ValidadorDeEndereco())
            .When(c => c.Endereco is not null);
    }
}

/// <summary>Validação do cadastro de veículo.</summary>
public sealed class ValidadorDeCriarVeiculo : AbstractValidator<CriarVeiculoRequest>
{
    public ValidadorDeCriarVeiculo()
    {
        RuleFor(v => v.ClienteId).NotEmpty().WithMessage("Informe o cliente proprietário do veículo.");

        RuleFor(v => v.Placa)
            .NotEmpty().WithMessage("Informe a placa do veículo.")
            .Must(placa => Placa.TentarCriar(placa, out _))
            .WithMessage("Placa inválida. Formatos aceitos: ABC1234 (brasileiro) ou ABC1D23 (Mercosul).");

        RuleFor(v => v.Marca).NotEmpty().MaximumLength(50);
        RuleFor(v => v.Modelo).NotEmpty().MaximumLength(80);

        RuleFor(v => v.AnoFabricacao)
            .InclusiveBetween(1900, DateTimeOffset.UtcNow.Year + 1)
            .WithMessage($"O ano de fabricação deve estar entre 1900 e {DateTimeOffset.UtcNow.Year + 1}.");

        RuleFor(v => v.Cor).MaximumLength(30);

        RuleFor(v => v.Quilometragem)
            .InclusiveBetween(0, 3_000_000)
            .WithMessage("A quilometragem informada é implausível.");
    }
}

/// <summary>Validação da atualização de veículo.</summary>
public sealed class ValidadorDeAtualizarVeiculo : AbstractValidator<AtualizarVeiculoRequest>
{
    public ValidadorDeAtualizarVeiculo()
    {
        RuleFor(v => v.Marca).NotEmpty().MaximumLength(50);
        RuleFor(v => v.Modelo).NotEmpty().MaximumLength(80);
        RuleFor(v => v.AnoFabricacao).InclusiveBetween(1900, DateTimeOffset.UtcNow.Year + 1);
        RuleFor(v => v.AnoModelo).InclusiveBetween(1900, DateTimeOffset.UtcNow.Year + 2);
        RuleFor(v => v.Cor).MaximumLength(30);
    }
}

/// <summary>Validação do registro de quilometragem.</summary>
public sealed class ValidadorDeRegistrarQuilometragem : AbstractValidator<RegistrarQuilometragemRequest>
{
    public ValidadorDeRegistrarQuilometragem() =>
        RuleFor(v => v.Quilometragem).InclusiveBetween(0, 3_000_000);
}
