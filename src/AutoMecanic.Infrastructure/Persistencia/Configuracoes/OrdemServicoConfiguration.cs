using AutoMecanic.Domain.Clientes;
using AutoMecanic.Domain.OrdensServico;
using AutoMecanic.Domain.Veiculos;
using AutoMecanic.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento do agregado <see cref="OrdemServico"/> e de suas entidades filhas.
/// <para>
/// Itens, orçamento e histórico são mapeados como <b>navegações pertencentes ao agregado</b>,
/// com exclusão em cascata: eles não existem fora da Ordem de Serviço. O acesso é feito pelos
/// campos privados, preservando o encapsulamento das coleções no domínio.
/// </para>
/// </summary>
internal sealed class OrdemServicoConfiguration : IEntityTypeConfiguration<OrdemServico>
{
    public void Configure(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.ToTable("ordem_servico", tabela =>
            tabela.HasComment("Ordens de Serviço: o agregado central do sistema."));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Numero)
            .HasConversion(ConversoresDeValueObject.NumeroOrdemServico)
            .HasColumnName("numero")
            .HasMaxLength(14)
            .IsRequired();

        builder.HasIndex(o => o.Numero)
            .IsUnique()
            .HasDatabaseName("ux_ordem_servico_numero");

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(o => o.DescricaoProblema).HasMaxLength(2000).IsRequired();
        builder.Property(o => o.DiagnosticoTecnico).HasMaxLength(4000);
        builder.Property(o => o.MotivoCancelamento).HasMaxLength(500);
        builder.Property(o => o.CriadaEm).IsRequired();

        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(o => o.ClienteId)
            .HasConstraintName("fk_ordem_servico_cliente")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Veiculo>()
            .WithMany()
            .HasForeignKey(o => o.VeiculoId)
            .HasConstraintName("fk_ordem_servico_veiculo")
            .OnDelete(DeleteBehavior.Restrict);

        // As coleções públicas são apenas fachadas somente-leitura sobre os campos privados,
        // que são as navegações realmente mapeadas. Ignorá-las evita mapeamento duplicado.
        builder.Ignore(o => o.ItensServico);
        builder.Ignore(o => o.ItensPeca);
        builder.Ignore(o => o.Historico);

        // Propriedades calculadas a partir dos itens: nunca persistidas.
        builder.Ignore(o => o.ValorTotalServicos);
        builder.Ignore(o => o.ValorTotalPecas);
        builder.Ignore(o => o.TempoEstimadoTotalEmMinutos);
        builder.Ignore(o => o.DuracaoDaExecucao);
        builder.Ignore(o => o.TempoTotalDeAtendimento);

        ConfigurarItensDeServico(builder);
        ConfigurarItensDePeca(builder);
        ConfigurarOrcamento(builder);
        ConfigurarHistorico(builder);

        // Índices que sustentam as listagens do painel e a consulta por período.
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_ordem_servico_status");
        builder.HasIndex(o => o.ClienteId).HasDatabaseName("ix_ordem_servico_cliente_id");
        builder.HasIndex(o => o.VeiculoId).HasDatabaseName("ix_ordem_servico_veiculo_id");
        builder.HasIndex(o => o.CriadaEm).HasDatabaseName("ix_ordem_servico_criada_em");
        builder.HasIndex(o => o.FinalizadaEm).HasDatabaseName("ix_ordem_servico_finalizada_em");

