using AutoMecanic.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AutoMecanic.Infrastructure.Persistencia;

/// <summary>
/// Contador de Ordens de Serviço por ano. Tabela de apoio, sem comportamento de domínio.
/// </summary>
public sealed class SequenciaOrdemServico
{
    /// <summary>Ano ao qual o contador se refere. Chave primária.</summary>
    public int Ano { get; set; }

    /// <summary>Último número já entregue naquele ano.</summary>
    public int UltimoValor { get; set; }
}

/// <summary>Mapeamento da tabela de sequência.</summary>
internal sealed class SequenciaOrdemServicoConfiguration : IEntityTypeConfiguration<SequenciaOrdemServico>
{
    public void Configure(EntityTypeBuilder<SequenciaOrdemServico> builder)
    {
        builder.ToTable("sequencia_ordem_servico", tabela =>
            tabela.HasComment("Contador do número sequencial de Ordem de Serviço, reiniciado a cada ano."));

        builder.HasKey(s => s.Ano);
        builder.Property(s => s.Ano).ValueGeneratedNever();
        builder.Property(s => s.UltimoValor).IsRequired();
    }
}

/// <summary>
/// Gera o próximo número sequencial de Ordem de Serviço.
/// <para>
/// A alocação é feita por um único <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>. Essa
/// forma é <b>atômica no banco</b>: duas requisições simultâneas são serializadas pelo bloqueio
/// de linha do PostgreSQL e recebem números diferentes, sem necessidade de bloqueio na
/// aplicação. Ler-e-incrementar em duas etapas produziria números duplicados sob concorrência.
/// </para>
/// <para>
/// A escolha por tabela de contador (e não por <c>SEQUENCE</c>) atende ao requisito de que a
/// numeração reinicie a cada ano, algo que uma sequência do PostgreSQL não faz sozinha.
/// </para>
/// </summary>
public sealed class GeradorDeNumeroDeOrdemServico(AutoMecanicDbContext contexto) : IGeradorDeNumeroDeOrdemServico
{
    private const string ComandoDeAlocacao = $"""
        INSERT INTO "{AutoMecanicDbContext.Esquema}".sequencia_ordem_servico (ano, ultimo_valor)
        VALUES (@ano, 1)
        ON CONFLICT (ano)
        DO UPDATE SET ultimo_valor = "{AutoMecanicDbContext.Esquema}".sequencia_ordem_servico.ultimo_valor + 1
        RETURNING ultimo_valor;
        """;

    public async Task<int> ProximoSequencialAsync(int ano, CancellationToken cancellationToken = default)
    {
        var conexao = contexto.Database.GetDbConnection();
        var precisaAbrir = conexao.State != System.Data.ConnectionState.Open;

        if (precisaAbrir)
        {
            await contexto.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var comando = conexao.CreateCommand();

            comando.CommandText = ComandoDeAlocacao;
            comando.Parameters.Add(new NpgsqlParameter("ano", ano));

            // Participa da transação corrente quando existir: se o caso de uso falhar
            // depois, o número alocado é devolvido junto com o rollback.
            if (contexto.Database.CurrentTransaction is { } transacao)
            {
                comando.Transaction = transacao.GetDbTransaction();
            }

            var resultado = await comando.ExecuteScalarAsync(cancellationToken);

            return Convert.ToInt32(resultado);
        }
        finally
        {
            if (precisaAbrir)
            {
                await contexto.Database.CloseConnectionAsync();
            }
        }
    }
}
