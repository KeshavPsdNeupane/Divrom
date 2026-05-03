using Kope.Core.Type.EnumAsset;
using UnityEngine;
namespace Kope.Core {
	public class EditorAssetExample : MonoBehaviour {
		public EnumPicker DamageType;

		public EnumTable<int> StatusEffects;
		void Start() {
			// to cleck for the assert error, try changing the value in the EnumAsset or
			//  setting it to null, then run the scene
			DealDamage();
		}
		public void DealDamage() {
			var info = DamageType.GetInstance();
			if (info != null) {
				Debug.Log($"Dealing {info.Name} damage!");
			}
			foreach (var kvp in StatusEffects.BindLookup) {
				Debug.Log($"Status Enum {kvp.Key} has binded value: {kvp.Value}");
			}

		}
	}
}