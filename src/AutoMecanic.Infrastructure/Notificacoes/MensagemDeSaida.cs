using AutoMecanic.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Notificacoes;

/// <summary>Situação de uma mensagem na caixa de saída.</summary>
public enum SituacaoDaMensagem
{
    /// <summary>Aguardando envio (primeira tentativa ou nova tentativa agendada).</summary>
    Pendente = 1,

    /// <summary>Entregue ao servidor de e-mail.</summary>
    Enviada = 2,

    /// <summary>Esgotou as tentativas. Fica registrada para investigação.</summary>
    Falhou = 3
}

/// <summary>
/// Linha da caixa de saída (<i>outbox</i>). Modelo de persistência, não de domínio: existe para
/// tornar o envio de e-mail confiável, e não para representar um conceito do negócio.
/// </summary>
public sealed class MensagemDeSaida
{
    public Guid Id { get; set; }

    public string Destinatario { get; set; } = string.Empty;

    public string NomeDoDestinatario { get; set; } = string.Empty;

    public string Assunto { get; set; } = string.Empty;

    public string CorpoHtml { get; set; } = string.Empty;

    public string CorpoTexto { get; set; } = string.Empty;

    /// <summary>Origem da mensagem (número da OS).</summary>
    public string Referencia { get; set; } = string.Empty;

    public SituacaoDaMensagem Situacao { get; set; } = SituacaoDaMensagem.Pendente;

    public int Tentativas { get; set; }

    public DateTimeOffset CriadaEm { get; set; }

    /// <summary>A partir de quando a mensagem pode ser (re)enviada.</summary>
    public DateTimeOffset ProximaTentativaEm { get; set; }

    public DateTimeOffset? EnviadaEm { get; set; }

    public string? UltimoErro { get; set; }

    public static MensagemDeSaida De(EmailDeSaida email, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(email);

        return new MensagemDeSaida
        {
            Id = Guid.CreateVersion7(),
            Destinatario = email.Destinatario,
            NomeDoDestinatario = email.NomeDoDestinatario,
            Assunto = email.Assunto,
            CorpoHtml = email.CorpoHtml,
            CorpoTexto = email.CorpoTexto,
            Referencia = email.Referencia,
            CriadaEm = agora,
            ProximaTentativaEm = agora
        };
    }

    public EmailDeSaida ParaEmail() =>
        new(Destinatario, NomeDoDestinatario, Assunto, CorpoHtml, CorpoTexto, Referencia);
}

/// <summary>Mapeamento da caixa de saída.</summary>
internal sealed class MensagemDeSaidaConfiguration : IEntityTypeConfiguration<MensagemDeSaida>
{
    public void Configure(EntityTypeBuilder<MensagemDeSaida> builder)
    {
        builder.ToTable("mensagem_saida", tabela =>
            tabela.HasComment("Caixa de saída de e-mails (outbox): gravada na transação da mudança, enviada depois."));

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Destinatario).HasMaxLength(254).IsRequired();
        builder.Property(m => m.NomeDoDestinatario).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Assunto).HasMaxLength(300).IsRequired();
        // Corpos sem limite: a convenção global de 500 caracteres vale para campos de cadastro,
        // não para o conteúdo de um e-mail.
        builder.Property(m => m.CorpoHtml).HasColumnType("text").IsRequired().Metadata.SetMaxLength(null);
        builder.Property(m => m.CorpoTexto).HasColumnType("text").IsRequired().Metadata.SetMaxLength(null);
        builder.Property(m => m.Referencia).HasMaxLength(40).IsRequired();
        builder.Property(m => m.Situacao).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.UltimoErro).HasMaxLength(1000);

        // Atende à busca do processador: pendentes, já liberadas para tentativa, mais antigas primeiro.
        builder.HasIndex(m => new { m.Situacao, m.ProximaTentativaEm })
            .HasDatabaseName("ix_mensagem_saida_situacao_proxima_tentativa_em");

        builder.HasIndex(m => m.Referencia).HasDatabaseName("ix_mensagem_saida_referencia");
    }
}
