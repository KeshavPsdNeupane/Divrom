using System;

/// <summary>
/// A generic abstract base class that implements logical equality based on a single
/// immutable, unique key defined by the derived class.
/// This is used to ensure derived classes (such as graph nodes) function correctly
/// as keys in hash-based collections like <see cref="Dictionary{TKey, TValue}"/>.
/// </summary>
/// <typeparam name="TKey">
/// The type of the unique identifier (for example: <see cref="int"/>, <see cref="string"/>,
/// or a <see cref="ValueTuple"/>).
/// </typeparam>
/// <example>
/// <code>
/// public class ExampleNode : KeyedEquatable&lt;int&gt;
/// {
///     private readonly int id;
///     public ExampleNode(int id)
///     {
///         this.id = id;
///     }
///     protected override int UniqueKey => id;
/// }
/// </code>
/// </example>
public abstract class KeyedEquatable<TKey> : IEquatable<KeyedEquatable<TKey>>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Derived classes must implement this property to return the unique, 
    /// immutable identifier that defines equality for the instance.
    /// </summary>
    protected abstract TKey UniqueKey { get; }

    #region Standard Equality Overrides

    /// <summary>
    /// Implements the required override for object comparison.
    /// </summary>
    public override bool Equals(object obj)
    {
        // 1. Check for null or quick reference match
        if (obj == null) return false;
        if (ReferenceEquals(this, obj)) return true;

        // 2. Safely cast and call the type-safe Equals method
        return obj is KeyedEquatable<TKey> other && Equals(other);
    }

    /// <summary>
    /// Implements the type-safe IEquatable interface.
    /// </summary>
    public bool Equals(KeyedEquatable<TKey> other)
    {
        if (other == null) return false;
        if (ReferenceEquals(this, other)) return true;

        // The core logic: Equality is determined by the equality of the UniqueKey property.
        return UniqueKey.Equals(other.UniqueKey);
    }

    /// <summary>
    /// Implements the required hash code override, ensuring consistency with Equals.
    /// </summary>
    public override int GetHashCode()
    {
        // The hash code is derived ONLY from the UniqueKey.
        return UniqueKey.GetHashCode();
    }

    #endregion

    #region Operator Overloads (Optional but Recommended)

    /// <summary>
    /// Overloads the equality operator (==).
    /// </summary>
    public static bool operator ==(KeyedEquatable<TKey> left, KeyedEquatable<TKey> right)
    {
        // Handles null checks for the left operand
        if (left == null)
        {
            return right == null;
        }
        return left.Equals(right);
    }

    /// <summary>
    /// Overloads the inequality operator (!=).
    /// </summary>
    public static bool operator !=(KeyedEquatable<TKey> left, KeyedEquatable<TKey> right)
    {
        return !(left == right);
    }

    #endregion
}

