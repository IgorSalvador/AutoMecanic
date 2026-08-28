using AutoMecanic.Domain.Identidade;
using AutoMecanic.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento do agregado <see cref="Usuario"/>.</summary>
internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", tabela =>
            tabela.HasComment("Usuários administrativos com acesso às APIs protegidas por JWT."));

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Nome).HasMaxLength(150).IsRequired();

        builder.Property(u => u.Email)
            .HasConversion(ConversoresDeValueObject.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ux_usuario_email");

        // 60 caracteres é o comprimento fixo de um hash BCrypt ($2a$<custo>$<salt+hash>).
        builder.Property(u => u.SenhaHash).HasMaxLength(72).IsRequired();

        builder.Property(u => u.Perfil)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.Ativo).IsRequired();
        builder.Property(u => u.TentativasFalhas).IsRequired();
        builder.Property(u => u.CadastradoEm).IsRequired();

        builder.HasIndex(u => u.Perfil).HasDatabaseName("ix_usuario_perfil");

        ConfiguracaoComum.ConfigurarConcorrencia(builder);
        ConfiguracaoComum.IgnorarEventosDeDominio(builder);
    }
}
