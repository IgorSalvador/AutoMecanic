using AutoMecanic.Application.Estoque.Dtos;
using AutoMecanic.Application.Identidade.Dtos;
using AutoMecanic.Application.OrdensServico.Dtos;
using AutoMecanic.Application.Servicos.Dtos;
using AutoMecanic.Domain.Clientes.ValueObjects;
using AutoMecanic.Domain.Veiculos.ValueObjects;
using FluentValidation;

namespace AutoMecanic.Application.Validacao;

/// <summary>Validação do cadastro de serviço no catálogo.</summary>
public sealed class ValidadorDeCriarServico : AbstractValidator<CriarServicoRequest>
{
    public ValidadorDeCriarServico()
    {
        RuleFor(s => s.Nome).NotEmpty().Length(3, 120);
        RuleFor(s => s.Descricao).MaximumLength(500);
        RuleFor(s => s.Categoria).IsInEnum();
        RuleFor(s => s.Preco).GreaterThan(0).WithMessage("O preço do serviço deve ser maior que zero.");
        RuleFor(s => s.TempoEstimadoEmMinutos)
            .InclusiveBetween(1, 43_200)
            .WithMessage("O tempo estimado deve estar entre 1 minuto e 30 dias.");
    }
}

/// <summary>Validação da atualização de serviço.</summary>
public sealed class ValidadorDeAtualizarServico : AbstractValidator<AtualizarServicoRequest>
{
    public ValidadorDeAtualizarServico()
    {
        RuleFor(s => s.Nome).NotEmpty().Length(3, 120);
        RuleFor(s => s.Descricao).MaximumLength(500);
        RuleFor(s => s.Categoria).IsInEnum();
        RuleFor(s => s.TempoEstimadoEmMinutos).InclusiveBetween(1, 43_200);
    }
}

/// <summary>Validação do reajuste de preço.</summary>
public sealed class ValidadorDeReajustarPreco : AbstractValidator<ReajustarPrecoRequest>
{
    public ValidadorDeReajustarPreco() =>
        RuleFor(s => s.NovoPreco).GreaterThan(0).LessThanOrEqualTo(9_999_999.99m);
}

/// <summary>Validação do cadastro de peça ou insumo.</summary>
public sealed class ValidadorDeCriarPeca : AbstractValidator<CriarPecaRequest>
{
    public ValidadorDeCriarPeca()
    {
        RuleFor(p => p.Codigo).NotEmpty().MaximumLength(40);
        RuleFor(p => p.Nome).NotEmpty().Length(2, 150);
        RuleFor(p => p.Descricao).MaximumLength(500);
        RuleFor(p => p.UnidadeMedida).IsInEnum();
        RuleFor(p => p.PrecoUnitario).GreaterThan(0);
        RuleFor(p => p.QuantidadeInicial).InclusiveBetween(0, 1_000_000);
        RuleFor(p => p.EstoqueMinimo).InclusiveBetween(0, 1_000_000);
    }
}

/// <summary>Validação da atualização de peça.</summary>
public sealed class ValidadorDeAtualizarPeca : AbstractValidator<AtualizarPecaRequest>
{
    public ValidadorDeAtualizarPeca()
    {
        RuleFor(p => p.Nome).NotEmpty().Length(2, 150);
        RuleFor(p => p.Descricao).MaximumLength(500);
        RuleFor(p => p.UnidadeMedida).IsInEnum();
        RuleFor(p => p.EstoqueMinimo).InclusiveBetween(0, 1_000_000);
    }
}

/// <summary>Validação da entrada de mercadoria.</summary>
public sealed class ValidadorDeRegistrarEntrada : AbstractValidator<RegistrarEntradaRequest>
{
    public ValidadorDeRegistrarEntrada()
    {
        RuleFor(e => e.Quantidade).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(e => e.Motivo).NotEmpty().MaximumLength(300).WithMessage("Informe o motivo da entrada (nota fiscal, fornecedor).");
    }
}

