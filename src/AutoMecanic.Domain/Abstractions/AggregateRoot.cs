namespace AutoMecanic.Domain.Abstractions;

/// <summary>
/// Marcador para a <b>Raiz de Agregado</b> (Aggregate Root). É o único ponto de entrada
/// permitido para alterar o estado de um agregado, e a fronteira de consistência
/// transacional: tudo dentro do agregado é salvo em uma única transação.
/// </summary>
public interface IAggregateRoot
{
    Guid Id { get; }

    IReadOnlyCollection<IDomainEvent> EventosDeDominio { get; }

    void LimparEventos();
}

/// <summary>
/// Base das raízes de agregado. Além da identidade herdada de <see cref="Entity"/>,
/// acumula os <b>Eventos de Domínio</b> ocorridos durante a operação corrente, que são
/// publicados pela Unidade de Trabalho no momento do commit.
/// </summary>
public abstract class AggregateRoot : Entity, IAggregateRoot
{
    private readonly List<IDomainEvent> _eventosDeDominio = [];

    protected AggregateRoot(Guid id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Controle de concorrência otimista. Mapeado para <c>xmin</c> no PostgreSQL, evitando
    /// que duas alterações concorrentes na mesma Ordem de Serviço se sobrescrevam.
    /// </summary>
    public uint Versao { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> EventosDeDominio => _eventosDeDominio.AsReadOnly();

    protected void RegistrarEvento(IDomainEvent evento) => _eventosDeDominio.Add(evento);

    public void LimparEventos() => _eventosDeDominio.Clear();
}
