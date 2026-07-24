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

[CustomPropertyDrawer(typeof(ReadOnlyTextAreaAttribute))]
public class ReadOnlyTextAreaDrawer : PropertyDrawer {
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		ReadOnlyTextAreaAttribute attr = (ReadOnlyTextAreaAttribute)attribute;

		GUIStyle style = new(EditorStyles.textArea) {
			wordWrap = true
		};

		EditorGUI.BeginProperty(position, label, property);

		GUI.enabled = false;
		property.stringValue = EditorGUI.TextArea(position, property.stringValue, style);
		GUI.enabled = true;

		EditorGUI.EndProperty();
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
		ReadOnlyTextAreaAttribute attr = (ReadOnlyTextAreaAttribute)attribute;

		GUIStyle style = new(EditorStyles.textArea) {
			wordWrap = true
		};

		float lineHeight = EditorGUIUtility.singleLineHeight;
		float minHeight = lineHeight * attr.minLines;
		float maxHeight = lineHeight * attr.maxLines;

		// Measure content height at current inspector width
		float width = EditorGUIUtility.currentViewWidth - 40f; // rough padding allowance
		float contentHeight = style.CalcHeight(new GUIContent(property.stringValue), width);

		return Mathf.Clamp(contentHeight, minHeight, maxHeight);
	}

}



[CustomPropertyDrawer(typeof(MessageAttribute))]
public class MessageDrawer : DecoratorDrawer {
	private const float FallbackWidth = 300f;
	private float _lastKnownWidth = FallbackWidth;

	private GUIStyle CachedStyle() {
		return new GUIStyle(EditorStyles.textArea) {
			wordWrap = true
		};
	}

	public override void OnGUI(Rect position) {
		MessageAttribute attr = (MessageAttribute)attribute;
		GUIStyle style = CachedStyle();

		_lastKnownWidth = position.width; // safe here, we're inside OnGUI

		GUI.enabled = false;
		EditorGUI.TextArea(position, attr.text, style);
		GUI.enabled = true;
	}

	public override float GetHeight() {
		MessageAttribute attr = (MessageAttribute)attribute;
		GUIStyle style = CachedStyle();

		float lineHeight = EditorGUIUtility.singleLineHeight;
		float minHeight = lineHeight * attr.minLines;
		float maxHeight = lineHeight * attr.maxLines;

		// Do NOT call EditorGUIUtility.currentViewWidth here — not safe outside OnGUI.
		float contentHeight = style.CalcHeight(new GUIContent(attr.text), _lastKnownWidth);

		return Mathf.Clamp(contentHeight, minHeight, maxHeight) + 4f;
	}
}
#endif

