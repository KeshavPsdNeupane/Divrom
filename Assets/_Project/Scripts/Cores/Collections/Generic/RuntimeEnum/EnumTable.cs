using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	/// <summary>
	/// Maps specific <see cref="EnumAsset"/> entries to generic data values.
	/// Useful for creating lookup tables (e.g., mapping an 'Element' Enum to a 'Color').
	/// </summary>
	[Serializable]
	public sealed class EnumTable<TBinded> {
		public EnumAsset Source => this._source;
		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_source', '_selectedValue', OR '_bindedValues'.
		// 1. REFLECTION: The custom 'EnumTableDrawer' (or Editor) targets these specific names.
		// 2. SERIALIZATION: Renaming these will cause Unity to lose all table data in Prefabs/Scenes.
		// 3. ARCHITECTURE: These arrays are kept parallel for Inspector editing; they are 
		//    transformed into Dictionaries at runtime for O(1) performance.
		// ==========================================================================================

		[SerializeField] private EnumAsset _source;
		[SerializeField] private int[] _selectedValue = Array.Empty<int>();
		[SerializeField] private TBinded[] _bindedValues = Array.Empty<TBinded>();

		private Dictionary<EnumInstance, TBinded> _bindLookup;
		private Dictionary<int, TBinded> _idLookup;

		/// <summary>
		/// Lazy-initializes the internal dictionaries. 
		/// Converts parallel serialized arrays into high-performance lookups.
		/// </summary>
		private void EnsureInitialized() {
			if (this._idLookup != null) return;

			Debug.Assert(_source != null, $"[{nameof(EnumTable<TBinded>)}] Source EnumAsset is null. Table cannot be initialized!");

			this._bindLookup = new Dictionary<EnumInstance, TBinded>();
			this._idLookup = new Dictionary<int, TBinded>();

			// Ensure we don't go out of bounds if arrays are somehow out of sync
			int count = Mathf.Min(this._selectedValue.Length, this._bindedValues.Length);

			for (int i = 0; i < count; i++) {
				int id = this._selectedValue[i];
				EnumInstance instance = this._source.GetInstance(id);

				if (instance != null) {
					TBinded value = this._bindedValues[i];
					this._bindLookup[instance] = value;
					this._idLookup[id] = value;
				}
			}
		}

		/// <summary>
		/// Retrieves the binded value using the persistent Integer ID (Handle).
		/// Recommended for performance and save-file compatibility.
		/// </summary>
		public TBinded Get(int enumId) {
			EnsureInitialized();
			return this._idLookup.TryGetValue(enumId, out TBinded value) ? value : default;
		}

		/// <summary>
		/// Provides access to the full lookup dictionary.
		/// </summary>
		public Dictionary<EnumInstance, TBinded> BindLookup {
			get {
				EnsureInitialized();
				return this._bindLookup;
			}
		}
		public (EnumInstance, TBinded) GetDefaultBinding() {
			if (this._source == null) return default;
			int zeroID = this._source.GetDefaultItemId();
			if (zeroID == default) return default;
			return (this._source.GetInstance(zeroID), Get(zeroID));
		}

		public bool ValidateTheInternal(EnumAsset checkEnum = null, Component callingComponent = null) {
			StringBuilder sb = new();
			if (this._source == null) {
				sb.AppendLine($"[{nameof(EnumTable<TBinded>)}] Validation failed: Source EnumAsset is not assigned.\n");
			}
			for (int i = 0; i < this._selectedValue.Length; i++) {
				int id = this._selectedValue[i];
				if (this._source.GetInstance(id) == null) {
					sb.AppendLine($"[{nameof(EnumTable<TBinded>)}] Validation warning: No EnumInstance" +
					$" with ID '{id}' found in source '{this._source.name}'. This entry will be ignored.");
				}
			}
			if (checkEnum != null && this._source != checkEnum) {
				sb.AppendLine($"[{nameof(EnumTable<TBinded>)}] Validation warning: Source EnumAsset" +
				$" '{this._source.name}' does not match expected '{checkEnum.name}'.");
			}
			if (sb.Length > 0) {
				Debug.LogError(sb.ToString(), callingComponent != null ? callingComponent.gameObject : null);
				return false;
			}
			return true;
		}
	}
}