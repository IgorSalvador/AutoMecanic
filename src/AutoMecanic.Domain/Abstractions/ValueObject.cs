namespace AutoMecanic.Domain.Abstractions;

/// <summary>
/// Base do <b>Objeto de Valor</b> (Value Object): não possui identidade própria, é imutável
/// e a igualdade é estrutural — dois objetos com os mesmos atributos são o mesmo valor.
/// Objetos de valor concentram as regras de formação e validação de conceitos do negócio
/// (CPF/CNPJ, placa, dinheiro), impedindo que estados inválidos sejam representáveis.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Componentes que definem a igualdade estrutural do valor.</summary>
    protected abstract IEnumerable<object?> ObterComponentesDeIgualdade();

    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        return ObterComponentesDeIgualdade().SequenceEqual(other.ObterComponentesDeIgualdade());
    }

    public override bool Equals(object? obj) => obj is ValueObject outro && Equals(outro);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var componente in ObterComponentesDeIgualdade())
        {
            hash.Add(componente);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? esquerda, ValueObject? direita) =>
        esquerda is null ? direita is null : esquerda.Equals(direita);

    public static bool operator !=(ValueObject? esquerda, ValueObject? direita) => !(esquerda == direita);
}
