namespace AutoMecanic.Application.Common;

/// <summary>
/// Recurso solicitado não existe. Traduzido para <c>404 Not Found</c> pela API.
/// </summary>
public sealed class RecursoNaoEncontradoException : Exception
{
    public RecursoNaoEncontradoException(string recurso, object identificador)
        : base($"{recurso} '{identificador}' não foi encontrado(a).")
    {
        Recurso = recurso;
        Identificador = identificador;
    }

    public RecursoNaoEncontradoException(string mensagem) : base(mensagem)
    {
        Recurso = string.Empty;
        Identificador = string.Empty;
    }

    public string Recurso { get; }

    public object Identificador { get; }
}

/// <summary>
/// A operação conflita com o estado atual dos dados — tipicamente uma chave natural duplicada
/// (CPF, placa, código de peça). Traduzido para <c>409 Conflict</c>.
/// </summary>
public sealed class ConflitoException : Exception
{
    public ConflitoException(string mensagem) : base(mensagem)
    {
    }

    public ConflitoException(string codigo, string mensagem) : base(mensagem) => Codigo = codigo;

    public string? Codigo { get; }
}

/// <summary>
/// Erros de validação de entrada, agrupados por campo. Traduzido para <c>400 Bad Request</c>
/// com o corpo no formato <c>ValidationProblemDetails</c>.
/// </summary>
public sealed class ValidacaoException : Exception
{
    public ValidacaoException(IDictionary<string, string[]> erros)
        : base("Um ou mais campos da requisição são inválidos.") => Erros = erros;

    public ValidacaoException(string campo, string erro)
        : base("Um ou mais campos da requisição são inválidos.") =>
        Erros = new Dictionary<string, string[]> { [campo] = [erro] };

    public IDictionary<string, string[]> Erros { get; }
}

/// <summary>
/// Credenciais inválidas ou sessão não autorizada. Traduzido para <c>401 Unauthorized</c>.
/// A mensagem é deliberadamente genérica para não revelar se um e-mail existe na base.
/// </summary>
public sealed class NaoAutorizadoException : Exception
{
    public NaoAutorizadoException(string mensagem = "Credenciais inválidas.") : base(mensagem)
    {
    }
}
