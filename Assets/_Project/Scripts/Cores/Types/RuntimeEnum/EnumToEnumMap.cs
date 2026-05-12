using System.Collections.Generic;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	/// <summary>
	/// Maps source EnumAsset entries to target EnumAsset entries.
	/// Relation: many-to-one — multiple source keys may share the same target value,
	/// but each source key appears exactly once (dictionary enforces this).
	/// </summary>
	[CreateAssetMenu(fileName = "NewEnumToEnumMap", menuName = "Scriptable Objects/Enum To Enum Map")]
	public class EnumToEnumMap : ScriptableObject, ISerializationCallbackReceiver {
		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_source' OR '_target'.
		// 1. REFLECTION: The custom 'EnumAssetEditor' targets these specific field names.
		// 2. SERIALIZATION: Renaming these will break all existing ScriptableObject (.asset) data.
		// ==========================================================================================

		[SerializeField] private EnumAsset _source;
		[SerializeField] private EnumAsset _target;

		// ── Serialization backing ────────────────────────────────────────────────
		[SerializeField] private List<int> _serializedKeys = new();
		[SerializeField] private List<int> _serializedValues = new();

		// ── Exclusion set ────────────────────────────────────────────────────────
		// Target InternalValues that cannot be mapped to from any source entry.
		// Stored as a list for Unity serialization; rebuilt as a HashSet at runtime.
		[SerializeField] private List<int> _excludedTargets = new();
		private HashSet<int> _excludedSet = new();

		// ── Runtime dictionary ───────────────────────────────────────────────────
		private Dictionary<int, int> _mapping = new();

		public EnumAsset Source => _source;
		public EnumAsset Target => _target;

		// ── Public API ───────────────────────────────────────────────────────────

		public int GetTargetValue(int sourceValue) {
			return this._mapping.TryGetValue(sourceValue, out int targetValue) ? targetValue : 0;
		}

		public EnumInstance GetTargetInstance(int sourceValue) {
			if (this._target == null) return null;
			int targetVal = GetTargetValue(sourceValue);
			return targetVal == 0 ? null : _target.GetInstance(targetVal);
		}

		public void SetMapping(int sourceValue, int targetValue) {
			this._mapping[sourceValue] = targetValue;
		}

		public void RemoveMapping(int sourceValue) {
			this._mapping.Remove(sourceValue);
		}

		public bool IsMapped(int sourceValue) {
			return this._mapping.TryGetValue(sourceValue, out int v) && v != 0;
		}

		public IReadOnlyDictionary<int, int> Mapping => this._mapping;

		// ── Exclusion API ─────────────────────────────────────────────────────────

		/// <summary>Returns true if this target InternalValue is globally excluded from being mapped to.</summary>
		public bool IsExcluded(int targetValue) => this._excludedSet.Contains(targetValue);

		/// <summary>Adds a target InternalValue to the exclusion set.</summary>
		public void AddExclusion(int targetValue) {
			if (this._excludedSet.Add(targetValue))
				this._excludedTargets.Add(targetValue);
		}

		/// <summary>Removes a target InternalValue from the exclusion set.</summary>
		public void RemoveExclusion(int targetValue) {
			if (this._excludedSet.Remove(targetValue))
				this._excludedTargets.Remove(targetValue);
		}

		public IReadOnlyCollection<int> ExcludedTargets => this._excludedSet;

		// ── ISerializationCallbackReceiver ───────────────────────────────────────

		public void OnBeforeSerialize() {
			this._serializedKeys.Clear();
			this._serializedValues.Clear();
			foreach (var kvp in this._mapping) {
				this._serializedKeys.Add(kvp.Key);
				this._serializedValues.Add(kvp.Value);
			}
			// _excludedTargets stays in sync via Add/RemoveExclusion
		}

		public void OnAfterDeserialize() {
			this._mapping = new Dictionary<int, int>();
			int count = Mathf.Min(this._serializedKeys.Count, this._serializedValues.Count);
			for (int i = 0; i < count; i++)
				this._mapping[this._serializedKeys[i]] = this._serializedValues[i];

			this._excludedSet = new HashSet<int>(this._excludedTargets);
		}
	}
}