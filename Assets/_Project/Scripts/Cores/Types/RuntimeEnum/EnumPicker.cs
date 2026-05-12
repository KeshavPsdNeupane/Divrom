using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	/// <summary>
	/// A serialized reference to a specific entry within a <see cref="EnumAsset"/>.
	/// Acts as the bridge between designer selection in the Inspector and runtime logic.
	/// </summary>
	[Serializable]
	public sealed class EnumPicker {

		public EnumAsset Source => _source;
		public long SelectedValue => _selectedValue;

		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_source' OR '_selectedValue'.
		// 1. REFLECTION: The custom 'EnumAssetEditor' (and PropertyDrawers) targets these names.
		// 2. SERIALIZATION: Changed _selectedValue to long to support 64-bit bitwise IDs.
		//    Renaming these will break every existing assignment in the Inspector.
		// ==========================================================================================

		[SerializeField] private EnumAsset _source;
		[SerializeField] private long _selectedValue = 0;

		/// <summary>
		/// Resolves the current selection into a functional <see cref="EnumInstance"/>.
		/// </summary>
		/// <returns>The instance containing the Alias and Handle, or null if unassigned.</returns>
		public EnumInstance GetInstance() {
			// Check for null source or default long value (0)
			if (this._source == null || this._selectedValue == 0) {
				return null;
			}

			return this._source.GetInstance(this._selectedValue);
		}

		/// <summary>
		/// Gets the raw internal long value for comparisons or storage.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long GetSelectedEnumId() => _selectedValue;

		/// <summary>
		/// Quick validation check to ensure the picker has a source and a valid selection.
		/// </summary>
		public bool IsValid => this._source != null && this._source.GetInstance(this._selectedValue) != null;

		public void LogAllSourceAsset() {
			if (this._source == null) {
				Debug.LogWarning($"[EnumPicker] No source assigned for EnumPicker on Object: {this}");
				return;
			}
		}
	}
}