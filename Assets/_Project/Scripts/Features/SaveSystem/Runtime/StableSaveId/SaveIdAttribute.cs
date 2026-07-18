using System;

namespace Kope.SaveSystem.Attributes {
	/// <summary>
	/// Declares a canonical SaveId for a component type. This is the "root" of a save-id family.
	/// A class carrying this attribute directly is always the declaring type for that id,
	/// regardless of whether subclasses inherit it via <see cref="InheritSaveIdAttribute"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class SaveComponentAttribute : Attribute {
		public string Id { get; }

		public SaveComponentAttribute(string id) {
			if (string.IsNullOrWhiteSpace(id)) {
				throw new ArgumentException("SaveComponent id cannot be null or whitespace.", nameof(id));
			}
			this.Id = id;
		}
	}
	/// <summary>
	/// No id of its own - walks up the base-type chain looking for the nearest
	/// SaveComponentAttribute and adopts that id. Marker only.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class InheritSaveIdAttribute : Attribute {
		/// <summary>
		/// How many base-type steps up to search for a declaring SaveComponentAttribute.
		/// 1 = only the direct base class. 2 = base, then base's base. Etc.
		/// Defaults to int.MaxValue (search all the way up to object).
		/// </summary>
		public int SearchNParent { get; }
		public const int DefaultSearchNParent = 1000;
		public InheritSaveIdAttribute(int searchNParent = DefaultSearchNParent) {
			if (searchNParent < 1) {
				throw new ArgumentOutOfRangeException(nameof(searchNParent), "SearchNParent must be at least 1.");
			}
			this.SearchNParent = searchNParent;
		}
	}
	/// <summary>
	/// Declares a stable SaveId for an ISaveData payload type. Data payloads are never
	/// inherited - every concrete ISaveData needs its own explicit, unique id. There is
	/// no "InheritSaveId" counterpart for data on purpose: unlike components (which are
	/// located via hierarchy/ECS path and only need Type -> id in the inherited case),
	/// data types are reconstructed FROM the id string alone during deserialization, so
	/// an inherited/ambiguous id here would be a genuine correctness bug, not just an
	/// API inconvenience.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class SaveComponentDataAttribute : Attribute {
		public string Id { get; }

		public SaveComponentDataAttribute(string id) {
			if (string.IsNullOrWhiteSpace(id)) {
				throw new ArgumentException("SaveComponentData id cannot be null or whitespace.", nameof(id));
			}
			this.Id = id;
		}
	}
}