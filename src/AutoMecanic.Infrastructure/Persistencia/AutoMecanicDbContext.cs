using System.Reflection;
using System.Text;
using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Estoque;
using AutoMecanic.Domain.Identidade;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.Servicos;
using AutoMecanic.Domain.Veiculos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AutoMecanic.Infrastructure.Persistencia;

/// <summary>
/// Contexto de persistência do sistema. Expõe uma coleção por <b>raiz de agregado</b> — e não
/// uma por tabela — refletindo no acesso a dados a mesma fronteira usada no modelo de domínio.
/// </summary>
public sealed class AutoMecanicDbContext(DbContextOptions<AutoMecanicDbContext> options) : DbContext(options)
{
    /// <summary>Nome do esquema onde todas as tabelas da aplicação residem.</summary>
    public const string Esquema = "automecanic";

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Veiculo> Veiculos => Set<Veiculo>();

    public DbSet<Servico> Servicos => Set<Servico>();

    public DbSet<Peca> Pecas => Set<Peca>();

    public DbSet<MovimentoEstoque> MovimentosDeEstoque => Set<MovimentoEstoque>();

    public DbSet<OrdemServico> OrdensDeServico => Set<OrdemServico>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        AplicarConvencaoSnakeCase(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Todo texto sem tamanho explícito recebe um limite conservador, evitando colunas
        // ilimitadas por descuido.
        configurationBuilder.Properties<string>().HaveMaxLength(500);

        base.ConfigureConventions(configurationBuilder);
    }

    /// <summary>
    /// Converte nomes de tabelas, colunas, chaves e índices de <c>PascalCase</c> para
    /// <c>snake_case</c>, que é a convenção idiomática do PostgreSQL — evita a necessidade de
    /// aspas duplas em toda consulta escrita à mão.
    /// <para>
    /// Nomes definidos explicitamente nas configurações são preservados: a convenção só
    /// preenche o que não foi decidido.
    /// </para>
    /// </summary>
    private static void AplicarConvencaoSnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            if (tipo.FindAnnotation(RelationalAnnotationNames.TableName) is null && tipo.GetTableName() is { } tabela)
            {
                tipo.SetTableName(ParaSnakeCase(tabela));
            }

            var identificador = StoreObjectIdentifier.Create(tipo, StoreObjectType.Table);

            foreach (var propriedade in tipo.GetProperties())
            {
                if (propriedade.FindAnnotation(RelationalAnnotationNames.ColumnName) is not null)
                {
                    continue;
                }

                var coluna = identificador is null
                    ? propriedade.Name
                    : propriedade.GetColumnName(identificador.Value) ?? propriedade.Name;

                propriedade.SetColumnName(ParaSnakeCase(coluna));
            }

            foreach (var chave in tipo.GetKeys())
            {
                if (chave.GetName() is { } nome)
                {
                    chave.SetName(ParaSnakeCase(nome));
                }
            }

            foreach (var chaveEstrangeira in tipo.GetForeignKeys())
            {
                if (chaveEstrangeira.GetConstraintName() is { } nome)
                {
                    chaveEstrangeira.SetConstraintName(ParaSnakeCase(nome));
                }
            }

            foreach (var indice in tipo.GetIndexes())
            {
                if (indice.GetDatabaseName() is { } nome)
                {
                    indice.SetDatabaseName(ParaSnakeCase(nome));
                }
            }
        }
    }

    /// <summary>
    /// "OrdemServico" → "ordem_servico"; "IX_Cliente_Documento" → "ix_cliente_documento";
    /// "QuantidadeEmEstoque" → "quantidade_em_estoque".
    /// </summary>
    private static string ParaSnakeCase(string nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return nome;
        }

        var construtor = new StringBuilder(nome.Length + 8);

        for (var i = 0; i < nome.Length; i++)
        {
            var caractere = nome[i];

            if (char.IsUpper(caractere))
            {
                // Sublinha antes de uma maiúscula apenas quando ela inicia uma nova palavra,
                // preservando siglas ("OSNumero" vira "os_numero", não "o_s_numero").
                var iniciaPalavra = i > 0
                    && nome[i - 1] != '_'
                    && (!char.IsUpper(nome[i - 1]) || (i + 1 < nome.Length && char.IsLower(nome[i + 1])));

                if (iniciaPalavra)
                {
                    construtor.Append('_');
                }

                construtor.Append(char.ToLowerInvariant(caractere));
            }
            else
            {
                construtor.Append(caractere);
            }
        }

        return construtor.ToString();
    }
}
