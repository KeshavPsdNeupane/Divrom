using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomEditor(typeof(EnumToEnumMap))]
	public class EnumToEnumMapEditor : Editor {

		// ── Serialized field names ───────────────────────────────────────────────
		private const string SOURCE_PROP = "_source";
		private const string TARGET_PROP = "_target";

		// ── Layout ───────────────────────────────────────────────────────────────
		private const float COL_GAP = 5f;
		private const float DEFAULT_KEY_RATIO = 0.45f;
		private const float ROW_HEIGHT = 20f;
		private const float ROW_PADDING = 2f;
		private const float HEADER_HEIGHT = 20f;

		private static readonly Color MISSING_COLOR = new(1f, 0.4f, 0.4f);
		private static readonly Color UNMAPPED_COLOR = new(1f, 0.85f, 0.4f);
		private static readonly Color EXCLUDED_COLOR = new(1f, 0.4f, 0.4f);

		private const string HELP_TEXT =
			"Mapping Note:\n" +
			"• Each source entry maps to exactly one target entry (many-to-one).\n" +
			"• Multiple source entries may point to the same target — that is allowed.\n" +
			"• Yellow rows are unmapped or just got excluded. Red rows have a target ID that no longer exists.\n" +
			"• The row list is driven by the source enum. Add/remove entries there.\n" +
			"• Excluded Targets are hidden from the picker and cannot be mapped to from any source.";

		public override void OnInspectorGUI() {
			serializedObject.Update();

			var sourceProp = serializedObject.FindProperty(SOURCE_PROP);
			var targetProp = serializedObject.FindProperty(TARGET_PROP);

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(sourceProp);
			EditorGUILayout.PropertyField(targetProp);
			if (EditorGUI.EndChangeCheck()) {
				serializedObject.ApplyModifiedProperties();
				EditorUtility.SetDirty(target);
				serializedObject.Update();
			}

			EditorGUILayout.Space(6);

			var map = (EnumToEnumMap)target;
			var sourceAsset = sourceProp.objectReferenceValue as EnumAsset;
			var targetAsset = targetProp.objectReferenceValue as EnumAsset;

			if (sourceAsset == null || targetAsset == null) {
				EditorGUILayout.HelpBox("Assign both a Source and Target enum asset to configure mappings.", MessageType.Info);
				serializedObject.ApplyModifiedProperties();
				return;
			}

			string[] allTargetNames = targetAsset.Instances.Select(i => i.Alias).ToArray();
			long[] allTargetIds = targetAsset.Instances.Select(i => i.InternalValue).ToArray();

			var allowedInstances = targetAsset.Instances.Where(i => !map.IsExcluded(i.InternalValue)).ToList();
			string[] allowedNames = allowedInstances.Select(i => i.Alias).ToArray();
			long[] allowedIds = allowedInstances.Select(i => i.InternalValue).ToArray();

			// ── Mapping rows ──────────────────────────────────────────────────────
			DrawSectionHeader("Mappings");
			DrawHead("Source Entry", "Target Entry");

			bool dirty = false;
			foreach (var sourceInstance in sourceAsset.Instances) {
				dirty |= DrawMappingRow(map, sourceInstance, allowedNames, allowedIds);
			}

			EditorGUILayout.Space(8);

			DrawSectionHeader("Excluded Targets");
			DrawHead("Target Entry", "");

			var currentExclusions = map.ExcludedTargets.ToList();
			foreach (long excludedId in currentExclusions) {
				int idx = System.Array.IndexOf(allTargetIds, excludedId);
				string label = idx == -1 ? $"! MISSING (ID: {excludedId})" : allTargetNames[idx];
				dirty |= DrawExclusionRow(map, excludedId, label, idx == -1);
			}

			var addableInstances = targetAsset.Instances.Where(i => !map.IsExcluded(i.InternalValue)).ToList();
			if (addableInstances.Count > 0) {
				EditorGUILayout.Space(2);
				var addRect = GUILayoutUtility.GetRect(0, ROW_HEIGHT + ROW_PADDING * 2, GUILayout.ExpandWidth(true));
				addRect.y += ROW_PADDING;
				addRect.height = ROW_HEIGHT;

				string[] addableNames = addableInstances.Select(i => $"+ {i.Alias}").ToArray();
				int picked = EditorGUI.Popup(addRect, -1, addableNames);
				if (picked >= 0) {
					long chosenId = addableInstances[picked].InternalValue;
					map.AddExclusion(chosenId);

					foreach (var sourceInstance in sourceAsset.Instances) {
						if (map.GetTargetValue(sourceInstance.InternalValue) == chosenId)
							map.RemoveMapping(sourceInstance.InternalValue);
					}
					dirty = true;
				}
			}

			if (dirty) {
				map.OnBeforeSerialize();
				EditorUtility.SetDirty(target);
			}

			EditorGUILayout.Space(4);
			EditorGUILayout.HelpBox(HELP_TEXT, MessageType.Info);

			serializedObject.ApplyModifiedProperties();
		}

		private static void DrawSectionHeader(string title) {
			var rect = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));
			EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);
		}

		private static void DrawHead(string leftLabel, string rightLabel) {
			var rect = GUILayoutUtility.GetRect(0, HEADER_HEIGHT, GUILayout.ExpandWidth(true));
			float kw = rect.width * DEFAULT_KEY_RATIO;

			EditorGUI.LabelField(new Rect(rect.x, rect.y, kw, rect.height), leftLabel, EditorStyles.miniBoldLabel);
			if (!string.IsNullOrEmpty(rightLabel))
				EditorGUI.LabelField(new Rect(rect.x + kw + COL_GAP, rect.y, rect.width - kw - COL_GAP, rect.height), rightLabel, EditorStyles.miniBoldLabel);

			EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), new Color(0.5f, 0.5f, 0.5f, 0.5f));
		}


		private static bool DrawMappingRow(EnumToEnumMap map, EnumInstance sourceInstance, string[] allowedNames, long[] allowedIds) {
			var rect = GUILayoutUtility.GetRect(0, ROW_HEIGHT + ROW_PADDING * 2, GUILayout.ExpandWidth(true));
			rect.y += ROW_PADDING;
			rect.height = ROW_HEIGHT;

			float kw = rect.width * DEFAULT_KEY_RATIO;
			float vx = rect.x + kw + COL_GAP;
			float vw = rect.width - kw - COL_GAP;

			EditorGUI.LabelField(new Rect(rect.x, rect.y, kw, rect.height), sourceInstance.Alias);

			long sourceId = sourceInstance.InternalValue;
			long currentTargetId = map.GetTargetValue(sourceId);
			int currentIdx = System.Array.IndexOf(allowedIds, currentTargetId);
			bool unmapped = currentTargetId == 0;
			bool excluded = !unmapped && map.IsExcluded(currentTargetId);
			bool missing = !unmapped && !excluded && currentIdx == -1;

			if (unmapped) GUI.backgroundColor = UNMAPPED_COLOR;
			else if (excluded) GUI.backgroundColor = EXCLUDED_COLOR;
			else if (missing) GUI.backgroundColor = MISSING_COLOR;

			var popupRect = new Rect(vx, rect.y, vw, rect.height);
			bool changed = false;

			if (excluded || missing) {
				string prefix = excluded ? "EXCLUDED" : "MISSING";
				var display = allowedNames.Prepend($"! {prefix} — was ID: {currentTargetId}").ToArray();
				int next = EditorGUI.Popup(popupRect, 0, display);
				if (next > 0) { map.SetMapping(sourceId, allowedIds[next - 1]); changed = true; }
			} else {
				var display = allowedNames.Prepend("— Unmapped —").ToArray();
				int selectedIdx = unmapped ? 0 : currentIdx + 1;
				int next = EditorGUI.Popup(popupRect, selectedIdx, display);
				if (next != selectedIdx) {
					if (next == 0) map.RemoveMapping(sourceId);
					else map.SetMapping(sourceId, allowedIds[next - 1]);
					changed = true;
				}
			}

			GUI.backgroundColor = Color.white;
			return changed;
		}

		private static bool DrawExclusionRow(EnumToEnumMap map, long excludedId, string label, bool isMissing) {
			var rect = GUILayoutUtility.GetRect(0, ROW_HEIGHT + ROW_PADDING * 2, GUILayout.ExpandWidth(true));
			rect.y += ROW_PADDING;
			rect.height = ROW_HEIGHT;

			float removeWidth = 22f;
			float labelWidth = rect.width - removeWidth - COL_GAP;

			if (isMissing) GUI.backgroundColor = MISSING_COLOR;
			EditorGUI.LabelField(new Rect(rect.x, rect.y, labelWidth, rect.height), label);
			GUI.backgroundColor = Color.white;

			bool changed = false;
			if (GUI.Button(new Rect(rect.x + labelWidth + COL_GAP, rect.y, removeWidth, rect.height), "✕")) {
				map.RemoveExclusion(excludedId);
				changed = true;
			}

			return changed;
		}
	}
}