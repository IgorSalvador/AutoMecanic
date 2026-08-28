using AutoMecanic.Domain.Abstractions;
using AutoMecanic.Domain.Servicos.Events;
using AutoMecanic.Domain.SharedKernel;

namespace AutoMecanic.Domain.Servicos;

/// <summary>Agrupamento do serviço no catálogo, usado para relatórios e para a busca do atendente.</summary>
public enum CategoriaServico
{
    /// <summary>Trocas e revisões periódicas (óleo, filtros, velas).</summary>
    ManutencaoPreventiva = 1,

    /// <summary>Reparos decorrentes de falha (motor, câmbio, embreagem).</summary>
    ManutencaoCorretiva = 2,

    /// <summary>Alinhamento, balanceamento, suspensão, freios.</summary>
    Suspensao = 3,

    /// <summary>Sistema elétrico, bateria, injeção eletrônica.</summary>
    Eletrica = 4,

    /// <summary>Funilaria e pintura.</summary>
    Funilaria = 5,

    /// <summary>Diagnóstico e escaneamento eletrônico.</summary>
    Diagnostico = 6,

    /// <summary>Demais serviços.</summary>
    Outros = 99
}

/// <summary>
/// <b>Raiz de Agregado</b> do catálogo de serviços da oficina (troca de óleo, alinhamento…).
/// <para>
/// É o <b>preço de tabela</b> vigente. Quando um serviço entra em uma Ordem de Serviço, o
/// preço é <b>copiado</b> para o item da OS: uma alteração posterior na tabela não pode
/// modificar retroativamente um orçamento já enviado ao cliente.
/// </para>
/// <para><b>Invariantes:</b> nome único e não vazio; preço maior que zero; tempo estimado
/// positivo (é o insumo do indicador de tempo médio de execução).</para>
/// </summary>
public sealed class Servico : AggregateRoot
{
    private const int ComprimentoMinimoNome = 3;
    private const int ComprimentoMaximoNome = 120;
    private const int TempoEstimadoMaximoEmMinutos = 60 * 24 * 30;

    private Servico()
    {
        Nome = null!;
        Preco = null!;
    }

    private Servico(
        Guid id,
        string nome,
        string? descricao,
        CategoriaServico categoria,
        Dinheiro preco,
        int tempoEstimadoEmMinutos)
        : base(id)
    {
        Nome = nome;
        Descricao = descricao;
        Categoria = categoria;
        Preco = preco;
        TempoEstimadoEmMinutos = tempoEstimadoEmMinutos;
        Ativo = true;
        CadastradoEm = DateTimeOffset.UtcNow;
    }

    public string Nome { get; private set; }

    public string? Descricao { get; private set; }

    public CategoriaServico Categoria { get; private set; }

    /// <summary>Preço de tabela vigente. Copiado para o item da OS no momento da inclusão.</summary>
    public Dinheiro Preco { get; private set; }

    /// <summary>Tempo padrão de execução, base para prometer prazo ao cliente.</summary>
    public int TempoEstimadoEmMinutos { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset CadastradoEm { get; private set; }

    public DateTimeOffset? AtualizadoEm { get; private set; }

    public TimeSpan TempoEstimado => TimeSpan.FromMinutes(TempoEstimadoEmMinutos);

    public static Servico Cadastrar(
        string? nome,
        string? descricao,
        CategoriaServico categoria,
        decimal preco,
        int tempoEstimadoEmMinutos)
    {
        var servico = new Servico(
            NovoId(),
            ValidarNome(nome),
            NormalizarDescricao(descricao),
            categoria,
            ValidarPreco(preco),
            ValidarTempoEstimado(tempoEstimadoEmMinutos));

        servico.RegistrarEvento(new ServicoCadastrado(servico.Id, servico.Nome, servico.Preco.Valor));

        return servico;
    }

    public void AtualizarDados(
        string? nome,
        string? descricao,
        CategoriaServico categoria,
        int tempoEstimadoEmMinutos)
    {
        GarantirServicoAtivo();

        Nome = ValidarNome(nome);
        Descricao = NormalizarDescricao(descricao);
        Categoria = categoria;
        TempoEstimadoEmMinutos = ValidarTempoEstimado(tempoEstimadoEmMinutos);
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Reajusta o preço de tabela. Emite evento com o valor anterior e o novo, dando
    /// rastreabilidade à formação de preço exigida pela gestão.
    /// </summary>
    public void ReajustarPreco(decimal novoPreco)
    {
        GarantirServicoAtivo();

        var precoValidado = ValidarPreco(novoPreco);

        if (precoValidado == Preco)
        {
            return;
        }

        var anterior = Preco;
        Preco = precoValidado;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new PrecoDoServicoReajustado(Id, anterior.Valor, precoValidado.Valor));
    }

    public void Inativar()
    {
        if (!Ativo)
        {
            return;
        }

        Ativo = false;
        AtualizadoEm = DateTimeOffset.UtcNow;

        RegistrarEvento(new ServicoInativado(Id, Nome));
    }

    public void Reativar()
    {
        if (Ativo)
        {
            return;
        }

        Ativo = true;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    /// <summary>Impede que um serviço fora de linha seja incluído em uma nova Ordem de Serviço.</summary>
    public void GarantirServicoAtivo()
    {
        if (!Ativo)
        {
            throw new DomainException(
                "SERVICO_INATIVO",
                $"O serviço '{Nome}' está inativo e não pode ser incluído em uma Ordem de Serviço.");
        }
    }

    private static string ValidarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new DomainException("NOME_OBRIGATORIO", "O nome do serviço é obrigatório.");
        }

        var limpo = nome.Trim();

        if (limpo.Length is < ComprimentoMinimoNome or > ComprimentoMaximoNome)
        {
            throw new DomainException(
                "NOME_INVALIDO",
                $"O nome do serviço deve ter entre {ComprimentoMinimoNome} e {ComprimentoMaximoNome} caracteres.");
        }

        return limpo;
    }

    private static Dinheiro ValidarPreco(decimal preco)
    {
        if (preco <= 0)
        {
            throw new DomainException("PRECO_INVALIDO", "O preço do serviço deve ser maior que zero.");
        }

        return Dinheiro.De(preco);
    }

    private static int ValidarTempoEstimado(int minutos)
    {
        if (minutos is <= 0 or > TempoEstimadoMaximoEmMinutos)
        {
            throw new DomainException(
                "TEMPO_ESTIMADO_INVALIDO",
                $"O tempo estimado deve estar entre 1 e {TempoEstimadoMaximoEmMinutos} minutos.");
        }

        return minutos;
    }

    private static string? NormalizarDescricao(string? descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            return null;
        }

        var limpa = descricao.Trim();

        if (limpa.Length > 500)
        {
            throw new DomainException("DESCRICAO_INVALIDA", "A descrição do serviço excede 500 caracteres.");
        }

        return limpa;
    }
}
