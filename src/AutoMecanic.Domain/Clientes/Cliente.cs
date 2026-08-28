using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Clientes.Events;
using AutoMecanic.Domain.Clientes.ValueObjects;

namespace AutoMecanic.Domain.Clientes;

/// <summary>
/// <b>Raiz de Agregado</b> do contexto Clientes &amp; Veículos.
/// <para>
/// Representa a pessoa (física ou jurídica) atendida pela oficina. É identificada
/// externamente pelo CPF/CNPJ — a chave natural usada na abertura da Ordem de Serviço —
/// e internamente por um <see cref="Entity.Id"/> imutável.
/// </para>
/// <para><b>Invariantes:</b> nome não vazio; documento válido e único; um cliente inativo
/// não pode receber novas Ordens de Serviço.</para>
/// </summary>
public sealed class Cliente : AggregateRoot
{
    private const int ComprimentoMinimoNome = 3;
    private const int ComprimentoMaximoNome = 150;

    private Cliente()
    {
        // Exigido pelo EF Core. Os campos são preenchidos por materialização.
        Nome = null!;
        Documento = null!;
        Email = null!;
        Telefone = null!;
    }

    private Cliente(Guid id, string nome, Documento documento, Email email, Telefone telefone, Endereco? endereco)
        : base(id)
    {
        Nome = nome;
        Documento = documento;
        Email = email;
        Telefone = telefone;
        Endereco = endereco;
        Ativo = true;
        CadastradoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Nome civil da pessoa física ou razão social da pessoa jurídica.</summary>
    public string Nome { get; private set; }

    public Documento Documento { get; private set; }

    public Email Email { get; private set; }

    public Telefone Telefone { get; private set; }

    public Endereco? Endereco { get; private set; }

    /// <summary>Clientes são inativados, nunca excluídos: o histórico de OS precisa ser preservado.</summary>
    public bool Ativo { get; private set; }

    public DateTimeOffset CadastradoEm { get; private set; }

    public DateTimeOffset? AtualizadoEm { get; private set; }

    public TipoPessoa TipoPessoa => Documento.Tipo;

    /// <summary>Cadastra um novo cliente e publica <see cref="ClienteCadastrado"/>.</summary>
    public static Cliente Cadastrar(
        string? nome,
        string? documento,
        string? email,
        string? telefone,
        Endereco? endereco = null)
    {
        var nomeValidado = ValidarNome(nome);

        var cliente = new Cliente(
            NovoId(),
            nomeValidado,
            ValueObjects.Documento.Criar(documento),
            ValueObjects.Email.Criar(email),
            ValueObjects.Telefone.Criar(telefone),
            endereco);

        cliente.RegistrarEvento(new ClienteCadastrado(cliente.Id, cliente.Documento.Numero, cliente.Nome));

        return cliente;
    }

    /// <summary>
    /// Atualiza os dados cadastrais. O documento é deliberadamente imutável: trocar o
    /// CPF/CNPJ significaria que se trata de outra pessoa, e não do mesmo cliente.
    /// </summary>
    public void AtualizarCadastro(string? nome, string? email, string? telefone, Endereco? endereco)
    {
        GarantirClienteAtivo();

        Nome = ValidarNome(nome);
        Email = ValueObjects.Email.Criar(email);
        Telefone = ValueObjects.Telefone.Criar(telefone);
        Endereco = endereco;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new DadosDeContatoDoClienteAtualizados(Id, Email.Endereco, Telefone.Numero));
    }

    /// <summary>Inativa o cliente. Operação idempotente.</summary>
    public void Inativar(string? motivo = null)
    {
        if (!Ativo)
        {
            return;
        }

        Ativo = false;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new ClienteInativado(Id, motivo ?? "Não informado"));
    }

    /// <summary>Reativa o cliente. Operação idempotente.</summary>
    public void Reativar()
    {
        if (Ativo)
        {
            return;
        }

        Ativo = true;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new ClienteReativado(Id));
    }

    /// <summary>Invariante consumida pelo contexto de Ordem de Serviço na abertura da OS.</summary>
    public void GarantirClienteAtivo()
    {
        if (!Ativo)
        {
            throw new DomainException(
                "CLIENTE_INATIVO",
                $"O cliente '{Nome}' está inativo e não pode ser utilizado em novas operações.");
        }
    }

    private static string ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("NOME_OBRIGATORIO", "O nome do cliente é obrigatório.");
        }

        var limpo = nome.Trim();

        if (limpo.Length is < ComprimentoMinimoNome or > ComprimentoMaximoNome)
        {
            throw new DomainException(
                "NOME_INVALIDO",
                $"O nome do cliente deve ter entre {ComprimentoMinimoNome} e {ComprimentoMaximoNome} caracteres.");
        }

        return limpo;
    }
}
