namespace AutoMecanic.Domain.Abstractions;

/// <summary>
/// Base de toda <b>Entidade</b> do domínio: objeto cuja identidade é definida por um
/// identificador estável, e não pelo conjunto de seus atributos. Duas entidades com o
/// mesmo <see cref="Id"/> são a mesma entidade, ainda que seus atributos difiram.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    /// <summary>Identidade da entidade. Imutável após a criação.</summary>
    public Guid Id { get; protected set; }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("O identificador de uma entidade não pode ser vazio.", nameof(id));
        }

        Id = id;
    }

    /// <summary>Construtor sem parâmetros exigido pelo ORM para materialização.</summary>
    protected Entity()
    {
    }

    /// <summary>
    /// Gera um novo identificador. Utiliza UUID v7 (ordenável por tempo), o que reduz
    /// fragmentação de índice no PostgreSQL em comparação com UUID v4 aleatório.
    /// </summary>
    public static Guid NovoId() => Guid.CreateVersion7();

    public bool Equals(Entity? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return GetType() == other.GetType() && Id == other.Id && Id != Guid.Empty;
    }

    public override bool Equals(object? obj) => obj is Entity outra && Equals(outra);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? esquerda, Entity? direita) =>
        esquerda is null ? direita is null : esquerda.Equals(direita);

    public static bool operator !=(Entity? esquerda, Entity? direita) => !(esquerda == direita);
}
