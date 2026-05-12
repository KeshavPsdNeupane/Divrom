using System;
using System.Collections.Generic;
using ZLinq;
using UnityEngine;
using Kope.Core.Attribute;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public sealed class EnumInstance {
		public string Alias => this._name;
		public long InternalValue => _value;

		[SerializeField] private string _name;
		[SerializeField] private long _value;

		public EnumInstance(string name, long value) {
			this._name = name;
			this._value = value;
		}

		public override string ToString() => this._name;
	}

	[CreateAssetMenu(fileName = "NewEnumAsset", menuName = "Kope/Enum Asset")]
	public class EnumAsset : ScriptableObject, ISerializationCallbackReceiver {
		// ========================================================================================
		// ID LAYOUT: [Asset ID (int32)] * 1,000,000,000 + [Local ID]
		// This ensures the IDs are visually differentiable: 123456000000007
		// ========================================================================================

		public const long MASK_MULTIPLIER = 1_000_000_000L;

		[ReadOnly, SerializeField] private int _enumAssetId = 0;

		public List<EnumInstance> Instances = new();
		private readonly Dictionary<long, EnumInstance> _cache = new();

		private void OnEnable() {
#if UNITY_EDITOR
			var path = UnityEditor.AssetDatabase.GetAssetPath(this);
			if (!string.IsNullOrEmpty(path)) {
				string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);

				// Use absolute hash to keep the prefix positive and readable
				int derivedId = Math.Abs(guid.GetHashCode());

				if (derivedId != this._enumAssetId) {
					this._enumAssetId = derivedId;
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
#endif
		}

		public void AddNewInstance() {
			int nextSuffix = 0;
			if (this.Instances.Count > 0) {
				long maxCurrentValue = this.Instances.AsValueEnumerable().Max(i => i.InternalValue);
				nextSuffix = (int)(maxCurrentValue % MASK_MULTIPLIER) + 1;
			}

			long combinedId = ((long)this._enumAssetId * MASK_MULTIPLIER) + nextSuffix;
			this.Instances.Add(new EnumInstance("New Entry", combinedId));
		}

		public EnumInstance GetInstance(long value) =>
			this._cache.TryGetValue(value, out var instance) ? instance : null;

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			this._cache.Clear();
			foreach (var inst in this.Instances) {
				if (inst != null && !this._cache.ContainsKey(inst.InternalValue))
					this._cache.Add(inst.InternalValue, inst);
			}
		}

		public long GetDefaultItemId() {
			if (this.Instances.Count == 0) return default;
			long defaultHandle = this._enumAssetId * MASK_MULTIPLIER;
			var idle = GetInstance(defaultHandle);
			return idle != null ? idle.InternalValue : Instances[0].InternalValue;
		}

		public void LogAllInstances() {
			foreach (var instance in this.Instances) {
				Debug.Log($"Enum Instance - Name: {instance.Alias}, Value: {instance.InternalValue}");
			}
		}
	}
}