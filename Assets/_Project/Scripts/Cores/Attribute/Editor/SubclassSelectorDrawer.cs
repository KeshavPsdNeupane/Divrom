using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kope.Core.Attributes.Editor {
	[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
	public class SubclassSelectorDrawer : PropertyDrawer {
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			if (property.propertyType != SerializedPropertyType.ManagedReference) {
				EditorGUI.LabelField(position, label.text, "Use [SerializeReference] with this.");
				return;
			}

			EditorGUI.BeginProperty(position, label, property);

			var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

			// 1. Improved Type Extraction
			Type fieldType = GetTargetType(property);

			// Draw the Type Picker Dropdown
			Rect buttonRect = EditorGUI.PrefixLabel(labelRect, label);

			// Get clean type name for display
			string fullTypeName = property.managedReferenceFullTypename;
			string displayTypeName = string.IsNullOrEmpty(fullTypeName)
				? "Null (Empty)"
				: fullTypeName.Split(' ').Last().Split('.').Last();

			if (EditorGUI.DropdownButton(buttonRect, new GUIContent(displayTypeName), FocusType.Keyboard)) {
				ShowTypeMenu(property, fieldType);
			}

			// Draw children if expanded
			if (property.isExpanded && !string.IsNullOrEmpty(fullTypeName)) {
				EditorGUI.indentLevel++;
				float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

				SerializedProperty iterator = property.Copy();
				SerializedProperty endProperty = iterator.GetEndProperty();

				if (iterator.NextVisible(true)) {
					do {
						if (SerializedProperty.EqualContents(iterator, endProperty)) break;
						float height = EditorGUI.GetPropertyHeight(iterator, true);
						Rect childRect = new Rect(position.x, position.y + yOffset, position.width, height);
						EditorGUI.PropertyField(childRect, iterator, true);
						yOffset += height + EditorGUIUtility.standardVerticalSpacing;
					} while (iterator.NextVisible(false));
				}
				EditorGUI.indentLevel--;
			}

			property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded, GUIContent.none, true);
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			if (!property.isExpanded || string.IsNullOrEmpty(property.managedReferenceFullTypename))
				return EditorGUIUtility.singleLineHeight;

			float height = EditorGUIUtility.singleLineHeight;
			SerializedProperty iterator = property.Copy();
			SerializedProperty endProperty = iterator.GetEndProperty();
			if (iterator.NextVisible(true)) {
				do {
					if (SerializedProperty.EqualContents(iterator, endProperty)) break;
					height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
				} while (iterator.NextVisible(false));
			}
			return height;
		}

		private void ShowTypeMenu(SerializedProperty property, Type targetType) {
			GenericMenu menu = new GenericMenu();
			menu.AddItem(new GUIContent("Null"), false, () => {
				property.serializedObject.Update();
				property.managedReferenceValue = null;
				property.serializedObject.ApplyModifiedProperties();
			});

			// Using TypeCache is much faster than Manual Assembly Scanning
			var types = TypeCache.GetTypesDerivedFrom(targetType)
				.Where(t => !t.IsAbstract && !t.IsInterface && t.IsSerializable);

			foreach (var type in types) {
				menu.AddItem(new GUIContent(type.Name), false, () => {
					property.serializedObject.Update();
					property.managedReferenceValue = Activator.CreateInstance(type);
					property.serializedObject.ApplyModifiedProperties();
				});
			}
			menu.ShowAsContext();
		}

		private Type GetTargetType(SerializedProperty property) {
			// This handles the "List<T>" case by looking at the fieldInfo directly
			// which Unity provides via the PropertyDrawer
			Type type = fieldInfo.FieldType;

			// If the field is a List or Array, we want the element type
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
				return type.GetGenericArguments()[0];
			}
			if (type.IsArray) {
				return type.GetElementType();
			}

			return type;
		}
	}
}