/// <summary>Validação da baixa por perda.</summary>
public sealed class ValidadorDeRegistrarPerda : AbstractValidator<RegistrarPerdaRequest>
{
    public ValidadorDeRegistrarPerda()
    {
        RuleFor(e => e.Quantidade).GreaterThan(0);
        RuleFor(e => e.Motivo).NotEmpty().MaximumLength(300);
    }
}

/// <summary>Validação do ajuste de inventário.</summary>
public sealed class ValidadorDeAjustarEstoque : AbstractValidator<AjustarEstoqueRequest>
{
    public ValidadorDeAjustarEstoque()
    {
        RuleFor(e => e.QuantidadeApurada).InclusiveBetween(0, 1_000_000);
        RuleFor(e => e.Motivo).NotEmpty().MaximumLength(300);
    }
}

/// <summary>Validação da abertura de Ordem de Serviço.</summary>
public sealed class ValidadorDeAbrirOrdemServico : AbstractValidator<AbrirOrdemServicoRequest>
{
    public ValidadorDeAbrirOrdemServico()
    {
        RuleFor(o => o.ClienteId).NotEmpty();
        RuleFor(o => o.VeiculoId).NotEmpty();
        RuleFor(o => o.DescricaoProblema)
            .NotEmpty().WithMessage("Registre o relato do cliente sobre o problema.")
            .MaximumLength(2000);
        RuleFor(o => o.QuilometragemEntrada)
            .InclusiveBetween(0, 3_000_000)
            .When(o => o.QuilometragemEntrada is not null);
    }
}

/// <summary>Validação do fluxo de recepção do veículo no balcão.</summary>
public sealed class ValidadorDeReceberVeiculo : AbstractValidator<ReceberVeiculoRequest>
{
    public ValidadorDeReceberVeiculo()
    {
        RuleFor(o => o.DocumentoCliente)
            .NotEmpty().WithMessage("Informe o CPF ou CNPJ do cliente.")
            .Must(documento => Documento.TentarCriar(documento, out _))
            .WithMessage("CPF ou CNPJ inválido: verifique os dígitos verificadores.");

        RuleFor(o => o.Placa)
            .NotEmpty().WithMessage("Informe a placa do veículo.")
            .Must(placa => Placa.TentarCriar(placa, out _))
            .WithMessage("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");

        RuleFor(o => o.DescricaoProblema).NotEmpty().MaximumLength(2000);

        RuleFor(o => o.AnoFabricacao)
            .InclusiveBetween(1900, DateTimeOffset.UtcNow.Year + 1)
            .When(o => o.AnoFabricacao is not null);

        RuleFor(o => o.QuilometragemEntrada)
            .InclusiveBetween(0, 3_000_000)
            .When(o => o.QuilometragemEntrada is not null);
    }
}

/// <summary>Validação do laudo técnico.</summary>
public sealed class ValidadorDeRegistrarDiagnostico : AbstractValidator<RegistrarDiagnosticoRequest>
{
    public ValidadorDeRegistrarDiagnostico() =>
        RuleFor(d => d.Diagnostico).NotEmpty().MaximumLength(4000);
}

/// <summary>Validação da inclusão de serviço na OS.</summary>
public sealed class ValidadorDeAdicionarServico : AbstractValidator<AdicionarServicoRequest>
{
    public ValidadorDeAdicionarServico()
    {
        RuleFor(i => i.ServicoId).NotEmpty();
        RuleFor(i => i.Quantidade).InclusiveBetween(1, 999);
    }
}

/// <summary>Validação da inclusão de peça na OS.</summary>
public sealed class ValidadorDeAdicionarPeca : AbstractValidator<AdicionarPecaRequest>
{
    public ValidadorDeAdicionarPeca()
    {
        RuleFor(i => i.PecaId).NotEmpty();
        RuleFor(i => i.Quantidade).InclusiveBetween(1, 9_999);
    }
}

