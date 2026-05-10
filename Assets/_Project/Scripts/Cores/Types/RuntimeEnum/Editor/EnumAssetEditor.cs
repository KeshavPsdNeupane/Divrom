using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using Kope.Core.Attribute; // Ensure this matches your namespace

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomEditor(typeof(EnumAsset))]
	public class EnumAssetEditor : Editor {
		private ReorderableList _list;

		private const string ALIAS_FIELD = "_name";
		private const string VALUE_FIELD = "_value";
		private const string ASSET_ID_FIELD = "_enumAssetId"; // Field we want to show as ReadOnly

		private List<int> _duplicateValueIndices = new();
		private List<int> _duplicateNameIndices = new();
		private bool _hasValueDuplicates = false;
		private bool _hasNameDuplicates = false;

		private static readonly Color DUPLICATE_COLOR = new(1f, 0.4f, 0.4f);
		private static readonly Color NORMAL_COLOR = new(0.6f, 0.8f, 1f);

		private const string HELP_TEXT =
			"DESIGNER GUIDELINES\n\n" +
			"• ORGANIZATION: Drag handles (left) to reorder. This is for visual grouping only.\n" +
			"• ANIMATION RULE: If this enum is used for Animator parameters, 'Idle' must always be at Local ID 0 to ensure default state consistency.\n" +
			"• ALIAS: The 'Enum Name' is for display and Animator hashing. Keep names unique and descriptive.\n" +
			"• INTERNAL ID: This is the persistent key (Asset ID + Local ID). DO NOT CHANGE if this entry is already referenced in save data or external assets.\n" +
			"• VALIDATION: Duplicate names or IDs will be highlighted and must be resolved to avoid runtime errors.";

		private const int VALUE_WIDTH = 65;

		private void OnEnable() {
			this._list = new ReorderableList(serializedObject,
				this.serializedObject.FindProperty("Instances"),
				true, true, true, true) {
				drawHeaderCallback = rect => {
					float valueWidth = VALUE_WIDTH;
					float nameWidth = rect.width - valueWidth - 25;

					EditorGUI.LabelField(new Rect(rect.x + 15, rect.y, nameWidth, rect.height), "Enum Name", EditorStyles.miniBoldLabel);
					EditorGUI.LabelField(new Rect(rect.x + 15 + nameWidth, rect.y, valueWidth, rect.height), "Local ID", EditorStyles.miniBoldLabel);
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
				if (this._duplicateNameIndices.Contains(index)) GUI.backgroundColor = DUPLICATE_COLOR;
				EditorGUI.PropertyField(new Rect(rect.x, rect.y, nameWidth, EditorGUIUtility.singleLineHeight), nameProp, GUIContent.none);
				GUI.backgroundColor = Color.white;

				// --- Column 2: Value (Masked Display) ---
				if (this._duplicateValueIndices.Contains(index)) GUI.backgroundColor = DUPLICATE_COLOR;
				else GUI.backgroundColor = NORMAL_COLOR;

				int fullId = valueProp.intValue;
				int multiplier = EnumAsset.MASK_MULTIPLIER;
				int displayId = fullId % multiplier;

				EditorGUI.BeginChangeCheck();
				int newLocalId = EditorGUI.IntField(new Rect(rect.x + nameWidth + 5, rect.y, valueWidth, EditorGUIUtility.singleLineHeight), displayId);

				if (EditorGUI.EndChangeCheck()) {
					int prefix = (fullId / multiplier) * multiplier;
					valueProp.intValue = prefix + Mathf.Clamp(newLocalId, 0, multiplier - 1);
				}
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

			// 1. Draw the Read-Only Asset ID at the top
			EditorGUILayout.Space();
			SerializedProperty assetIdProp = serializedObject.FindProperty(ASSET_ID_FIELD);
			if (assetIdProp != null) {
				EditorGUILayout.PropertyField(assetIdProp);
			}

			// 2. Check logic
			CheckForDuplicates();

			// 3. Draw the List
			EditorGUILayout.Space();
			this._list.DoLayoutList();

			// 4. Warnings
			if (this._hasValueDuplicates) EditorGUILayout.HelpBox("DUPLICATE IDs: Multiple entries share the same Local ID!", MessageType.Error);
			if (this._hasNameDuplicates) EditorGUILayout.HelpBox("DUPLICATE NAMES: Ensure names are unique for Animator hashing.", MessageType.Warning);

			// 5. Help Text
			EditorGUILayout.Space();
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

				if (!valueMap.ContainsKey(val)) valueMap[val] = new List<int>();
				valueMap[val].Add(i);

				if (!nameMap.ContainsKey(name)) nameMap[name] = new List<int>();
				nameMap[name].Add(i);
			}

			foreach (var kvp in valueMap) if (kvp.Value.Count > 1) {
				this._duplicateValueIndices.AddRange(kvp.Value);
				this._hasValueDuplicates = true;
			}
			foreach (var kvp in nameMap) if (kvp.Value.Count > 1) {
				this._duplicateNameIndices.AddRange(kvp.Value);
				this._hasNameDuplicates = true;
			}
		}
	}
}