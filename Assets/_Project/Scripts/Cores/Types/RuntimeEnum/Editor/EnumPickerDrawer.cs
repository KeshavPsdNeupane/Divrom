using UnityEngine;
using UnityEditor;
using ZLinq;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	// UI for the Picker when it appears on other scripts
	[CustomPropertyDrawer(typeof(EnumPicker))]
	public class EnumPickerDrawer : PropertyDrawer {
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);

			var sourceProp = property.FindPropertyRelative("Source");
			var valueProp = property.FindPropertyRelative("SelectedValue");

			Rect labelRect = new(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
			Rect soRect = new(position.x + EditorGUIUtility.labelWidth, position.y, (position.width - EditorGUIUtility.labelWidth) * 0.4f, position.height);
			Rect popupRect = new(soRect.xMax + 5, position.y, position.width - soRect.xMax - 5, position.height);

			EditorGUI.LabelField(labelRect, label);
			EditorGUI.PropertyField(soRect, sourceProp, GUIContent.none);

			EnumAsset asset = sourceProp.objectReferenceValue as EnumAsset;
			if (asset != null && asset.Instances.Count > 0) {
				string[] names = asset.Instances.AsValueEnumerable().Select(i => i.Name).ToArray();
				int[] values = asset.Instances.AsValueEnumerable().Select(i => i.Value).ToArray();
				int currentIndex = System.Array.IndexOf(values, valueProp.intValue);

				if (currentIndex == -1) {
					GUI.backgroundColor = Color.red;
					if (GUI.Button(popupRect, $"ID {valueProp.intValue} MISSING", EditorStyles.miniButton)) {
						valueProp.intValue = values[0]; // Reset to first available
					}
				} else {
					int newIndex = EditorGUI.Popup(popupRect, currentIndex, names);
					valueProp.intValue = values[newIndex];
				}
				GUI.backgroundColor = Color.white;
			}

			EditorGUI.EndProperty();
		}
	}
}