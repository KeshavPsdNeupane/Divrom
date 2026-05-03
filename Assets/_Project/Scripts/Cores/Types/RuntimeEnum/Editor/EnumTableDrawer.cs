using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System.Linq;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomPropertyDrawer(typeof(EnumTable<>))]
	public class EnumTableDrawer : PropertyDrawer {
		private const string SOURCE_PROP = "Source";
		private const string KEYS_PROP = "SelectedValue";
		private const string VALUES_PROP = "BindedValues";
		private const float BIND_VAL_WIDTH = 0.6f;
		private const float ROW_HEIGHT = 21f;
		private const float HEADER_HEIGHT = 21f;
		private const float FOOTER_HEIGHT = 21f;
		private const float HELP_BOX_GAP = 5f;
		private const float SOURCE_GAP = 10f;
		private const float LIST_GAP = 5f;
		private const float NO_SOURCE_HEIGHT = 25f;

		private static readonly GUIContent _helpContent = new("Duplicate enum keys will be ignored — " +
		"only the first mapping per key is used.");

		private ReorderableList _list;

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
				this._list = null;
			}

			EnumAsset currentAsset = sourceProp.objectReferenceValue as EnumAsset;
			if (currentAsset != null) {
				if (this._list == null || this._list.serializedProperty.serializedObject != property.serializedObject) {
					SetupList(keysProp, valuesProp, currentAsset);
				}

				float listY = position.y + EditorGUIUtility.singleLineHeight + LIST_GAP;
				Rect listRect = new(position.x, listY, position.width, this._list.GetHeight());
				this._list.DoList(listRect);

				float helpBoxHeight = GetHelpBoxHeight(position.width);
				Rect helpRect = new(position.x, listY + this._list.GetHeight() + HELP_BOX_GAP, position.width, helpBoxHeight);
				EditorGUI.HelpBox(helpRect, _helpContent.text, MessageType.Info);
			}

			EditorGUI.EndProperty();
		}

		private void SetupList(SerializedProperty keys, SerializedProperty values, EnumAsset asset) {
			this._list = new ReorderableList(keys.serializedObject, keys, true, true, true, true) {
				drawHeaderCallback = rect => {
					float keyWidth = rect.width * (1f - BIND_VAL_WIDTH);
					EditorGUI.LabelField(new Rect(rect.x, rect.y, keyWidth, rect.height),
					 "Enum Key", EditorStyles.miniBoldLabel);
					EditorGUI.LabelField(new Rect(rect.x + keyWidth + 5, rect.y, rect.width *
					BIND_VAL_WIDTH, rect.height), "Binded Value", EditorStyles.miniBoldLabel);
				},

				drawElementCallback = (rect, index, isActive, isFocused) => {
					if (values.arraySize <= index) values.arraySize = keys.arraySize;

					var keyProp = keys.GetArrayElementAtIndex(index);
					var valProp = values.GetArrayElementAtIndex(index);

					float keyWidth = rect.width * (1f - BIND_VAL_WIDTH);
					Rect keyRect = new(rect.x, rect.y + 2, keyWidth, EditorGUIUtility.singleLineHeight);
					Rect valRect = new(rect.x + keyWidth + 5, rect.y + 2, rect.width - keyWidth - 5, EditorGUIUtility.singleLineHeight);

					string[] names = asset.Instances.Select(i => i.Name).ToArray();
					int[] ids = asset.Instances.Select(i => i.Value).ToArray();
					int currIdx = System.Array.IndexOf(ids, keyProp.intValue);

					bool isMissing = currIdx == -1;

					GUI.backgroundColor = isMissing ? new Color(1f, 0.4f, 0.4f) : Color.white;

					if (isMissing) {
						string[] displayNames = names.Prepend($"! MISSING (ID: {keyProp.intValue})").ToArray();
						int nextIdx = EditorGUI.Popup(keyRect, 0, displayNames);
						if (nextIdx > 0) keyProp.intValue = ids[nextIdx - 1];
					} else {
						int nextIdx = EditorGUI.Popup(keyRect, currIdx, names);
						keyProp.intValue = ids[nextIdx];
					}

					GUI.backgroundColor = Color.white;

					EditorGUI.PropertyField(valRect, valProp, GUIContent.none);
				},

				onAddCallback = l => {
					int index = l.serializedProperty.arraySize;
					l.serializedProperty.arraySize++;
					values.arraySize = l.serializedProperty.arraySize;

					if (asset.Instances.Count > 0) {
						l.serializedProperty.GetArrayElementAtIndex(index).intValue = asset.Instances[0].Value;
					}
				},

				onRemoveCallback = l => {
					values.DeleteArrayElementAtIndex(l.index);
					ReorderableList.defaultBehaviours.DoRemoveButton(l);
				}
			};
		}

		private float GetHelpBoxHeight(float width) {
			return EditorStyles.helpBox.CalcHeight(_helpContent, width);
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			var sourceProp = property.FindPropertyRelative(SOURCE_PROP);
			var keysProp = property.FindPropertyRelative(KEYS_PROP);

			float h = EditorGUIUtility.singleLineHeight + SOURCE_GAP;
			if (sourceProp.objectReferenceValue != null) {
				int elementCount = Mathf.Max(keysProp.arraySize, 1);
				float helpBoxHeight = GetHelpBoxHeight(EditorGUIUtility.currentViewWidth);
				h += HEADER_HEIGHT
				   + (elementCount * ROW_HEIGHT)
				   + FOOTER_HEIGHT
				   + HELP_BOX_GAP
				   + helpBoxHeight;
			} else {
				h += NO_SOURCE_HEIGHT;
			}
			return h;
		}
	}
}