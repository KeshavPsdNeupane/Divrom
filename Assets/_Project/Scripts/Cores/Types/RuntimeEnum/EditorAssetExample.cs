using Kope.Core.Type.EnumAsset;
using UnityEngine;
namespace Kope.Core {
	public class EditorAssetExample : MonoBehaviour {
		public EnumPicker DamageType;

		public EnumTable<string> StatusEffects;


		// the nested part works too, used for testing the drawer's handling of nested tables and 
		// potential issues with list caching, But now they are resolved and the drawer should be
		// robust to nesting depth in general. Still not a recommended use of the system, just for testing purposes.
		// the only use if 0 layer of nesting, just enum and a value, is already a bit of an overkill
		// for most use cases, but it can be useful for more complex data binding scenarios.
		[Tooltip("This is a nested status effects table" +
		"Used for stress test of the system in a nested structure ")]
		public EnumTable<EnumTable<string>> NestedStatusEffects;
		void Start() {
			// to cleck for the assert error, try changing the value in the EnumAsset or
			//  setting it to null, then run the scene
			DealDamage();
		}
		public void DealDamage() {
			var info = DamageType.GetInstance();
			if (info != null) {
				Debug.Log($"Dealing {info.Alias} damage!");
			}
			foreach (var kvp in StatusEffects.BindLookup) {
				Debug.Log($"Status Enum {kvp.Key} has binded value: {kvp.Value}");
			}

			foreach (var kvp in NestedStatusEffects.BindLookup) {
				foreach (var nestedKvp in kvp.Value.BindLookup) {
					Debug.Log($"Nested Status Enum {kvp.Key}, nested " +
					$"key {nestedKvp.Key} has binded value: {nestedKvp.Value}");
				}
			}


		}
	}
}