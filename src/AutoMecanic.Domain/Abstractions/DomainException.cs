namespace AutoMecanic.Domain.Abstractions;

/// <summary>
/// Violação de uma <b>invariante</b> ou <b>regra de negócio</b> do domínio.
/// É traduzida pela camada de API para <c>422 Unprocessable Entity</c>, distinguindo-a
/// de erros de formato de requisição (<c>400</c>) e de falhas técnicas (<c>500</c>).
/// </summary>
public class DomainException : Exception
{
    public DomainException(string mensagem) : base(mensagem)
    {
    }

    public DomainException(string codigo, string mensagem) : base(mensagem) => Codigo = codigo;

    /// <summary>Código estável da regra violada, útil para o cliente da API reagir programaticamente.</summary>
    public string? Codigo { get; }

    /// <summary>Lança <see cref="DomainException"/> quando a condição informada for verdadeira.</summary>
    public static void LancarSe(bool condicao, string mensagem)
    {
        if (condicao)
        {
            throw new DomainException(mensagem);
        }
    }
}
