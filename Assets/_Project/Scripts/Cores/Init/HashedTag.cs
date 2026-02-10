using System;


public interface IHashTagProvider
{
    HashedTag HashedTag { get; }
}

public readonly struct HashedTag : IEquatable<HashedTag>
{
    private readonly string tag;
    private readonly int tagHash;

    public HashedTag(string tag)
    {
        this.tag = tag ?? throw new ArgumentNullException(nameof(tag));
        this.tagHash = CreateHash(tag);
    }

    public bool Equals(HashedTag other)
    {
        return tagHash == other.tagHash && tag == other.tag;
    }

    public override bool Equals(object obj)
        => obj is HashedTag other && Equals(other);

    public static bool operator ==(HashedTag a, HashedTag b) => a.Equals(b);
    public static bool operator !=(HashedTag a, HashedTag b) => !a.Equals(b);

    public override int GetHashCode() => tagHash;

    private static int CreateHash(string tag)
    {
        unchecked
        {
            const int fnvPrime = 16777619;
            int hash = (int)2166136261;

            for (int i = 0; i < tag.Length; i++)
                hash = (hash ^ tag[i]) * fnvPrime;

            return hash;
        }
    }


}


