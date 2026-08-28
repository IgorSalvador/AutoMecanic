using AutoMecanic.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoMecanic.Infrastructure.Persistencia.Configuracoes;

/// <summary>Configurações repetidas em todas as raízes de agregado.</summary>
internal static class ConfiguracaoComum
{
    /// <summary>
    /// Liga o controle de concorrência otimista à coluna de sistema <c>xmin</c> do PostgreSQL,
    /// que o próprio banco incrementa a cada UPDATE.
    /// <para>
    /// Sem isso, dois atendentes editando a mesma Ordem de Serviço simultaneamente fariam a
    /// última gravação sobrescrever silenciosamente a primeira. Com isso, a segunda gravação
    /// falha com <c>DbUpdateConcurrencyException</c>, traduzida pela API para <c>409</c>.
    /// </para>
    /// </summary>
    public static void ConfigurarConcorrencia<TAgregado>(EntityTypeBuilder<TAgregado> builder)
        where TAgregado : AggregateRoot
    {
        builder.Property(a => a.Versao)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }

    /// <summary>
    /// Eventos de domínio vivem apenas em memória, entre a alteração do agregado e o commit.
    /// Não são persistidos: são despachados pela Unidade de Trabalho e descartados.
    /// </summary>
    public static void IgnorarEventosDeDominio<TAgregado>(EntityTypeBuilder<TAgregado> builder)
        where TAgregado : AggregateRoot =>
        builder.Ignore(a => a.EventosDeDominio);
}
