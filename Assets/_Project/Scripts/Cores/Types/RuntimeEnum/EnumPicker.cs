using System;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	/// <summary>
	/// A serialized reference to a specific entry within a <see cref="EnumAsset"/>.
	/// Acts as the bridge between designer selection in the Inspector and runtime logic.
	/// </summary>
	[Serializable]
	public sealed class EnumPicker {

		public EnumAsset Source => _source;
		public int SelectedValue => _selectedValue;

		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_source' OR '_selectedValue'.
		// 1. REFLECTION: The custom 'EnumAssetEditor' (and PropertyDrawers) targets these names.
		// 2. SERIALIZATION: Renaming these will break every existing assignment in the Inspector
		//    across the entire project, resetting selections to null/-1.
		// ==========================================================================================

		[SerializeField] private EnumAsset _source;
		[SerializeField] private int _selectedValue = -1;

		/// <summary>
		/// Resolves the current selection into a functional <see cref="EnumInstance"/>.
		/// </summary>
		/// <returns>The instance containing the Alias and Handle, or null if unassigned.</returns>
		public EnumInstance GetInstance() {
			Debug.Assert(this._source != null && this._selectedValue != -1,
			 $"[EnumPicker] Source EnumAsset is not assigned or no value is selected on Object: {this.ToString()}");

			return this._source != null ? this._source.GetInstance(this._selectedValue) : null;
		}

		/// <summary>
		/// Quick validation check to ensure the picker has a source and a valid selection.
		/// </summary>
		public bool IsValid => this._source != null && this._source.GetInstance(this._selectedValue) != null;
	}
}