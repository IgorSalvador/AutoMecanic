using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento do agregado <see cref="Veiculo"/>.</summary>
internal sealed class VeiculoConfiguration : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> builder)
    {
        builder.ToTable("veiculo", tabela =>
            tabela.HasComment("Veículos atendidos pela oficina, vinculados a um cliente."));

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Placa)
            .HasConversion(ConversoresDeValueObject.Placa)
            .HasColumnName("placa")
            .HasMaxLength(7)
            .IsRequired();

        builder.HasIndex(v => v.Placa)
            .IsUnique()
            .HasDatabaseName("ux_veiculo_placa");

        builder.Property(v => v.Marca).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Modelo).HasMaxLength(80).IsRequired();
        builder.Property(v => v.Cor).HasMaxLength(30);
        builder.Property(v => v.AnoFabricacao).IsRequired();
        builder.Property(v => v.AnoModelo).IsRequired();
        builder.Property(v => v.Quilometragem).IsRequired();
        builder.Property(v => v.Ativo).IsRequired();
        builder.Property(v => v.CadastradoEm).IsRequired();

        // Relacionamento entre agregados declarado apenas pela chave estrangeira: não há
        // propriedade de navegação de Veiculo para Cliente no domínio, justamente para
        // impedir que o código atravesse a fronteira do agregado sem perceber.
        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(v => v.ClienteId)
            .HasConstraintName("fk_veiculo_cliente")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.ClienteId).HasDatabaseName("ix_veiculo_cliente_id");
        builder.HasIndex(v => v.Ativo).HasDatabaseName("ix_veiculo_ativo");

        ConfiguracaoComum.ConfigurarConcorrencia(builder);
        ConfiguracaoComum.IgnorarEventosDeDominio(builder);
    }
}
