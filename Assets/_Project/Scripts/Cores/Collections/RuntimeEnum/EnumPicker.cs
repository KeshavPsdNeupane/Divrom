using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	/// <summary>
	/// A serialized reference to a specific entry within a <see cref="EnumAsset"/>.
	/// Acts as the bridge between designer selection in the Inspector and runtime logic.
	/// </summary>
	[System.Serializable]
	public sealed class EnumPicker {

		public EnumAsset Source => this._source;
		public int SelectedValue => this._selectedValue;

		// ==========================================================================================
		// CRITICAL: DO NOT RENAME '_source' OR '_selectedValue'.
		// 1. REFLECTION: The custom 'EnumAssetEditor' (and PropertyDrawers) targets these names.
		// 2. SERIALIZATION: Renaming these will break every existing assignment in the Inspector
		//    across the entire project, resetting selections to null/-1.
		// ==========================================================================================

		[SerializeField] private EnumAsset _source;
		[SerializeField] private int _selectedValue = 0;

		/// <summary>
		/// Resolves the current selection into a functional <see cref="EnumInstance"/>.
		/// </summary>
		/// <returns>The instance containing the Alias and Handle, or null if unassigned.</returns>
		public EnumInstance GetInstance() {
			Debug.Assert(this._source != null && this._selectedValue != 0,
			 $"[EnumPicker] Source EnumAsset is not assigned or no value is selected on Object: {this}");
			var instance = this._source != null ? this._source.GetInstance(this._selectedValue) : null;
			return instance;
		}

		// just inline this 1 line function for efficiency — we call it every frame in some cases,
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetSelectedEnumId() => this.GetInstance().InternalValue;
		/// <summary>
		/// Quick validation check to ensure the picker has a source and a valid selection.
		/// </summary>
		public bool IsValid => this._source != null && this._source.GetInstance(this._selectedValue) != null;


		public void LogAllSourceAsset() {
			if (this._source == null) {
				Debug.LogWarning($"[EnumPicker] No source assigned for EnumPicker on Object");
				return;
			}

			this.Source.LogAllInstances();
		}


		/// <summary>
		/// Performs a validation check on the current selection, logging detailed warnings if issues are found.
		/// This is intended to be called during development (e.g., in OnValidate) to catch misconfigurations early.
		/// If a problem is detected, the selection will be reset to 0 (unassigned) to prevent runtime errors, and a warning will be logged with
		/// context about the issue and the object in question. If the selection is valid, no action is taken.
		/// </summary>
		/// <param name="CallingUnityObject"></param>
		public bool ValidateTheInternal(Object CallingUnityObject = null) {
			StringBuilder sb = new();
			if (this._source == null) {
				sb.AppendLine($"[EnumPicker] Source EnumAsset is not assigned on Object\n");
				this._selectedValue = 0;
			}
			if (this._source != null && this._selectedValue != 0) {
				var instance = this._source.GetInstance(this._selectedValue);
				if (instance == null) {
					sb.AppendLine($"[EnumPicker] Selected value '{this._selectedValue}' does not exist in " +
					$"source '{this._source.name}' on Object");
				}
			}
			if (sb.Length > 0) {
				Debug.LogError(sb.ToString(), CallingUnityObject);
				return false;
			}
			return true;
		}

	}
}