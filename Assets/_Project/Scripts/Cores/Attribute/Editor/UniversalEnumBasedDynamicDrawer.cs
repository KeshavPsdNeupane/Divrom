using System.Reflection;
using UnityEngine;
using Kope.Core.Attribute.DataStructure;
using Kope.Core.Attribute;
using UnityEditor;
namespace Kope.Core.Attributes.Editor {
	using System;

	[CustomPropertyDrawer(typeof(DynamicSelection<,>), true)]
	public class UniversalEnumBasedDynamicDrawer : PropertyDrawer {
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);

			SerializedProperty typeProp = property.FindPropertyRelative("selectedType");
			Rect enumRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			EditorGUI.PropertyField(enumRect, typeProp, label);

			string selectedEnumName = typeProp.enumNames[typeProp.enumValueIndex];
			FieldInfo targetField = GetBoundField(property, selectedEnumName);

			if (targetField != null) {
				SerializedProperty dataProp = property.FindPropertyRelative(targetField.Name);
				if (dataProp != null) {
					float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
					float dataHeight = EditorGUI.GetPropertyHeight(dataProp, true);
					Rect dataRect = new(position.x, position.y + yOffset, position.width, dataHeight);
					EditorGUI.PropertyField(dataRect, dataProp, new GUIContent("Settings"), true);
				}
			}

			EditorGUI.EndProperty();
		}

		private FieldInfo GetBoundField(SerializedProperty property, string enumName) {
			Type containerType = fieldInfo.FieldType;

			if (containerType.IsGenericType && containerType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
				containerType = containerType.GetGenericArguments()[0];
			else if (containerType.IsArray)
				containerType = containerType.GetElementType();

			var fields = containerType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			foreach (var f in fields) {
				var attr = (BindToEnumAttribute)Attribute.GetCustomAttribute(f, typeof(BindToEnumAttribute));
				if (attr != null && attr.EnumValue.ToString() == enumName) return f;
			}
			return null;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			SerializedProperty typeProp = property.FindPropertyRelative("selectedType");
			string selectedEnumName = typeProp.enumNames[typeProp.enumValueIndex];

			FieldInfo targetField = GetBoundField(property, selectedEnumName);
			float h = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

			if (targetField != null) {
				var dataProp = property.FindPropertyRelative(targetField.Name);
				h += EditorGUI.GetPropertyHeight(dataProp, true);
			}
			return h;
		}
	}
}