/// <summary>Validação da alteração de quantidade de um item.</summary>
public sealed class ValidadorDeAlterarQuantidade : AbstractValidator<AlterarQuantidadeRequest>
{
    public ValidadorDeAlterarQuantidade() =>
        RuleFor(i => i.Quantidade).InclusiveBetween(1, 9_999);
}

/// <summary>Validação da geração de orçamento.</summary>
public sealed class ValidadorDeGerarOrcamento : AbstractValidator<GerarOrcamentoRequest>
{
    public ValidadorDeGerarOrcamento() =>
        RuleFor(o => o.PercentualDesconto)
            .InclusiveBetween(0m, 100m)
            .WithMessage("O desconto deve estar entre 0% e 100%.");
}

/// <summary>Validação do envio do orçamento.</summary>
public sealed class ValidadorDeEnviarOrcamento : AbstractValidator<EnviarOrcamentoRequest>
{
    public ValidadorDeEnviarOrcamento() =>
        RuleFor(o => o.ValidadeEmDias).InclusiveBetween(1, 90);
}

/// <summary>Validação do cancelamento de OS.</summary>
public sealed class ValidadorDeCancelarOrdemServico : AbstractValidator<CancelarOrdemServicoRequest>
{
    public ValidadorDeCancelarOrdemServico() =>
        RuleFor(o => o.Motivo)
            .NotEmpty().WithMessage("Informe o motivo do cancelamento.")
            .MaximumLength(500);
}

/// <summary>Validação das credenciais de login.</summary>
public sealed class ValidadorDeLogin : AbstractValidator<LoginRequest>
{
    public ValidadorDeLogin()
    {
        RuleFor(l => l.Email).NotEmpty().MaximumLength(254);
        RuleFor(l => l.Senha).NotEmpty().MaximumLength(128);
    }
}

/// <summary>Validação da criação de usuário administrativo.</summary>
public sealed class ValidadorDeCriarUsuario : AbstractValidator<CriarUsuarioRequest>
{
    public ValidadorDeCriarUsuario()
    {
        RuleFor(u => u.Nome).NotEmpty().Length(3, 150);
        RuleFor(u => u.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(u => u.Perfil).IsInEnum();
        RuleFor(u => u.Senha).NotEmpty().Must(SenhaAtendeAPolitica).WithMessage(MensagemDePolitica);
    }

    internal static bool SenhaAtendeAPolitica(string? senha) =>
        !string.IsNullOrWhiteSpace(senha)
        && senha.Length is >= 8 and <= 128
        && senha.Any(char.IsUpper)
        && senha.Any(char.IsLower)
        && senha.Any(char.IsDigit)
        && senha.Any(c => !char.IsLetterOrDigit(c));

    internal const string MensagemDePolitica =
        "A senha deve ter de 8 a 128 caracteres e conter maiúscula, minúscula, dígito e caractere especial.";
}

/// <summary>Validação da troca de senha pelo próprio usuário.</summary>
public sealed class ValidadorDeAlterarSenha : AbstractValidator<AlterarSenhaRequest>
{
    public ValidadorDeAlterarSenha()
    {
        RuleFor(s => s.SenhaAtual).NotEmpty();
        RuleFor(s => s.NovaSenha)
            .NotEmpty()
            .Must(ValidadorDeCriarUsuario.SenhaAtendeAPolitica)
            .WithMessage(ValidadorDeCriarUsuario.MensagemDePolitica);
    }
}

/// <summary>Validação da redefinição administrativa de senha.</summary>
public sealed class ValidadorDeRedefinirSenha : AbstractValidator<RedefinirSenhaRequest>
{
    public ValidadorDeRedefinirSenha() =>
        RuleFor(s => s.NovaSenha)
            .NotEmpty()
            .Must(ValidadorDeCriarUsuario.SenhaAtendeAPolitica)
            .WithMessage(ValidadorDeCriarUsuario.MensagemDePolitica);
}
