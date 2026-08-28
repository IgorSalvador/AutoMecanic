using AutoMecanic.Domain.Clientes;
using AutoMecanic.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento do agregado <see cref="Cliente"/>.</summary>
internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("cliente", tabela =>
            tabela.HasComment("Clientes atendidos pela oficina, pessoa física ou jurídica."));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Nome)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.Documento)
            .HasConversion(ConversoresDeValueObject.Documento)
            .HasColumnName("documento")
            .HasMaxLength(14)
            .IsRequired();

        // Chave natural: garante no banco a mesma unicidade verificada na aplicação,
        // fechando a janela de corrida entre duas requisições simultâneas.
        builder.HasIndex(c => c.Documento)
            .IsUnique()
            .HasDatabaseName("ux_cliente_documento");

        builder.Property(c => c.Email)
            .HasConversion(ConversoresDeValueObject.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(c => c.Telefone)
            .HasConversion(ConversoresDeValueObject.Telefone)
            .HasColumnName("telefone")
            .HasMaxLength(11)
            .IsRequired();

        // Endereço tem vários campos e nenhum sentido fora do cliente: mapeado como tipo
        // pertencente (owned), materializado nas colunas do próprio registro do cliente.
        builder.OwnsOne(c => c.Endereco, endereco =>
        {
            endereco.Property(e => e.Logradouro).HasColumnName("endereco_logradouro").HasMaxLength(200);
            endereco.Property(e => e.Numero).HasColumnName("endereco_numero").HasMaxLength(20);
            endereco.Property(e => e.Complemento).HasColumnName("endereco_complemento").HasMaxLength(100);
            endereco.Property(e => e.Bairro).HasColumnName("endereco_bairro").HasMaxLength(100);
            endereco.Property(e => e.Cidade).HasColumnName("endereco_cidade").HasMaxLength(100);
            endereco.Property(e => e.Uf).HasColumnName("endereco_uf").HasMaxLength(2);
            endereco.Property(e => e.Cep).HasColumnName("endereco_cep").HasMaxLength(8);
        });

        builder.Property(c => c.Ativo).IsRequired();
        builder.Property(c => c.CadastradoEm).IsRequired();

        builder.HasIndex(c => c.Nome).HasDatabaseName("ix_cliente_nome");
        builder.HasIndex(c => c.Ativo).HasDatabaseName("ix_cliente_ativo");

        ConfiguracaoComum.ConfigurarConcorrencia(builder);
        ConfiguracaoComum.IgnorarEventosDeDominio(builder);
    }
}
