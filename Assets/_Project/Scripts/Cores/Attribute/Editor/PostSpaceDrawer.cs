using UnityEditor;
using UnityEngine;

namespace Kope.Core.Attribute.Editor {
	[CustomPropertyDrawer(typeof(PostSpaceAttribute))]
	public class PostSpaceDrawer : PropertyDrawer {
		private PostSpaceAttribute postSpaceAttribute => (PostSpaceAttribute)attribute;

		// Tells Unity how much vertical space to allocate for this property
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			// Total height = Standard height of the field + custom bottom space
			return EditorGUI.GetPropertyHeight(property, label, true) + postSpaceAttribute.spaceHeight;
		}

		// Draws the actual field
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			// Subtract the extra space from the height so the field doesn't look stretched
			position.height -= postSpaceAttribute.spaceHeight;

			// Draw the property normally
			EditorGUI.PropertyField(position, property, label, true);
		}
	}
}