        ConfiguracaoComum.ConfigurarConcorrencia(builder);
        ConfiguracaoComum.IgnorarEventosDeDominio(builder);
    }

    private static void ConfigurarItensDeServico(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.OwnsMany<ItemServico>("_itensServico", item =>
        {
            item.ToTable("ordem_servico_item_servico", tabela =>
                tabela.HasComment("Serviços contratados em uma OS, com preço congelado na inclusão."));

            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();

            item.WithOwner().HasForeignKey(i => i.OrdemServicoId);

            item.Property(i => i.Descricao).HasMaxLength(120).IsRequired();

            item.Property(i => i.PrecoUnitario)
                .HasConversion(ConversoresDeValueObject.Dinheiro)
                .HasColumnName("preco_unitario")
                .HasColumnType("numeric(14,2)")
                .IsRequired();

            item.Property(i => i.Quantidade).IsRequired();
            item.Property(i => i.TempoEstimadoEmMinutos).IsRequired();
            item.Property(i => i.AdicionadoEm).IsRequired();

            item.Ignore(i => i.Subtotal);
            item.Ignore(i => i.TempoTotalEstimadoEmMinutos);

            item.HasIndex(i => i.ServicoId).HasDatabaseName("ix_ordem_servico_item_servico_servico_id");
        });

        builder.Navigation("_itensServico")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarItensDePeca(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.OwnsMany<ItemPeca>("_itensPeca", item =>
        {
            item.ToTable("ordem_servico_item_peca", tabela =>
                tabela.HasComment("Peças previstas em uma OS, com preço congelado e situação de reserva."));

            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();

            item.WithOwner().HasForeignKey(i => i.OrdemServicoId);

            item.Property(i => i.CodigoPeca).HasMaxLength(40).IsRequired();
            item.Property(i => i.NomePeca).HasMaxLength(150).IsRequired();

            item.Property(i => i.PrecoUnitario)
                .HasConversion(ConversoresDeValueObject.Dinheiro)
                .HasColumnName("preco_unitario")
                .HasColumnType("numeric(14,2)")
                .IsRequired();

            item.Property(i => i.Quantidade).IsRequired();
            item.Property(i => i.Reservada).IsRequired();
            item.Property(i => i.Consumida).IsRequired();
            item.Property(i => i.AdicionadoEm).IsRequired();

            item.Ignore(i => i.Subtotal);

            item.HasIndex(i => i.PecaId).HasDatabaseName("ix_ordem_servico_item_peca_peca_id");
        });

        builder.Navigation("_itensPeca")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigurarOrcamento(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.OwnsOne(o => o.Orcamento, orcamento =>
        {
            orcamento.ToTable("orcamento", tabela =>
                tabela.HasComment("Orçamento gerado automaticamente a partir dos itens da OS."));

            orcamento.HasKey(o => o.Id);
            orcamento.Property(o => o.Id).ValueGeneratedNever();

            orcamento.WithOwner().HasForeignKey(o => o.OrdemServicoId);

            orcamento.Property(o => o.ValorServicos)
                .HasConversion(ConversoresDeValueObject.Dinheiro)
                .HasColumnName("valor_servicos")
                .HasColumnType("numeric(14,2)")
                .IsRequired();

            orcamento.Property(o => o.ValorPecas)
                .HasConversion(ConversoresDeValueObject.Dinheiro)
                .HasColumnName("valor_pecas")
                .HasColumnType("numeric(14,2)")
                .IsRequired();

            orcamento.Property(o => o.ValorTotal)
                .HasConversion(ConversoresDeValueObject.Dinheiro)
                .HasColumnName("valor_total")
                .HasColumnType("numeric(14,2)")
                .IsRequired();

            orcamento.Property(o => o.PercentualDesconto)
                .HasColumnType("numeric(5,2)")
                .IsRequired();

            orcamento.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(25)
                .IsRequired();

            orcamento.Property(o => o.MotivoReprovacao).HasMaxLength(500);
            orcamento.Property(o => o.GeradoEm).IsRequired();

            orcamento.Ignore(o => o.ValorBruto);
            orcamento.Ignore(o => o.ValorDesconto);
            orcamento.Ignore(o => o.FoiRespondido);

            orcamento.HasIndex(o => o.Status).HasDatabaseName("ix_orcamento_status");
            orcamento.HasIndex(o => o.ValidoAte).HasDatabaseName("ix_orcamento_valido_ate");
        });

        builder.Navigation(o => o.Orcamento)
            .UsePropertyAccessMode(PropertyAccessMode.Property);
    }

    private static void ConfigurarHistorico(EntityTypeBuilder<OrdemServico> builder)
    {
        builder.OwnsMany<HistoricoStatus>("_historico", historico =>
        {
            historico.ToTable("ordem_servico_historico", tabela =>
                tabela.HasComment("Linha do tempo de transições de status da OS."));

            historico.HasKey(h => h.Id);
            historico.Property(h => h.Id).ValueGeneratedNever();

            historico.WithOwner().HasForeignKey(h => h.OrdemServicoId);

            historico.Property(h => h.StatusAnterior)
                .HasConversion<string>()
                .HasMaxLength(25);

            historico.Property(h => h.StatusAtual)
                .HasConversion<string>()
                .HasMaxLength(25)
                .IsRequired();

            historico.Property(h => h.Observacao).HasMaxLength(500);
            historico.Property(h => h.OcorridoEm).IsRequired();

            historico.HasIndex(h => new { h.OrdemServicoId, h.OcorridoEm })
                .HasDatabaseName("ix_ordem_servico_historico_ordem_data");
        });

        builder.Navigation("_historico")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
