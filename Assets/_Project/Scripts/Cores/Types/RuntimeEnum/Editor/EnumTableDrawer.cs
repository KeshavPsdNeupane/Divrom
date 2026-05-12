using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System.Linq;
using System.Collections.Generic;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomPropertyDrawer(typeof(EnumTable<>), true)]
	public class EnumTableDrawer : PropertyDrawer {

		private const string SOURCE_PROP = "_source";
		private const string KEYS_PROP = "_selectedValue";
		private const string VALUES_PROP = "_bindedValues";

		private const float MIN_KEY_WIDTH = 80f;
		private const float DEFAULT_KEY_RATIO = 0.4f;
		private const float STACK_THRESHOLD = 3f;
		private const float HEADER_HEIGHT = 21f;
		private const float FOOTER_HEIGHT = 21f;
		private const float HELP_BOX_GAP = 5f;
		private const float SOURCE_GAP = 10f;
		private const float LIST_GAP = 5f;
		private const float ELEMENT_PADDING = 4f;
		private const float COL_GAP = 5f;
		private const int BOTTOM_EXTRA_PADDING = 10;

		private static readonly GUIContent _helpContent = new(
			"Duplicate enum keys will be ignored — only the first mapping per key is used.");

		private readonly Dictionary<string, ReorderableList> _lists = new();

		private ReorderableList GetList(SerializedProperty keys, SerializedProperty values, string path) {
			if (this._lists.TryGetValue(path, out var cached) &&
				cached.serializedProperty.serializedObject == keys.serializedObject)
				return cached;

			return this._lists[path] = BuildList(keys, values);
		}

		private void InvalidateList(string path) => this._lists.Remove(path);

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);

			var sourceProp = property.FindPropertyRelative(SOURCE_PROP);
			var keysProp = property.FindPropertyRelative(KEYS_PROP);
			var valuesProp = property.FindPropertyRelative(VALUES_PROP);

			Rect sourceRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

			EditorGUI.BeginChangeCheck();
			EditorGUI.PropertyField(sourceRect, sourceProp, label);
			if (EditorGUI.EndChangeCheck()) {
				keysProp.arraySize = 0;
				valuesProp.arraySize = 0;
				InvalidateList(property.propertyPath);
			}

			if (sourceProp.objectReferenceValue is not EnumAsset asset) {
				EditorGUI.EndProperty();
				return;
			}

			var list = GetList(keysProp, valuesProp, property.propertyPath);
			float listY = position.y + EditorGUIUtility.singleLineHeight + LIST_GAP;

			list.drawElementCallback = MakeDrawElement(keysProp, valuesProp, sourceProp);
			list.elementHeightCallback = MakeElementHeight(valuesProp);

			list.DoList(new Rect(position.x, listY, position.width, list.GetHeight()));

			float helpH = GetHelpBoxHeight(position.width);
			EditorGUI.HelpBox(
				new Rect(position.x, listY + list.GetHeight() + HELP_BOX_GAP, position.width, helpH),
				_helpContent.text, MessageType.Info);

			EditorGUI.EndProperty();
		}

		private static ReorderableList.ElementCallbackDelegate MakeDrawElement(
				SerializedProperty keys, SerializedProperty values, SerializedProperty sourceProp) {
			return (rect, index, _, _) => {
				// Ensure values array is kept in sync with keys array
				if (values.arraySize <= index) values.arraySize = keys.arraySize;

				if (sourceProp.objectReferenceValue is not EnumAsset asset) return;

				var keyProp = keys.GetArrayElementAtIndex(index);
				var valProp = values.GetArrayElementAtIndex(index);
				float valHeight = EditorGUI.GetPropertyHeight(valProp, true);
				float sl = EditorGUIUtility.singleLineHeight;

				Rect keyRect, valRect;
				if (IsStacked(valHeight)) {
					keyRect = new Rect(rect.x, rect.y + 2, rect.width, sl);
					valRect = new Rect(rect.x, rect.y + 2 + sl + COL_GAP, rect.width, valHeight);
				} else {
					float kw = KeyWidth(rect.width, valHeight);
					keyRect = new Rect(rect.x, rect.y + 2, kw, sl);
					float valX = rect.x + kw + COL_GAP;
					float valW = rect.width - kw - COL_GAP;

					if (valProp.hasChildren && valProp.propertyType == SerializedPropertyType.Generic) {
						float indent = 15f;
						valX += indent;
						valW -= indent;
					}
					valRect = new Rect(valX, rect.y + 2, valW, valHeight);
				}

				string[] names = asset.Instances.Select(i => i.Alias).ToArray();
				long[] ids = asset.Instances.Select(i => i.InternalValue).ToArray();

				int currIdx = -1;
				long currentLong = keyProp.longValue;
				for (int i = 0; i < ids.Length; i++) {
					if (ids[i] == currentLong) {
						currIdx = i;
						break;
					}
				}

				bool missing = currIdx == -1;
				GUI.backgroundColor = missing ? new Color(1f, 0.4f, 0.4f) : Color.white;

				if (missing) {
					var display = names.Prepend($"! MISSING (ID: {currentLong})").ToArray();
					int next = EditorGUI.Popup(keyRect, 0, display);
					if (next > 0) keyProp.longValue = ids[next - 1];
				} else {
					int nextIdx = EditorGUI.Popup(keyRect, currIdx, names);
					keyProp.longValue = ids[nextIdx];
				}
				GUI.backgroundColor = Color.white;

				EditorGUI.PropertyField(valRect, valProp, GUIContent.none, true);
			};
		}

		private ReorderableList BuildList(SerializedProperty keys, SerializedProperty values) {
			return new ReorderableList(keys.serializedObject, keys, true, true, true, true) {
				drawHeaderCallback = rect => {
					float kw = rect.width * DEFAULT_KEY_RATIO;
					EditorGUI.LabelField(new Rect(rect.x, rect.y, kw, rect.height), "Enum Key", EditorStyles.miniBoldLabel);
					EditorGUI.LabelField(new Rect(rect.x + kw + COL_GAP, rect.y, rect.width - kw, rect.height), "Binded Value", EditorStyles.miniBoldLabel);
				},
				onAddCallback = l => {
					// 1. Add the elements
					int newIdx = l.serializedProperty.arraySize;
					l.serializedProperty.arraySize++;
					values.arraySize = l.serializedProperty.arraySize;
					string listPath = l.serializedProperty.propertyPath;
					SerializedProperty parentProp = l.serializedProperty.serializedObject.FindProperty(
						listPath[..listPath.LastIndexOf('.')]
					);

					if (parentProp != null) {
						var sourceProp = parentProp.FindPropertyRelative(SOURCE_PROP);
						var asset = sourceProp?.objectReferenceValue as EnumAsset;

						if (asset != null && asset.Instances.Count > 0) {
							l.serializedProperty.GetArrayElementAtIndex(newIdx).longValue = asset.GetDefaultItemId();
						}
					}
				},
				onRemoveCallback = l => {
					int index = l.index;
					if (index >= 0 && index < values.arraySize) {
						// Double delete to handle object references and array resizing
						if (values.GetArrayElementAtIndex(index).propertyType == SerializedPropertyType.ObjectReference) {
							values.DeleteArrayElementAtIndex(index);
						}
						values.DeleteArrayElementAtIndex(index);
					}
					ReorderableList.defaultBehaviours.DoRemoveButton(l);
				},
				onReorderCallbackWithDetails = (_, oldIndex, newIndex) => {
					values.MoveArrayElement(oldIndex, newIndex);
				}
			};
		}

		// --- Helper Methods ---
		private static bool IsStacked(float h) => h > EditorGUIUtility.singleLineHeight * STACK_THRESHOLD;
		private static float KeyWidth(float total, float h) => Mathf.Max(MIN_KEY_WIDTH, total * (DEFAULT_KEY_RATIO - Mathf.Max(0, (h / EditorGUIUtility.singleLineHeight) - 1) * 0.04f));
		private static float ElementHeight(float h) => IsStacked(h) ? EditorGUIUtility.singleLineHeight + COL_GAP + h + ELEMENT_PADDING : Mathf.Max(EditorGUIUtility.singleLineHeight, h) + ELEMENT_PADDING;
		private static float GetHelpBoxHeight(float width) => EditorStyles.helpBox.CalcHeight(_helpContent, width);
		private static ReorderableList.ElementHeightCallbackDelegate MakeElementHeight(SerializedProperty values) => i => (i < values.arraySize) ? ElementHeight(EditorGUI.GetPropertyHeight(values.GetArrayElementAtIndex(i), true)) : EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			var sourceProp = property.FindPropertyRelative(SOURCE_PROP);
			var keysProp = property.FindPropertyRelative(KEYS_PROP);
			var valuesProp = property.FindPropertyRelative(VALUES_PROP);
			float h = EditorGUIUtility.singleLineHeight + SOURCE_GAP;
			if (sourceProp.objectReferenceValue == null) return h + 25f + BOTTOM_EXTRA_PADDING;
			int count = keysProp.arraySize;
			float elemsH = count == 0 ? EditorGUIUtility.singleLineHeight + ELEMENT_PADDING : Enumerable.Range(0, count).Sum(i => ElementHeight(i < valuesProp.arraySize ? EditorGUI.GetPropertyHeight(valuesProp.GetArrayElementAtIndex(i), true) : EditorGUIUtility.singleLineHeight));
			return h + HEADER_HEIGHT + elemsH + FOOTER_HEIGHT + HELP_BOX_GAP + GetHelpBoxHeight(EditorGUIUtility.currentViewWidth) + BOTTOM_EXTRA_PADDING;
		}
	}
}