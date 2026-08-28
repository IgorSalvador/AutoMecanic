using AutoMecanic.Domain.Servicos;
using AutoMecanic.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento do catálogo de <see cref="Servico"/>.</summary>
internal sealed class ServicoConfiguration : IEntityTypeConfiguration<Servico>
{
    public void Configure(EntityTypeBuilder<Servico> builder)
    {
        builder.ToTable("servico", tabela =>
            tabela.HasComment("Catálogo de serviços prestados, com preço de tabela e tempo estimado."));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Nome).HasMaxLength(120).IsRequired();
        builder.Property(s => s.Descricao).HasMaxLength(500);

        // Enumerações gravadas por nome: torna as consultas diretas ao banco legíveis
        // e desacopla o esquema da ordem numérica dos membros no código.
        builder.Property(s => s.Categoria)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.Preco)
            .HasConversion(ConversoresDeValueObject.Dinheiro)
            .HasColumnName("preco")
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(s => s.TempoEstimadoEmMinutos).IsRequired();
        builder.Property(s => s.Ativo).IsRequired();
        builder.Property(s => s.CadastradoEm).IsRequired();

        builder.HasIndex(s => s.Nome)
            .IsUnique()
            .HasDatabaseName("ux_servico_nome");

        builder.HasIndex(s => s.Categoria).HasDatabaseName("ix_servico_categoria");

        ConfiguracaoComum.ConfigurarConcorrencia(builder);
        ConfiguracaoComum.IgnorarEventosDeDominio(builder);
    }
}
