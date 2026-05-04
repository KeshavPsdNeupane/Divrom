using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System.Linq;
using System.Collections.Generic;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	[CustomPropertyDrawer(typeof(EnumTable<>), true)]
	public class EnumTableDrawer : PropertyDrawer {

		// ── Serialized field names ────────────────────────────────────────────────
		private const string SOURCE_PROP = "_source";
		private const string KEYS_PROP = "_selectedValue";
		private const string VALUES_PROP = "_bindedValues";

		// ── Layout constants ──────────────────────────────────────────────────────
		private const float MIN_KEY_WIDTH = 80f;
		private const float DEFAULT_KEY_RATIO = 0.4f;
		private const float STACK_THRESHOLD = 3f;
		private const float HEADER_HEIGHT = 21f;
		private const float FOOTER_HEIGHT = 21f;
		private const float HELP_BOX_GAP = 5f;
		private const float SOURCE_GAP = 10f;
		private const float LIST_GAP = 5f;
		private const float NO_SOURCE_HEIGHT = 25f;
		private const float ELEMENT_PADDING = 4f;
		private const float COL_GAP = 5f;
		private const int BOTTOM_EXTRA_PADDING = 10; // just extra padding at bottom

		private static readonly GUIContent _helpContent = new(
			"Duplicate enum keys will be ignored — only the first mapping per key is used.");

		// ── Per-path list cache ───────────────────────────────────────────────────
		// Keyed by propertyPath so nested elements each own their list.
		// The list holds no asset reference — asset is resolved fresh each draw
		// so reordering the outer list never leaves a stale asset in a closure.
		private readonly Dictionary<string, ReorderableList> _lists = new();

		// ── List cache access ─────────────────────────────────────────────────────

		private ReorderableList GetList(SerializedProperty keys, SerializedProperty values, string path) {
			if (this._lists.TryGetValue(path, out var cached) &&
				cached.serializedProperty.serializedObject == keys.serializedObject)
				return cached;

			return this._lists[path] = BuildList(keys, values);
		}

		private void InvalidateList(string path) => this._lists.Remove(path);

		// ── Layout helpers ────────────────────────────────────────────────────────

		private static bool IsStacked(float valueHeight) =>
			valueHeight > EditorGUIUtility.singleLineHeight * STACK_THRESHOLD;

		// Shrinks key ratio 4% per extra value line; clamps to MIN_KEY_WIDTH.
		private static float KeyWidth(float totalWidth, float valueHeight) {
			float extraLines = Mathf.Max(0f, valueHeight / EditorGUIUtility.singleLineHeight - 1f);
			float ratio = Mathf.Max(0f, DEFAULT_KEY_RATIO - extraLines * 0.04f);
			return Mathf.Max(MIN_KEY_WIDTH, totalWidth * ratio);
		}

		// Stacked: key row + gap + value. Side-by-side: tallest of the two.
		private static float ElementHeight(float valueHeight) {
			float sl = EditorGUIUtility.singleLineHeight;
			return IsStacked(valueHeight)
				? sl + COL_GAP + valueHeight + ELEMENT_PADDING
				: Mathf.Max(sl, valueHeight) + ELEMENT_PADDING;
		}

		// ── OnGUI ─────────────────────────────────────────────────────────────────

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
				InvalidateList(property.propertyPath); // stale list would reference old asset
			}

			if (sourceProp.objectReferenceValue is not EnumAsset asset) {
				EditorGUI.EndProperty();
				return;
			}

			var list = GetList(keysProp, valuesProp, property.propertyPath);
			float listY = position.y + EditorGUIUtility.singleLineHeight + LIST_GAP;

			// Pass asset separately so callbacks always read it from current
			// serialized state, not from a captured closure that goes stale on reorder.
			list.drawElementCallback = MakeDrawElement(keysProp, valuesProp, sourceProp);
			list.elementHeightCallback = MakeElementHeight(valuesProp);

			list.DoList(new Rect(position.x, listY, position.width, list.GetHeight()));

			float helpH = GetHelpBoxHeight(position.width);
			EditorGUI.HelpBox(
				new Rect(position.x, listY + list.GetHeight() + HELP_BOX_GAP, position.width, helpH),
				_helpContent.text, MessageType.Info);

			EditorGUI.EndProperty();
		}

		// ── Callbacks built fresh each OnGUI ──────────────────────────────────────
		// sourceProp is passed instead of asset so the asset is read from the
		// live serialized state at draw time — not captured at BuildList time.
		// This is what prevents MISSING IDs after reorder.

		private static ReorderableList.ElementCallbackDelegate MakeDrawElement(
				SerializedProperty keys, SerializedProperty values, SerializedProperty sourceProp) {
			return (rect, index, _, _) => {
				if (values.arraySize <= index) values.arraySize = keys.arraySize;

				// Resolve asset fresh — not from a closure captured at setup time.
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

					// Foldout arrow needs ~14px clearance on the left of the value rect.
					// Without it the arrow is hidden behind the key popup for compound types.
					float valX = rect.x + kw + COL_GAP;
					float valW = rect.width - kw - COL_GAP;
					bool hasFoldout = valProp.hasChildren && valProp.propertyType == SerializedPropertyType.Generic;
					if (hasFoldout) {
						float indent = EditorGUIUtility.singleLineHeight; // ~18px, covers arrow + default indent
						valX += indent;
						valW -= indent;
					}
					valRect = new Rect(valX, rect.y + 2, valW, valHeight);
				}

				string[] names = asset.Instances.Select(i => i.Alias).ToArray();
				int[] ids = asset.Instances.Select(i => i.InternalValue).ToArray();
				int currIdx = System.Array.IndexOf(ids, keyProp.intValue);
				bool missing = currIdx == -1;

				GUI.backgroundColor = missing ? new Color(1f, 0.4f, 0.4f) : Color.white;
				if (missing) {
					var display = names.Prepend($"! MISSING (ID: {keyProp.intValue})").ToArray();
					int next = EditorGUI.Popup(keyRect, 0, display);
					if (next > 0) keyProp.intValue = ids[next - 1];
				} else {
					keyProp.intValue = ids[EditorGUI.Popup(keyRect, currIdx, names)];
				}
				GUI.backgroundColor = Color.white;

				EditorGUI.PropertyField(valRect, valProp, GUIContent.none, true);
			};
		}

		private static ReorderableList.ElementHeightCallbackDelegate MakeElementHeight(SerializedProperty values) {
			return index => {
				if (index >= values.arraySize) return EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;
				return ElementHeight(EditorGUI.GetPropertyHeight(values.GetArrayElementAtIndex(index), true));
			};
		}

		// ── List construction ─────────────────────────────────────────────────────
		// asset is intentionally NOT passed here — callbacks are assigned fresh
		// each OnGUI via MakeDrawElement so they always read the live sourceProp.

		private ReorderableList BuildList(SerializedProperty keys, SerializedProperty values) {
			return new ReorderableList(keys.serializedObject, keys, true, true, true, true) {

				drawHeaderCallback = rect => {
					float kw = rect.width * DEFAULT_KEY_RATIO;
					EditorGUI.LabelField(new Rect(rect.x, rect.y, kw, rect.height),
						"Enum Key", EditorStyles.miniBoldLabel);
					EditorGUI.LabelField(new Rect(rect.x + kw + COL_GAP, rect.y, rect.width - kw, rect.height),
						"Binded Value", EditorStyles.miniBoldLabel);
				},

				onAddCallback = l => {
					int i = l.serializedProperty.arraySize;
					l.serializedProperty.arraySize++;
					values.arraySize = l.serializedProperty.arraySize;
					// asset not available here — first instance default applied in drawElementCallback
				},

				onRemoveCallback = l => {
					values.DeleteArrayElementAtIndex(l.index);
					ReorderableList.defaultBehaviours.DoRemoveButton(l);
				},

				// keys is reordered by ReorderableList internally; mirror it on values.
				onReorderCallbackWithDetails = (_, oldIndex, newIndex) => {
					values.MoveArrayElement(oldIndex, newIndex);
					this._lists.Clear(); // all sibling paths shifted; rebuild on next draw
				}
			};
		}


		private static float GetHelpBoxHeight(float width) =>
			EditorStyles.helpBox.CalcHeight(_helpContent, width);

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			var sourceProp = property.FindPropertyRelative(SOURCE_PROP);
			var keysProp = property.FindPropertyRelative(KEYS_PROP);
			var valuesProp = property.FindPropertyRelative(VALUES_PROP);

			float h = EditorGUIUtility.singleLineHeight + SOURCE_GAP;

			if (sourceProp.objectReferenceValue == null)
				return h + NO_SOURCE_HEIGHT + BOTTOM_EXTRA_PADDING;

			int count = keysProp.arraySize;
			float elemsH = count == 0
				? EditorGUIUtility.singleLineHeight + ELEMENT_PADDING
				: Enumerable.Range(0, count).Sum(i => ElementHeight(
					i < valuesProp.arraySize
						? EditorGUI.GetPropertyHeight(valuesProp.GetArrayElementAtIndex(i), true)
						: EditorGUIUtility.singleLineHeight));

			return h + HEADER_HEIGHT + elemsH + FOOTER_HEIGHT
					 + HELP_BOX_GAP + GetHelpBoxHeight(EditorGUIUtility.currentViewWidth)
					 + BOTTOM_EXTRA_PADDING;
		}
	}
}