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
		//    simplicity, as entry counts are typically low enough that memory gains are negligible.
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
		// ID LAYOUT: [AAAAAA][LLLL]  (10 digits total, fits comfortably in int32)
		//   A = Asset prefix  (6 digits, range 10000–214748, derived from asset GUID)
		//   L = Local suffix  (4 digits, range 0–9999, sequential per instance)
		//
		// Example: assetId=123456, localId=7 → fullId = 1234560007
		//
		// Total addressable IDs: ~204,748 assets × 9,999 instances ≈ 2.04 billion
		// int32 max:             2,147,483,647  ✓ safe (214748 * 10000 + 9999 = 2,147,489,999)
		//
		// Ctrl+D resistance: prefix is derived from the asset's Unity GUID via FNV-1a hash.
		// Duplicating an asset produces a new .meta file with a new GUID, so the derived
		// prefix automatically diverges — no O(n) collision scan required.
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
	}
}