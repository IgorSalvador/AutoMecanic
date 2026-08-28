using AutoMecanic.Domain.Estoque;
using AutoMecanic.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento do agregado <see cref="Peca"/>.</summary>
internal sealed class PecaConfiguration : IEntityTypeConfiguration<Peca>
{
    public void Configure(EntityTypeBuilder<Peca> builder)
    {
        builder.ToTable("peca", tabela =>
        {
            tabela.HasComment("Peças e insumos controlados pelo estoque da oficina.");

            // As invariantes de saldo também são declaradas no banco. Se um dia alguém
            // alterar dados por SQL direto, a restrição continua valendo.
            tabela.HasCheckConstraint("ck_peca_saldo_nao_negativo", "quantidade_em_estoque >= 0");
            tabela.HasCheckConstraint("ck_peca_reserva_nao_negativa", "quantidade_reservada >= 0");
            tabela.HasCheckConstraint(
                "ck_peca_reserva_menor_que_saldo",
                "quantidade_reservada <= quantidade_em_estoque");
            tabela.HasCheckConstraint("ck_peca_preco_positivo", "preco_unitario > 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Codigo).HasMaxLength(40).IsRequired();
        builder.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(500);

        builder.Property(p => p.UnidadeMedida)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.PrecoUnitario)
            .HasConversion(ConversoresDeValueObject.Dinheiro)
            .HasColumnName("preco_unitario")
            .HasColumnType("numeric(14,2)")
            .IsRequired();

        builder.Property(p => p.QuantidadeEmEstoque).IsRequired();
        builder.Property(p => p.QuantidadeReservada).IsRequired();
        builder.Property(p => p.EstoqueMinimo).IsRequired();
        builder.Property(p => p.Ativo).IsRequired();
        builder.Property(p => p.CadastradoEm).IsRequired();

        // QuantidadeDisponivel e AbaixoDoEstoqueMinimo são derivadas do saldo: calculadas
        // em memória, nunca armazenadas, para que não possam divergir da fonte da verdade.
        builder.Ignore(p => p.QuantidadeDisponivel);
        builder.Ignore(p => p.AbaixoDoEstoqueMinimo);

        builder.HasIndex(p => p.Codigo)
            .IsUnique()
            .HasDatabaseName("ux_peca_codigo");

        builder.HasIndex(p => p.Nome).HasDatabaseName("ix_peca_nome");
        builder.HasIndex(p => p.Ativo).HasDatabaseName("ix_peca_ativo");

        ConfiguracaoComum.ConfigurarConcorrencia(builder);
        ConfiguracaoComum.IgnorarEventosDeDominio(builder);
    }
}

/// <summary>Mapeamento do razão de estoque (<see cref="MovimentoEstoque"/>).</summary>
internal sealed class MovimentoEstoqueConfiguration : IEntityTypeConfiguration<MovimentoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentoEstoque> builder)
    {
        builder.ToTable("movimento_estoque", tabela =>
            tabela.HasComment("Razão append-only de todas as movimentações de estoque."));

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Quantidade).IsRequired();
        builder.Property(m => m.SaldoAnterior).IsRequired();
        builder.Property(m => m.SaldoAtual).IsRequired();
        builder.Property(m => m.Motivo).HasMaxLength(300).IsRequired();
        builder.Property(m => m.OcorridoEm).IsRequired();

        builder.HasOne<Peca>()
            .WithMany()
            .HasForeignKey(m => m.PecaId)
            .HasConstraintName("fk_movimento_estoque_peca")
            .OnDelete(DeleteBehavior.Restrict);

        // Índice composto na ordem em que o extrato é consultado: uma peça, do mais
        // recente para o mais antigo.
        builder.HasIndex(m => new { m.PecaId, m.OcorridoEm })
            .HasDatabaseName("ix_movimento_estoque_peca_data")
            .IsDescending(false, true);

        builder.HasIndex(m => m.OrdemServicoId).HasDatabaseName("ix_movimento_estoque_ordem_servico");

        ConfiguracaoComum.ConfigurarConcorrencia(builder);
        ConfiguracaoComum.IgnorarEventosDeDominio(builder);
    }
}
