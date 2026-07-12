using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Kope.Core.Collections.Editor {
	/// <summary>
	/// Nestable adaptive Inspector drawer for SerializableDictionary&lt;,&gt;.
	/// Dynamically switches between single-line and multi-line layouts based on TValue size
	/// and safely clears list caches to handle nesting during reorder actions.
	/// </summary>
	[CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
	public class SerializableDictionaryDrawer : PropertyDrawer {
		private const float ROW_SPACING = 4f;
		private const float ELEMENT_PADDING = 4f;
		private const float COL_GAP = 6f;
		private const float MIN_KEY_WIDTH = 80f;
		private const float DEFAULT_KEY_RATIO = 0.35f;
		private const float STACK_THRESHOLD = 2.5f;

		// Cache per-path to handle multiple dictionaries or deeply nested dictionaries correctly
		private readonly Dictionary<string, ReorderableList> _lists = new();

		private ReorderableList GetList(SerializedProperty property, SerializedProperty keysProp, SerializedProperty valuesProp) {
			string path = property.propertyPath;

			// Validate that the cached list matches the current target object to avoid domain-reload leaks
			if (_lists.TryGetValue(path, out var cached) && cached.serializedProperty.serializedObject == property.serializedObject)
				return cached;

			return _lists[path] = BuildList(property, keysProp, valuesProp);
		}

		private static bool IsStacked(float valueHeight) => valueHeight > EditorGUIUtility.singleLineHeight * STACK_THRESHOLD;

		private static float CalculateKeyWidth(float totalWidth) {
			return Mathf.Max(MIN_KEY_WIDTH, totalWidth * DEFAULT_KEY_RATIO);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);

			SerializedProperty keysProp = property.FindPropertyRelative("keys");
			SerializedProperty valuesProp = property.FindPropertyRelative("values");

			if (keysProp == null || valuesProp == null) {
				EditorGUI.LabelField(position, label.text, "keys/values fields not found — is this a SerializableDictionary<,>?");
				EditorGUI.EndProperty();
				return;
			}

			HashSet<int> conflicts = FindConflictedIndices(property);
			ReorderableList list = GetList(property, keysProp, valuesProp);

			// Freshly assign dynamic closures so they capture the correct element paths this frame
			list.drawElementCallback = MakeDrawElementCallback(keysProp, valuesProp, conflicts, property);
			list.elementHeightCallback = MakeElementHeightCallback(keysProp, valuesProp);

			list.DoList(position);

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			SerializedProperty keysProp = property.FindPropertyRelative("keys");
			SerializedProperty valuesProp = property.FindPropertyRelative("values");

			if (keysProp == null || valuesProp == null)
				return EditorGUIUtility.singleLineHeight;

			ReorderableList list = GetList(property, keysProp, valuesProp);
			return list.GetHeight();
		}

		private static ReorderableList.ElementHeightCallbackDelegate MakeElementHeightCallback(SerializedProperty keysProp, SerializedProperty valuesProp) {
			return index => {
				if (index >= keysProp.arraySize || index >= valuesProp.arraySize)
					return EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;

				SerializedProperty valProp = valuesProp.GetArrayElementAtIndex(index);
				float valHeight = EditorGUI.GetPropertyHeight(valProp, true);

				if (IsStacked(valHeight)) {
					float keyHeight = EditorGUI.GetPropertyHeight(keysProp.GetArrayElementAtIndex(index), true);
					return keyHeight + valHeight + ROW_SPACING + ELEMENT_PADDING;
				}

				float maxSingleLineH = Mathf.Max(EditorGUI.GetPropertyHeight(keysProp.GetArrayElementAtIndex(index), true), valHeight);
				return maxSingleLineH + ELEMENT_PADDING;
			};
		}

		private static ReorderableList.ElementCallbackDelegate MakeDrawElementCallback(
			SerializedProperty keysProp, SerializedProperty valuesProp, HashSet<int> conflicts, SerializedProperty dictProperty) {
			return (rect, index, active, focused) => {
				if (index >= keysProp.arraySize || index >= valuesProp.arraySize)
					return;

				SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(index);
				SerializedProperty valProp = valuesProp.GetArrayElementAtIndex(index);

				rect.y += 2f;
				rect.height -= ELEMENT_PADDING;

				if (conflicts.Contains(index)) {
					Rect bg = rect;
					bg.x -= 2; bg.width += 4;
					EditorGUI.DrawRect(bg, new Color(0.75f, 0.2f, 0.2f, 0.2f));
				}

				float keyHeight = EditorGUI.GetPropertyHeight(keyProp, true);
				float valHeight = EditorGUI.GetPropertyHeight(valProp, true);

				EditorGUI.BeginChangeCheck();

				if (IsStacked(valHeight)) {
					// --- MULTI-LINE STACKED LAYOUT ---
					Rect keyRect = new(rect.x, rect.y, rect.width, keyHeight);
					EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);

					Rect valRect = new(rect.x, rect.y + keyHeight + ROW_SPACING, rect.width, valHeight);
					GUIContent valLabel = valProp.hasChildren ? new GUIContent("Value") : GUIContent.none;
					EditorGUI.PropertyField(valRect, valProp, valLabel, true);
				} else {
					// --- SIDE-BY-SIDE LAYOUT ---
					float kw = CalculateKeyWidth(rect.width);
					Rect keyRect = new(rect.x, rect.y, kw, keyHeight);
					EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);

					float valX = rect.x + kw + COL_GAP;
					float valW = rect.width - kw - COL_GAP;

					// Match your nested layout helper: indent slightly if it has generic layout dropdown arrows
					if (valProp.hasChildren && valProp.propertyType == SerializedPropertyType.Generic) {
						float indent = 12f;
						valX += indent;
						valW -= indent;
					}

					Rect valRect = new(valX, rect.y, valW, valHeight);
					EditorGUI.PropertyField(valRect, valProp, GUIContent.none, true);
				}

				if (EditorGUI.EndChangeCheck()) {
					dictProperty.serializedObject.ApplyModifiedProperties();
				}
			};
		}

		private ReorderableList BuildList(SerializedProperty dictProperty, SerializedProperty keysProp, SerializedProperty valuesProp) {
			var list = new ReorderableList(dictProperty.serializedObject, keysProp, true, true, true, true);

			list.drawHeaderCallback = rect => {
				string text = $"{dictProperty.displayName} ({keysProp.arraySize})";
				EditorGUI.LabelField(rect, text, EditorStyles.boldLabel);
			};

			// FIX: Lock step array sizing to prevent mismatch frames
			list.onAddCallback = l => {
				int newSize = keysProp.arraySize + 1;

				// Force sizes to match simultaneously
				keysProp.arraySize = newSize;
				valuesProp.arraySize = newSize;

				// Optional: Clean up default initialization values if needed
				SerializedProperty newKey = keysProp.GetArrayElementAtIndex(newSize - 1);
				SerializedProperty newVal = valuesProp.GetArrayElementAtIndex(newSize - 1);

				// Reset the value to 0f for floats to prevent copying previous elements
				if (newVal.propertyType == SerializedPropertyType.Float) {
					newVal.floatValue = 0f;
				}

				dictProperty.serializedObject.ApplyModifiedProperties();
			};

			// FIX: Lock step removal 
			list.onRemoveCallback = l => {
				int index = l.index >= 0 ? l.index : keysProp.arraySize - 1;
				if (index < 0) return;

				// Double delete handles ObjectReferences correctly if values are components/assets
				if (index < valuesProp.arraySize) {
					if (valuesProp.GetArrayElementAtIndex(index).propertyType == SerializedPropertyType.ObjectReference) {
						valuesProp.DeleteArrayElementAtIndex(index);
					}
					valuesProp.DeleteArrayElementAtIndex(index);
				}

				keysProp.DeleteArrayElementAtIndex(index);

				// Safety fallback: Force absolute size symmetry
				if (keysProp.arraySize != valuesProp.arraySize) {
					valuesProp.arraySize = keysProp.arraySize;
				}

				dictProperty.serializedObject.ApplyModifiedProperties();
			};

			list.onReorderCallbackWithDetails = (_, oldIndex, newIndex) => {
				if (oldIndex < valuesProp.arraySize && newIndex < valuesProp.arraySize) {
					valuesProp.MoveArrayElement(oldIndex, newIndex);
				}
				dictProperty.serializedObject.ApplyModifiedProperties();
				_lists.Clear();
			};

			return list;
		}

		// ── Reflection Path Utilities ─────────────────────────────────────────────

		private static HashSet<int> FindConflictedIndices(SerializedProperty dictProperty) {
			var conflicted = new HashSet<int>();
			object target = GetTargetObjectOfProperty(dictProperty);
			if (target == null) return conflicted;

			FieldInfo keysField = target.GetType().GetField("keys", BindingFlags.NonPublic | BindingFlags.Instance);
			if (keysField?.GetValue(target) is not IList runtimeKeys) return conflicted;

			var firstSeenAt = new Dictionary<object, int>(new BoxedEqualityComparer());
			for (int i = 0; i < runtimeKeys.Count; i++) {
				object key = runtimeKeys[i];
				if (key == null) {
					conflicted.Add(i);
					continue;
				}
				if (firstSeenAt.TryGetValue(key, out int firstIndex)) {
					conflicted.Add(firstIndex);
					conflicted.Add(i);
				} else {
					firstSeenAt[key] = i;
				}
			}
			return conflicted;
		}

		private sealed class BoxedEqualityComparer : IEqualityComparer<object> {
			bool IEqualityComparer<object>.Equals(object x, object y) => x == null ? y == null : x.Equals(y);
			int IEqualityComparer<object>.GetHashCode(object obj) => obj?.GetHashCode() ?? 0;
		}

		private static object GetTargetObjectOfProperty(SerializedProperty prop) {
			string path = prop.propertyPath.Replace(".Array.data[", "[");
			object obj = prop.serializedObject.targetObject;
			string[] elements = path.Split('.');

			foreach (string element in elements) {
				if (element.Contains("[")) {
					string elementName = element.Substring(0, element.IndexOf("["));
					int index = Convert.ToInt32(element.Substring(element.IndexOf("["))
						.Replace("[", "").Replace("]", ""));
					obj = GetIndexedValue(obj, elementName, index);
				} else {
					obj = GetFieldOrPropertyValue(obj, element);
				}
				if (obj == null) return null;
			}
			return obj;
		}

		private static object GetFieldOrPropertyValue(object source, string name) {
			if (source == null) return null;
			System.Type type = source.GetType();
			while (type != null) {
				FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
				if (field != null) return field.GetValue(source);

				PropertyInfo prop = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
				if (prop != null) return prop.GetValue(source, null);
				type = type.BaseType;
			}
			return null;
		}

		private static object GetIndexedValue(object source, string name, int index) {
			if (GetFieldOrPropertyValue(source, name) is not IEnumerable enumerable) return null;
			IEnumerator enumerator = enumerable.GetEnumerator();
			for (int i = 0; i <= index; i++) {
				if (!enumerator.MoveNext()) return null;
			}
			return enumerator.Current;
		}
	}
}