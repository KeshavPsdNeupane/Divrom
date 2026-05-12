using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomEditor(typeof(EnumAsset))]
	public class EnumAssetEditor : Editor {
		private ReorderableList _list;

		private const string ALIAS_FIELD = "_name";
		private const string VALUE_FIELD = "_value";
		private const string ASSET_ID_FIELD = "_enumAssetId";
		private readonly List<int> _duplicateValueIndices = new();
		private readonly List<int> _duplicateNameIndices = new();
		private bool _hasValueDuplicates = false;
		private bool _hasNameDuplicates = false;

		private static readonly Color DUPLICATE_COLOR = new(1f, 0.4f, 0.4f);
		private static readonly Color NORMAL_COLOR = new(0.6f, 0.8f, 1f);

		private const string HELP_TEXT =
			"DESIGNER RULES\n\n" +
			"• FALLBACK: Local ID 0 must be the default state (e.g., 'Idle').\n" +
			"• ORDERING: Dragging rows is visual only; IDs never change.\n" +
			"• NAMES: Must be unique (used for Animator hashing).\n" +
			"• PERSISTENCE: Caution while changing IDs; it may break the referencing system.\n" +
			"• VALIDATION: Avoid duplicate names/IDs to prevent lookup errors.\n" +
			"• COLLISIONS: Use 'Enum Asset Manager' (Tools > Kope) to fix ID conflicts.";

		private const string COLLISION_WARNING =
			"CRITICAL: Before using this enum, or doing anything in this enum, check if there is a collision or not in the Enum Asset Manager.";

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

			// 1. Draw the Asset ID
			EditorGUILayout.Space();
			SerializedProperty assetIdProp = serializedObject.FindProperty(ASSET_ID_FIELD);
			SerializedProperty manualProp = serializedObject.FindProperty("_isManualId");

			if (assetIdProp != null) {
				string labelPrefix = manualProp.boolValue ? "[MANUAL] " : "[AUTO] ";
				GUIContent idLabel = new GUIContent(labelPrefix + "Asset ID",
					"Check the 'Enum Asset Manager' (Tools > Kope) before creating/modifying Enums.");

				EditorGUILayout.PropertyField(assetIdProp, idLabel);

				if (manualProp.boolValue) {
					EditorGUILayout.HelpBox($"MANUAL ID ACTIVE: This ID is locked and will not change automatically.", MessageType.Warning);
					if (GUILayout.Button("Revert to Auto-GUID Hash")) {
						manualProp.boolValue = false;
					}
				}
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

			// 6. Highlighted Collision Warning
			EditorGUILayout.HelpBox(COLLISION_WARNING, MessageType.Warning);

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