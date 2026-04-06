using System;
using System.ComponentModel;
using Newtonsoft.Json;

public interface IHashTagProvider {
	HashedTag HashedTag { get; }
}
/// <summary>
/// A struct that represents a hashed version of a string tag, providing efficient equality checks and hash code generation.
/// <para>
/// The HashedTag struct is designed to be immutable and value-based, making it suitable for use as a key in dictionaries or for comparisons.
/// It uses a custom hash function (FNV-1a) to generate a hash code from the input string, and it implements IEquatable for efficient equality checks.
/// </para>
/// </summary>

[Serializable]
[TypeConverter(typeof(HashedTagTypeConverter))]
public readonly struct HashedTag : IEquatable<HashedTag> {

	[JsonProperty("tag")]
	private readonly string tag;

	[JsonIgnore]
	private readonly int tagHash;

	[JsonConstructor]
	public HashedTag(string tag) {
		this.tag = tag ?? string.Empty;
		this.tagHash = CreateHash(this.tag);
	}


	public bool Equals(HashedTag other) {
		return tagHash == other.tagHash && tag == other.tag;
	}

	public override bool Equals(object obj)
		=> obj is HashedTag other && Equals(other);

	public static bool operator ==(HashedTag a, HashedTag b) => a.Equals(b);
	public static bool operator !=(HashedTag a, HashedTag b) => !a.Equals(b);

	public override int GetHashCode() => tagHash;

	public override string ToString() => tag;

	private static int CreateHash(string tag) {
		unchecked {
			const int fnvPrime = 16777619;
			int hash = (int)2166136261;

			for (int i = 0; i < tag.Length; i++)
				hash = (hash ^ tag[i]) * fnvPrime;

			return hash;
		}
	}


}


