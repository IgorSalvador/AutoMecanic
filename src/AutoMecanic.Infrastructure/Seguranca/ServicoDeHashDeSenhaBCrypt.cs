using AutoMecanic.Application.Abstractions;

namespace AutoMecanic.Infrastructure.Seguranca;

/// <summary>
/// Hash de senha com BCrypt.
/// <para>
/// BCrypt é uma função <b>deliberadamente lenta</b> e com <i>salt</i> embutido por hash. Isso
/// torna inviável tanto o ataque por tabela arco-íris quanto a força bruta em massa sobre um
/// vazamento do banco — ao contrário de SHA-256 e semelhantes, que são rápidos por projeto e
/// portanto inadequados para senhas.
/// </para>
/// <para>
/// O fator de custo 12 significa 2¹² iterações: aproximadamente 250 ms por verificação em
/// hardware atual. É o ponto de equilíbrio entre resistência a ataque e latência de login.
/// </para>
/// </summary>
public sealed class ServicoDeHashDeSenhaBCrypt : IServicoDeHashDeSenha
{
    private const int FatorDeCusto = 12;

    public string GerarHash(string senha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senha);

        return BCrypt.Net.BCrypt.HashPassword(senha, FatorDeCusto);
    }

    public bool Verificar(string senha, string hash)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrompido ou em formato desconhecido: trata como falha de autenticação,
            // nunca como erro do servidor, para não vazar detalhe do armazenamento.
            return false;
        }
    }
}
