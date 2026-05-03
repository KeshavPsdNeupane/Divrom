using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomPropertyDrawer(typeof(EnumPicker))]
	public class EnumPickerDrawer : PropertyDrawer {
		// this must be same as the field names in EnumPicker class
		private const string SOURCE_PROPERTY_NAME = "Source";
		// this must be same as the field names in EnumPicker class
		private const string VALUE_PROPERTY_NAME = "SelectedValue";

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);

			// getting references to the Source and Value properties
			// from the EnumPicker class using the defined constant names
			var sourceProp = property.FindPropertyRelative(SOURCE_PROPERTY_NAME);
			var valueProp = property.FindPropertyRelative(VALUE_PROPERTY_NAME);

			// Define Layout Rects
			float labelWidth = EditorGUIUtility.labelWidth;
			Rect labelRect = new(position.x, position.y, labelWidth, position.height);
			Rect contentRect = new(position.x + labelWidth, position.y, position.width - labelWidth, position.height);

			float sourceWidth = contentRect.width * 0.4f;
			Rect soRect = new(contentRect.x, contentRect.y, sourceWidth, position.height);
			Rect popupRect = new(soRect.xMax + 5, position.y, contentRect.width - sourceWidth - 5, position.height);

			EditorGUI.LabelField(labelRect, label);

			// --- 1. Draw Source Field & Detect Changes ---
			EditorGUI.BeginChangeCheck();

			EditorGUI.PropertyField(soRect, sourceProp, GUIContent.none);
			if (EditorGUI.EndChangeCheck()) {
				// If the Source was changed/assigned, default to the first available index
				EnumAsset newAsset = sourceProp.objectReferenceValue as EnumAsset;
				if (newAsset != null && newAsset.Instances.Count > 0) {
					valueProp.intValue = newAsset.Instances[0].Value;
				} else {
					valueProp.intValue = -1; // Reset if asset is null/empty
				}
			}

			// --- 2. Draw the Popup ---
			EnumAsset asset = sourceProp.objectReferenceValue as EnumAsset;
			if (asset != null && asset.Instances.Count > 0) {
				string[] names = asset.Instances.Select(i => i.Name).ToArray();
				int[] values = asset.Instances.Select(i => i.Value).ToArray();

				int currentIndex = System.Array.IndexOf(values, valueProp.intValue);

				// Handle missing ID (from deletion) visually
				if (currentIndex == -1) {
					GUI.backgroundColor = Color.red;
					if (GUI.Button(popupRect, $"MISSING ID: {valueProp.intValue}", EditorStyles.miniButton)) {
						valueProp.intValue = values[0];
					}
					GUI.backgroundColor = Color.white;
				} else {
					int newIndex = EditorGUI.Popup(popupRect, currentIndex, names);
					valueProp.intValue = values[newIndex];
				}
			} else {
				// Disabled state if no asset is assigned
				GUI.enabled = false;
				EditorGUI.Popup(popupRect, 0, new string[] { "Assign EnumAsset..." });
				GUI.enabled = true;
			}

			EditorGUI.EndProperty();
		}
	}
}