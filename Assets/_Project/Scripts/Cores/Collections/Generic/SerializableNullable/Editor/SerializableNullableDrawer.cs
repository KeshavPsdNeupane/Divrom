using UnityEditor;
using UnityEngine;

namespace Kope.Core.Type.Generic.Editor {

	/// Draws SerializableNullable<T> as a single value field + toggle instead of
	/// the default two-line _value/_hasValue layout. Toggle disables the field
	/// (doesn't hide it) so the last serialized value stays visible while off.
	[CustomPropertyDrawer(typeof(SerializableNullable<>))]
	public sealed class SerializableNullableDrawer : PropertyDrawer {
		private const string VALUE_FIELD_NAME = "_value";

		private const string hasValueFieldName = "_hasValue";
		private const float TOGGLE_WIDTH = 16f;
		private const float TOGGLE_GAP = 4f;
		private const float STRIP_WIDTH = 2f;
		private const float STRIP_GAP = 3f;
		private static readonly Color ActiveStrip = new(0.35f, 0.75f, 0.45f, 0.9f);
		private static readonly Color InactiveStrip = new(0.5f, 0.5f, 0.5f, 0.35f);

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			SerializedProperty valueProp = property.FindPropertyRelative(VALUE_FIELD_NAME);
			// Must match live height even while disabled, or foldout children snap/jump on toggle.
			return EditorGUI.GetPropertyHeight(valueProp, label, true);
		}
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			SerializedProperty valueProp = property.FindPropertyRelative(VALUE_FIELD_NAME);
			SerializedProperty hasValueProp = property.FindPropertyRelative(hasValueFieldName);

			EditorGUI.BeginProperty(position, label, property);

			float lineHeight = EditorGUIUtility.singleLineHeight;
			bool hasValue = hasValueProp.boolValue;

			Rect stripRect = new(position.x, position.y, STRIP_WIDTH, lineHeight);
			Rect valueRect = new(
				position.x + STRIP_WIDTH + STRIP_GAP,
				position.y,
				position.width - STRIP_WIDTH - STRIP_GAP - TOGGLE_WIDTH - TOGGLE_GAP,
				position.height);
			Rect toggleRect = new(position.xMax - TOGGLE_WIDTH, position.y, TOGGLE_WIDTH, lineHeight);

			EditorGUI.DrawRect(stripRect, hasValue ? ActiveStrip : InactiveStrip);

			using (new EditorGUI.DisabledScope(!hasValue)) {
				EditorGUI.PropertyField(valueRect, valueProp, label, true);
			}

			EditorGUI.BeginChangeCheck();
			bool toggled = EditorGUI.Toggle(toggleRect, new GUIContent("", "Has Value"), hasValue);
			if (EditorGUI.EndChangeCheck()) {
				hasValueProp.boolValue = toggled;
				// _value is left untouched on toggle-off; matches GetValueOrDefault semantics
				// since the field re-enables with whatever was last set rather than resetting.
			}

			EditorGUI.EndProperty();
		}
	}
}