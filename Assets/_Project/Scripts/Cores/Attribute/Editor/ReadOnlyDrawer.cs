#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Kope.Core.Attribute;


/// <summary>
/// Custom property drawer for ReadOnlyAttribute to make fields read-only in the Unity Inspector.
/// </summary>
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer {
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		GUI.enabled = false;
		EditorGUI.PropertyField(position, property, label, true);
		GUI.enabled = true;
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
		return EditorGUI.GetPropertyHeight(property, label, true);
	}
}
#endif
