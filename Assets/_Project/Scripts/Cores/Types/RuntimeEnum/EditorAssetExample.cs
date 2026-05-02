using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Core {
	public class EditorAssetExample : MonoBehaviour {
		public EnumPicker DamageType;

		void Start() {
			// to cleck for the assert error, try changing the value in the EnumAsset or
			//  setting it to null, then run the scene
			DealDamage();
		}
		public void DealDamage() {
			var info = DamageType.GetInstance();
			if (info != null) {
				Debug.Log($"Dealing {info.Name} damage!");
			} else {
				// This acts as your "Compiler Error" at Runtime
				Debug.LogError("DamageType is invalid! Was the value changed in the EnumAsset?");
			}
		}
	}
}
