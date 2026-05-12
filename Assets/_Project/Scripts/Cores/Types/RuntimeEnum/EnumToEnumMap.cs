using System.Collections.Generic;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	[CreateAssetMenu(fileName = "NewEnumToEnumMap", menuName = "Scriptable Objects/Enum To Enum Map")]
	public class EnumToEnumMap : ScriptableObject, ISerializationCallbackReceiver {

		[SerializeField] private EnumAsset _source;
		[SerializeField] private EnumAsset _target;

		[SerializeField] private List<long> _serializedKeys = new();
		[SerializeField] private List<long> _serializedValues = new();

		[SerializeField] private List<long> _excludedTargets = new();
		private HashSet<long> _excludedSet = new();

		private Dictionary<long, long> _mapping = new();

		public EnumAsset Source => _source;
		public EnumAsset Target => _target;


		public long GetTargetValue(long sourceValue) {
			return this._mapping.TryGetValue(sourceValue, out long targetValue) ? targetValue : 0;
		}

		public EnumInstance GetTargetInstance(long sourceValue) {
			if (this._target == null) return null;
			long targetVal = GetTargetValue(sourceValue);
			return targetVal == 0 ? null : _target.GetInstance(targetVal);
		}

		public void SetMapping(long sourceValue, long targetValue) {
			this._mapping[sourceValue] = targetValue;
		}

		public void RemoveMapping(long sourceValue) {
			this._mapping.Remove(sourceValue);
		}

		public bool IsMapped(long sourceValue) {
			return this._mapping.TryGetValue(sourceValue, out long v) && v != 0;
		}

		public IReadOnlyDictionary<long, long> Mapping => this._mapping;

		public bool IsExcluded(long targetValue) => this._excludedSet.Contains(targetValue);

		public void AddExclusion(long targetValue) {
			if (this._excludedSet.Add(targetValue))
				this._excludedTargets.Add(targetValue);
		}

		public void RemoveExclusion(long targetValue) {
			if (this._excludedSet.Remove(targetValue))
				this._excludedTargets.Remove(targetValue);
		}

		public IReadOnlyCollection<long> ExcludedTargets => this._excludedSet;

		// ── Serialization ───────────────────────────────────────────────────

		public void OnBeforeSerialize() {
			this._serializedKeys.Clear();
			this._serializedValues.Clear();
			foreach (var kvp in this._mapping) {
				this._serializedKeys.Add(kvp.Key);
				this._serializedValues.Add(kvp.Value);
			}
		}

		public void OnAfterDeserialize() {
			this._mapping = new Dictionary<long, long>();
			int count = Mathf.Min(this._serializedKeys.Count, this._serializedValues.Count);
			for (int i = 0; i < count; i++)
				this._mapping[this._serializedKeys[i]] = this._serializedValues[i];

			this._excludedSet = new HashSet<long>(this._excludedTargets);
		}
	}
}