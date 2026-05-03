using System;
using UnityEngine;

namespace Kope.Core.Type.EnumAsset {
	[Serializable]
	public class EnumPicker {
		public EnumAsset Source;
		public int SelectedValue = -1;

		public EnumInstance GetInstance() {
			Debug.Assert(Source != null && SelectedValue != -1,
			 "[EnumPicker] Source EnumAsset is not assigned or no value is selected!");

			EnumInstance instance = Source != null ? Source.GetInstance(SelectedValue) : null;
			return instance;
		}

		public bool IsValid => Source != null && Source.GetInstance(SelectedValue) != null;
	}
}