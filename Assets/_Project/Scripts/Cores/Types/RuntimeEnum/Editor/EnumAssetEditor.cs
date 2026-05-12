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
			"DESIGNER GUIDELINES\n\n" +
			"• DEFAULT ENTRY: Local ID 0 should always represent the default/fallback state.\n" +
			"• REORDERING: Drag handles only change visual order. Internal IDs remain persistent.\n" +
			"• ENUM NAME: Used for display and hashing. Keep them unique.\n" +
			"• INTERNAL ID: Calculated as (AssetHash * 1B) + LocalID. Changing Local ID manually can break references.\n" +
			"• VALIDATION: Red highlights indicate duplicate names or Local IDs.";

		private const int VALUE_WIDTH = 80;

		private void OnEnable() {
			_list = new ReorderableList(serializedObject,
				serializedObject.FindProperty("Instances"),
				true, true, true, true) {

				drawHeaderCallback = rect => {
					float valueWidth = VALUE_WIDTH;
					float nameWidth = rect.width - valueWidth - 25;

					EditorGUI.LabelField(new Rect(rect.x + 15, rect.y, nameWidth, rect.height), "Enum Name", EditorStyles.miniBoldLabel);
					EditorGUI.LabelField(new Rect(rect.x + 15 + nameWidth, rect.y, valueWidth, rect.height), "Local ID", EditorStyles.miniBoldLabel);
				}
			};

			_list.drawElementCallback = (rect, index, isActive, isFocused) => {
				var element = _list.serializedProperty.GetArrayElementAtIndex(index);
				var nameProp = element.FindPropertyRelative(ALIAS_FIELD);
				var valueProp = element.FindPropertyRelative(VALUE_FIELD);
				rect.y += 2;

				float valueWidth = VALUE_WIDTH;
				float nameWidth = rect.width - valueWidth - 10;

				// --- Column 1: Name ---
				if (_duplicateNameIndices.Contains(index)) GUI.backgroundColor = DUPLICATE_COLOR;
				EditorGUI.PropertyField(new Rect(rect.x, rect.y, nameWidth, EditorGUIUtility.singleLineHeight), nameProp, GUIContent.none);
				GUI.backgroundColor = Color.white;

				// --- Column 2: Local ID (Masked) ---
				if (_duplicateValueIndices.Contains(index)) GUI.backgroundColor = DUPLICATE_COLOR;
				else GUI.backgroundColor = NORMAL_COLOR;

				// Using longValue for the new 64-bit ID system
				long fullId = valueProp.longValue;
				long multiplier = EnumAsset.MASK_MULTIPLIER;
				long localId = fullId % multiplier;

				EditorGUI.BeginChangeCheck();
				// We use LongField or IntField for the local part (since it's < 1B)
				long newLocalId = EditorGUI.LongField(new Rect(rect.x + nameWidth + 5, rect.y, valueWidth, EditorGUIUtility.singleLineHeight), localId);

				if (EditorGUI.EndChangeCheck()) {
					long prefix = (fullId / multiplier) * multiplier;
					// Clamp to ensure the local ID doesn't bleed into the Asset ID prefix
					valueProp.longValue = prefix + Mathf.Clamp((int)newLocalId, 0, (int)multiplier - 1);
				}
				GUI.backgroundColor = Color.white;
			};

			_list.onAddCallback = l => {
				var asset = (EnumAsset)target;
				Undo.RecordObject(asset, "Add Enum Entry");
				asset.AddNewInstance();
				EditorUtility.SetDirty(asset);
				serializedObject.Update(); // Refresh to show new entry
			};
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			EditorGUILayout.Space();
			SerializedProperty assetIdProp = serializedObject.FindProperty(ASSET_ID_FIELD);
			if (assetIdProp != null) {
				// Keep the Asset ID read-only so the prefix remains stable
				GUI.enabled = false;
				EditorGUILayout.PropertyField(assetIdProp, new GUIContent("Global Asset Prefix (Hash)"));
				GUI.enabled = true;
			}

			CheckForDuplicates();

			EditorGUILayout.Space();
			_list.DoLayoutList();

			if (_hasValueDuplicates) EditorGUILayout.HelpBox("DUPLICATE IDs: Multiple entries share the same Local ID!", MessageType.Error);
			if (_hasNameDuplicates) EditorGUILayout.HelpBox("DUPLICATE NAMES: Ensure names are unique.", MessageType.Warning);

			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(HELP_TEXT, MessageType.Info);

			serializedObject.ApplyModifiedProperties();
		}

		private void CheckForDuplicates() {
			_duplicateValueIndices.Clear();
			_duplicateNameIndices.Clear();
			_hasValueDuplicates = false;
			_hasNameDuplicates = false;

			var prop = serializedObject.FindProperty("Instances");
			Dictionary<long, List<int>> valueMap = new();
			Dictionary<string, List<int>> nameMap = new();

			for (int i = 0; i < prop.arraySize; i++) {
				var element = prop.GetArrayElementAtIndex(i);
				long val = element.FindPropertyRelative(VALUE_FIELD).longValue;
				string name = element.FindPropertyRelative(ALIAS_FIELD).stringValue;

				if (!valueMap.ContainsKey(val)) valueMap[val] = new List<int>();
				valueMap[val].Add(i);

				if (!nameMap.ContainsKey(name)) nameMap[name] = new List<int>();
				nameMap[name].Add(i);
			}

			foreach (var kvp in valueMap) if (kvp.Value.Count > 1) {
				_duplicateValueIndices.AddRange(kvp.Value);
				_hasValueDuplicates = true;
			}
			foreach (var kvp in nameMap) if (kvp.Value.Count > 1) {
				_duplicateNameIndices.AddRange(kvp.Value);
				_hasNameDuplicates = true;
			}
		}
	}
}