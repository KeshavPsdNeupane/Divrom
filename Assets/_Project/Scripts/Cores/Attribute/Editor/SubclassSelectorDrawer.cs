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

			string fullTypeName = property.managedReferenceFullTypename;

			// --- Missing Type Validation Logic ---
			bool isMissing = !string.IsNullOrEmpty(fullTypeName) && GetTypeFromManagedReference(fullTypeName) == null;

			if (isMissing) {
				Color previousColor = GUI.color;
				GUI.color = new Color(1f, 0.4f, 0.4f); // Noticeable soft red
				if (EditorGUI.DropdownButton(buttonRect, new GUIContent($"MISSING: {ExtractTypeName(fullTypeName)}"), FocusType.Keyboard)) {
					ShowTypeMenu(property, fieldType);
				}
				GUI.color = previousColor;
			} else {
				string displayTypeName = string.IsNullOrEmpty(fullTypeName)
					? "Null (Empty)"
					: ExtractTypeName(fullTypeName);

				if (EditorGUI.DropdownButton(buttonRect, new GUIContent(displayTypeName), FocusType.Keyboard)) {
					ShowTypeMenu(property, fieldType);
				}
			}

			// Draw children if expanded
			if (property.isExpanded && !string.IsNullOrEmpty(fullTypeName) && !isMissing) {
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
			// If missing or null, just show the header
			if (!property.isExpanded || string.IsNullOrEmpty(property.managedReferenceFullTypename))
				return EditorGUIUtility.singleLineHeight;

			// If type is missing, we can't draw children anyway
			if (GetTypeFromManagedReference(property.managedReferenceFullTypename) == null)
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
			Type type = fieldInfo.FieldType;
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
				return type.GetGenericArguments()[0];
			}
			if (type.IsArray) {
				return type.GetElementType();
			}
			return type;
		}

		private string ExtractTypeName(string fullTypeName) {
			if (string.IsNullOrEmpty(fullTypeName)) return "Null";
			return fullTypeName.Split(' ').Last().Split('.').Last();
		}

		private Type GetTypeFromManagedReference(string fullTypeName) {
			if (string.IsNullOrEmpty(fullTypeName)) return null;

			var parts = fullTypeName.Split(' ');
			if (parts.Length < 2) return Type.GetType(fullTypeName);

			var assemblyName = parts[0];
			var className = parts[1];

			// Combines class and assembly so Type.GetType can find it
			return Type.GetType($"{className}, {assemblyName}");
		}
	}
}