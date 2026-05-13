using System;
using System.Collections.Generic;
using ZLinq;
using UnityEngine;
using Kope.Core.Attribute;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public sealed class EnumInstance {
		/// <summary>
		/// HUMAN-READABLE IDENTIFIER
		/// Used for Animator hashing and debugging.
		/// </summary>
		public string Alias => this._name;

		/// <summary>
		/// PERSISTENT DATA HANDLE
		/// Used for save files and O(1) dictionary lookups.
		/// </summary>
		public int InternalValue => _value;

		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_name' OR '_value'.
		// 1. REFLECTION: The custom 'EnumAssetEditor' targets these specific field names.
		// 2. SERIALIZATION: Renaming these will break all existing ScriptableObject (.asset) data.
		// 3. OPTIMIZATION: int32 is used over smaller types (byte/short) to maintain alignment
		//    simplicity, as entry counts are typically low enough that memory gains are negligible.
		// ==========================================================================================

		[SerializeField] private string _name;
		[SerializeField] private int _value;

		public EnumInstance(string name, int value) {
			this._name = name;
			this._value = value;
		}

		public override string ToString() => this._name;
	}

	[CreateAssetMenu(fileName = "NewEnumAsset", menuName = "Kope/Enum Asset")]
	public class EnumAsset : ScriptableObject, ISerializationCallbackReceiver {
		// ========================================================================================
		// ID LAYOUT: [AAAAAA][LLLL]  (Up to 10 digits total, fits in signed int32)
		//   A = Asset prefix  (range 10000–214747, derived from asset GUID)
		//   L = Local suffix  (range 0–9999, sequential per instance)
		//
		// Example: assetId=123456, localId=7 → fullId = 1234560007
		//
		// TOTAL CAPACITY:
		// Assets: 204,748 unique prefixes (214747 - 10000)
		// Instances: 10,000 local IDs per asset
		//
		// SAFETY CHECK:
		// Max prefix (214747) * Multiplier (10000) + Max suffix (9999) = 2,147,479,999
		// int32.MaxValue = 2,147,483,647
		// Result: 2,147,479,999 < 2,147,483,647 (✓ SAFE with 3,648 margin)
		//
		// GUID HASHING:
		// Prefix is derived from the asset's Unity GUID via FNV-1a hash.
		// Duplicating an asset (Ctrl+D) creates a new GUID, forcing a new prefix 
		// automatically without needing an O(n) collision scan.
		// ========================================================================================

		/// <summary>Splits the full ID into asset prefix and local suffix.</summary>
		[HideInInspector] public const int MASK_MULTIPLIER = 10000;

		/// <summary>Minimum asset prefix. Guarantees 5+ digit prefixes for visual consistency.</summary>
		[HideInInspector] public const int MIN_ASSET_PREFIX = 10000;

		/// <summary>
		/// Maximum safe asset prefix.
		/// 214748 * 10000 + 9999 = 2,147,489,999 which is below int32 max (2,147,483,647).
		/// </summary>
		[HideInInspector] public const int MAX_ASSET_PREFIX = 214748;

		[ReadOnly, SerializeField] private int _enumAssetId = 0;
		[SerializeField, HideInInspector] private bool _isManualId = false;

		public List<EnumInstance> Instances = new();
		private readonly Dictionary<int, EnumInstance> _cache = new();

		/// <summary>
		/// Derives a stable asset prefix from the asset's Unity GUID.
		/// Since Ctrl+D produces a new .meta file with a new GUID, the derived prefix
		/// automatically diverges on the duplicate — no collision scan required.
		/// Only runs in the editor; builds use whatever was serialized to disk.
		/// </summary>
		private void OnEnable() {
#if UNITY_EDITOR
			if (this._isManualId) return;

			var path = UnityEditor.AssetDatabase.GetAssetPath(this);
			if (!string.IsNullOrEmpty(path)) {
				string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
				int derivedId = GuidToAssetPrefix(guid);
				if (derivedId != this._enumAssetId) {
					this._enumAssetId = derivedId;
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
#endif
		}

#if UNITY_EDITOR
		/// <summary>
		/// FNV-1a hash of the asset GUID, folded into [MIN_ASSET_PREFIX, MAX_ASSET_PREFIX].
		/// Deterministic: same GUID always produces the same prefix. Safe across renames
		/// and moves since Unity preserves the GUID through those operations.
		/// </summary>
		private static int GuidToAssetPrefix(string guid) {
			return FnvHash.ComputeInRange(guid, MIN_ASSET_PREFIX, MAX_ASSET_PREFIX);
		}

		public void ManualUpdateId(int newPrefix) {
			this._enumAssetId = newPrefix;
			this._isManualId = true;

			for (int i = 0; i < Instances.Count; i++) {
				int localId = Instances[i].InternalValue % MASK_MULTIPLIER;
				int newValue = (newPrefix * MASK_MULTIPLIER) + localId;

				var field = typeof(EnumInstance).GetField("_value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				field.SetValue(Instances[i], newValue);
			}
		}
		public void RevertToAutomaticId() {
			this._isManualId = false;
			var path = UnityEditor.AssetDatabase.GetAssetPath(this);
			if (!string.IsNullOrEmpty(path)) {
				string guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
				int derivedId = GuidToAssetPrefix(guid);
				ManualUpdateId(derivedId);
				this._isManualId = false;
			}
		}
#endif

		public void AddNewInstance() {
			int nextSuffix = 0;
			if (this.Instances.Count > 0) {
				// Get the highest current suffix by stripping the prefix using modulo
				int maxCurrentValue = this.Instances.AsValueEnumerable().Max(i => i.InternalValue);
				nextSuffix = (maxCurrentValue % MASK_MULTIPLIER) + 1;
			}

			if (nextSuffix >= MASK_MULTIPLIER) {
				Debug.LogError($"[EnumAsset] Asset '{this.name}' (ID: {this._enumAssetId}) has exceeded the max instance count of {MASK_MULTIPLIER - 1}!");
				return;
			}

			// Combine: Prefix * 10,000 + Suffix (e.g., 1234560007)
			int combinedId = (this._enumAssetId * MASK_MULTIPLIER) + nextSuffix;
			this.Instances.Add(new EnumInstance("New Entry", combinedId));
		}

		public EnumInstance GetInstance(int value) {
			return this._cache.TryGetValue(value, out var instance) ? instance : null;
		}

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			this._cache.Clear();
			foreach (var inst in this.Instances) {
				if (inst != null && !this._cache.ContainsKey(inst.InternalValue))
					this._cache.Add(inst.InternalValue, inst);
			}
		}

		public int GetDefaultItemId() {
			if (Instances.Count == 0) return default;

			var idle = GetInstance(this._enumAssetId * MASK_MULTIPLIER); ;
			if (idle != null) return idle.InternalValue;

			// since the count is already checked so,
			// this will not cause any issue because the first item will be returned if the idle is not found.
			return Instances[0].InternalValue;
		}
		public void LogAllInstances() {
			Debug.Log($"[EnumAsset] Logging all instances for asset: {this.name} (ID: {this._enumAssetId})");
			foreach (var instance in this.Instances) {
				Debug.Log($"- Instance Alias: {instance.Alias}, ID: {instance.InternalValue}");
			}
		}



	}
}