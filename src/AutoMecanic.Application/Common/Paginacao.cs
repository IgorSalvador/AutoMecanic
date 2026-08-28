namespace AutoMecanic.Application.Common;

/// <summary>
/// Parâmetros de paginação das listagens. O tamanho de página é limitado no próprio tipo
/// para que nenhuma consulta consiga pedir a base inteira em uma requisição.
/// </summary>
public sealed record ParametrosDePaginacao
{
    /// <summary>Maior tamanho de página aceito, independentemente do que o cliente pedir.</summary>
    public const int TamanhoMaximoDePagina = 100;

    private const int TamanhoPadraoDePagina = 20;

    private readonly int _pagina = 1;
    private readonly int _tamanhoPagina = TamanhoPadraoDePagina;

    /// <summary>Número da página, começando em 1.</summary>
    public int Pagina
    {
        get => _pagina;
        init => _pagina = value < 1 ? 1 : value;
    }

    /// <summary>Itens por página. Valores fora da faixa são ajustados silenciosamente.</summary>
    public int TamanhoPagina
    {
        get => _tamanhoPagina;
        init => _tamanhoPagina = value switch
        {
            < 1 => TamanhoPadraoDePagina,
            > TamanhoMaximoDePagina => TamanhoMaximoDePagina,
            _ => value
        };
    }

    /// <summary>Quantidade de registros a pular na consulta.</summary>
    public int Deslocamento => (Pagina - 1) * TamanhoPagina;
}

/// <summary>Página de resultados com os metadados necessários à navegação pelo cliente.</summary>
/// <typeparam name="T">Tipo do item retornado.</typeparam>
public sealed record ResultadoPaginado<T>
{
    public required IReadOnlyList<T> Itens { get; init; }

    public required int Pagina { get; init; }

    public required int TamanhoPagina { get; init; }

    /// <summary>Total de registros que atendem ao filtro, ignorando a paginação.</summary>
    public required int TotalDeItens { get; init; }

    public int TotalDePaginas => TamanhoPagina == 0 ? 0 : (int)Math.Ceiling(TotalDeItens / (double)TamanhoPagina);

    public bool TemPaginaAnterior => Pagina > 1;

    public bool TemProximaPagina => Pagina < TotalDePaginas;

    public static ResultadoPaginado<T> Criar(IReadOnlyList<T> itens, int totalDeItens, ParametrosDePaginacao parametros) =>
        new()
        {
            Itens = itens,
            Pagina = parametros.Pagina,
            TamanhoPagina = parametros.TamanhoPagina,
            TotalDeItens = totalDeItens
        };

    public static ResultadoPaginado<T> Vazio(ParametrosDePaginacao parametros) =>
        Criar([], 0, parametros);
}
