using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	/// <summary>
	/// Maps specific <see cref="EnumAsset"/> entries to generic data values.
	/// Updated to use long for the new bitwise ID system.
	/// </summary>
	[Serializable]
	public sealed class EnumTable<TBinded> {
		public EnumAsset Source => this._source;

		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_source', '_selectedValue', OR '_bindedValues'.
		// 1. REFLECTION: The custom 'EnumTableDrawer' targets these specific names.
		// 2. SERIALIZATION: _selectedValue is now long[] to match the 64-bit InternalValue.
		// ==========================================================================================

		[SerializeField] private EnumAsset _source;
		[SerializeField] private long[] _selectedValue = Array.Empty<long>();
		[SerializeField] private TBinded[] _bindedValues = Array.Empty<TBinded>();

		private Dictionary<EnumInstance, TBinded> _bindLookup;
		private Dictionary<long, TBinded> _idLookup;

		/// <summary>
		/// Lazy-initializes the internal dictionaries using bitwise long IDs.
		/// </summary>
		private void EnsureInitialized() {
			if (this._idLookup != null) return;

			Debug.Assert(_source != null, $"[{nameof(EnumTable<TBinded>)}] Source EnumAsset is null.");

			this._bindLookup = new Dictionary<EnumInstance, TBinded>();
			this._idLookup = new Dictionary<long, TBinded>();

			int count = Mathf.Min(this._selectedValue.Length, this._bindedValues.Length);

			for (int i = 0; i < count; i++) {
				long id = this._selectedValue[i];
				EnumInstance instance = this._source.GetInstance(id);

				if (instance != null) {
					TBinded value = this._bindedValues[i];
					this._bindLookup[instance] = value;
					this._idLookup[id] = value;
				}
			}
		}

		/// <summary>
		/// Retrieves the binded value using the 64-bit bitwise ID.
		/// </summary>
		public TBinded Get(long enumId) {
			EnsureInitialized();
			return this._idLookup.TryGetValue(enumId, out TBinded value) ? value : default;
		}

		public Dictionary<EnumInstance, TBinded> BindLookup {
			get {
				EnsureInitialized();
				return this._bindLookup;
			}
		}

		public (EnumInstance instance, TBinded value) GetDefaultBinding() {
			if (this._source == null) return default;
			long defaultID = this._source.GetDefaultItemId();
			if (defaultID == 0) return default;
			return (this._source.GetInstance(defaultID), Get(defaultID));
		}
	}
}