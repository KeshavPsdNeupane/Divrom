using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomEditor(typeof(EnumAsset))]
	public class EnumAssetEditor : Editor {
		private ReorderableList _list;

		// Tracking lists for indices that have collisions
		private const string ALIAS_FIELD = "_name";
		private const string VALUE_FIELD = "_value";
		private List<int> _duplicateValueIndices = new();
		private List<int> _duplicateNameIndices = new();

		private bool _hasValueDuplicates = false;
		private bool _hasNameDuplicates = false;

		private static readonly Color DUPLICATE_COLOR = new(1f, 0.4f, 0.4f);
		private static readonly Color NORMAL_COLOR = new(0.6f, 0.8f, 1f);

		private const string HELP_TEXT =
			"Designer Note:\n" +
			"• You can drag the handles (left) to reorganize the list.\n" +
			"• 'Enum Name' is for display only, but should be unique for clarity.\n" +
			"• 'ID/Value' is the unique key. If you change this, existing references will return null.\n" +
			"• Duplicate IDs/Names will be highlighted in red.";

		private const int VALUE_WIDTH = 50;

		private void OnEnable() {
			this._list = new ReorderableList(serializedObject,
				this.serializedObject.FindProperty("Instances"),
				true, true, true, true) {
				drawHeaderCallback = rect => {
					float valueWidth = VALUE_WIDTH;
					float nameWidth = rect.width - valueWidth - 25;

					EditorGUI.LabelField(new Rect(rect.x + 15, rect.y, nameWidth, rect.height), "Enum Name", EditorStyles.miniBoldLabel);
					EditorGUI.LabelField(new Rect(rect.x + 15 + nameWidth, rect.y, valueWidth, rect.height), "ID/Value", EditorStyles.miniBoldLabel);
				}
			};

			this._list.drawElementCallback = (rect, index, isActive, isFocused) => {
				var element = this._list.serializedProperty.GetArrayElementAtIndex(index);
				var nameProp = element.FindPropertyRelative(ALIAS_FIELD);
				var valueProp = element.FindPropertyRelative(VALUE_FIELD);
				rect.y += 2;

				float valueWidth = VALUE_WIDTH;
				float nameWidth = rect.width - valueWidth - 10;

				// --- Column 1: Name ---
				if (_duplicateNameIndices.Contains(index)) GUI.backgroundColor = DUPLICATE_COLOR;
				EditorGUI.PropertyField(
					new Rect(rect.x, rect.y, nameWidth, EditorGUIUtility.singleLineHeight),
					nameProp, GUIContent.none);
				GUI.backgroundColor = Color.white;

				// --- Column 2: Value ---
				if (_duplicateValueIndices.Contains(index)) GUI.backgroundColor = DUPLICATE_COLOR;
				else GUI.backgroundColor = NORMAL_COLOR;

				EditorGUI.PropertyField(
					new Rect(rect.x + nameWidth + 5, rect.y, valueWidth, EditorGUIUtility.singleLineHeight),
					valueProp, GUIContent.none);

				GUI.backgroundColor = Color.white;
			};

			this._list.onAddCallback = l => {
				var asset = (EnumAsset)target;
				Undo.RecordObject(asset, "Add Enum Entry");
				asset.AddNewInstance();
				EditorUtility.SetDirty(asset);
			};
		}

		public override void OnInspectorGUI() {
			this.serializedObject.Update();

			// Perform detection before drawing
			CheckForDuplicates();

			EditorGUILayout.Space();
			this._list.DoLayoutList();

			// Error Feedback
			if (_hasValueDuplicates) {
				EditorGUILayout.HelpBox("DUPLICATE IDs: Multiple entries share the same Value. This breaks data integrity!", MessageType.Error);
			}

			if (_hasNameDuplicates) {
				EditorGUILayout.HelpBox("DUPLICATE NAMES: Multiple entries share the same Name. This may cause confusion.", MessageType.Warning);
			}

			EditorGUILayout.HelpBox(HELP_TEXT, MessageType.Info);

			this.serializedObject.ApplyModifiedProperties();
		}

		private void CheckForDuplicates() {
			this._duplicateValueIndices.Clear();
			this._duplicateNameIndices.Clear();
			this._hasValueDuplicates = false;
			this._hasNameDuplicates = false;

			var prop = this.serializedObject.FindProperty("Instances");

			Dictionary<int, List<int>> valueMap = new();
			Dictionary<string, List<int>> nameMap = new();

			for (int i = 0; i < prop.arraySize; i++) {
				var element = prop.GetArrayElementAtIndex(i);
				int val = element.FindPropertyRelative(VALUE_FIELD).intValue;
				string name = element.FindPropertyRelative(ALIAS_FIELD).stringValue;

				// Track Values
				if (!valueMap.ContainsKey(val)) valueMap[val] = new List<int>();
				valueMap[val].Add(i);

				// Track Names
				if (!nameMap.ContainsKey(name)) nameMap[name] = new List<int>();
				nameMap[name].Add(i);
			}

			// Mark Value collisions
			foreach (var kvp in valueMap) {
				if (kvp.Value.Count > 1) {
					this._duplicateValueIndices.AddRange(kvp.Value);
					this._hasValueDuplicates = true;
				}
			}

			// Mark Name collisions
			foreach (var kvp in nameMap) {
				if (kvp.Value.Count > 1) {
					this._duplicateNameIndices.AddRange(kvp.Value);
					this._hasNameDuplicates = true;
				}
			}
		}
	}
}