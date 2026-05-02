using System;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public class EnumPicker {
		public EnumAsset Source;
		public int SelectedValue;

		/// <summary>
		/// Gets the current instance. 
		/// Throws a Debug.Assert error if the source is missing or the ID was deleted.
		/// </summary>
		public EnumInstance GetInstance() {
			Debug.Assert(Source != null, "[EnumPicker] Source EnumAsset is not assigned!");

			EnumInstance instance = Source != null ? Source.GetInstance(SelectedValue) : null;

			Debug.Assert(instance != null,
				$"[EnumPicker] ID {SelectedValue} is missing in {Source?.name}! " +
				"The value was likely deleted from the asset.");

			return instance;
		}

		public bool IsValid => Source != null && Source.GetInstance(SelectedValue) != null;
	}
}