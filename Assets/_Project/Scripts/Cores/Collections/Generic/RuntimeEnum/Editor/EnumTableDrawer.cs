using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System.Linq;
using System.Collections.Generic;

namespace Kope.Core.Type.EnumAsset.EditorTools {
	/// <summary>
	/// Same adaptive layout as SerializableDictionaryDrawer: TABLE mode (a real "Enum Key |
	/// Binded Value" grid) when every bound value fits on one line, COVERING mode (one bordered
	/// box per element, key row + value row stacked) as soon as any value needs more than that.
	/// The enum key itself is always a single-line Popup, so it never gets a collapse arrow —
	/// only the value side can.
	/// </summary>
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
		private const float ELEMENT_PADDING = 4f;
		private const float COL_GAP = 5f;
		private const int BOTTOM_EXTRA_PADDING = 10;

		// Odin-style boxed group metrics, shared with COVERING mode's per-element boxes
		private const float BOX_SIDE_MARGIN = 4f;
		private const float BOX_HEADER_PAD_V = 3f;
		private const float BOX_SEPARATOR_H = 1f;
		private const float BOX_BODY_PAD_TOP = 5f;
		private const float BOX_BODY_PAD_BOTTOM = 5f;
		private const float BOX_BODY_PAD_H = 8f;

		private static readonly GUIContent _helpContent = new(
			"Duplicate enum keys will be ignored — only the first mapping per key is used.");

		private static Color SeparatorColor => EditorGUIUtility.isProSkin
			? new Color(1f, 1f, 1f, 0.08f)
			: new Color(0f, 0f, 0f, 0.12f);

		// Cache per-path to handle multiple tables or nested tables correctly
		private readonly Dictionary<string, ReorderableList> _lists = new();

		private ReorderableList GetList(SerializedProperty keys, SerializedProperty values, string path) {
			if (this._lists.TryGetValue(path, out var cached) &&
				cached.serializedProperty.serializedObject == keys.serializedObject)
				return cached;

			return this._lists[path] = BuildList(keys, values);
		}

		private void InvalidateList(string path) => this._lists.Remove(path);

		// ── Layout Helpers ────────────────────────────────────────────────────────

		private static bool IsStacked(float h) => h > EditorGUIUtility.singleLineHeight * STACK_THRESHOLD;

		// A value is "foldable" if it's a plain class/struct with its own fields — the only
		// case where we can safely flatten-draw children ourselves instead of the value's
		// normal rendering. The enum key (an int) is never foldable.
		private static bool IsFoldable(SerializedProperty prop) =>
			prop.propertyType == SerializedPropertyType.Generic && prop.hasVisibleChildren;

		// Anything that needs more than one line gets its own arrow — foldable structs AND
		// tall non-foldable things (AnimationCurve, Gradient, a custom-drawn struct).
		private static bool NeedsCollapseArrow(SerializedProperty prop) {
			if (IsFoldable(prop)) return true;
			return IsStacked(EditorGUI.GetPropertyHeight(prop, true));
		}

		// TABLE mode is only valid while every bound value is a one-liner. As soon as any value
		// needs a collapse arrow, the whole table switches to COVERING mode.
		private static bool ComputeTableMode(SerializedProperty values) {
			for (int i = 0; i < values.arraySize; i++) {
				if (NeedsCollapseArrow(values.GetArrayElementAtIndex(i)))
					return false;
			}
			return true;
		}

		private static float KeyWidth(float total, float h) {
			float extraLines = Mathf.Max(0f, h / EditorGUIUtility.singleLineHeight - 1f);
			float ratio = Mathf.Max(0f, DEFAULT_KEY_RATIO - extraLines * 0.04f);
			return Mathf.Max(MIN_KEY_WIDTH, total * ratio);
		}

		private static float ElementHeight(float h) {
			float sl = EditorGUIUtility.singleLineHeight;
			return IsStacked(h)
				? sl + COL_GAP + h + ELEMENT_PADDING
				: Mathf.Max(sl, h) + ELEMENT_PADDING;
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
				InvalidateList(property.propertyPath);
			}

			if (sourceProp.objectReferenceValue is not EnumAsset asset) {
				EditorGUI.EndProperty();
				return;
			}

			var list = GetList(keysProp, valuesProp, property.propertyPath);
			bool tableMode = ComputeTableMode(valuesProp);
			float listY = position.y + EditorGUIUtility.singleLineHeight + LIST_GAP;

			// Freshly assign callbacks so they capture the current SerializedProperty instances
			// and reflect whichever layout mode this frame's data calls for.
			list.drawElementCallback = tableMode
				? MakeTableDrawElement(keysProp, valuesProp, sourceProp)
				: MakeCoveringDrawElement(keysProp, valuesProp, sourceProp);
			list.elementHeightCallback = tableMode
				? MakeTableElementHeight(valuesProp)
				: MakeCoveringElementHeight(valuesProp);

			// COVERING mode's boxes already label themselves "Key"/"Value", so the column
			// header is only needed (and only meaningful) in TABLE mode.
			list.headerHeight = tableMode ? HEADER_HEIGHT : 0f;
			list.drawHeaderCallback = tableMode ? DrawTableHeader : null;

			list.DoList(new Rect(position.x, listY, position.width, list.GetHeight()));

			float helpH = GetHelpBoxHeight(position.width);
			EditorGUI.HelpBox(
				new Rect(position.x, listY + list.GetHeight() + HELP_BOX_GAP, position.width, helpH),
				_helpContent.text, MessageType.Info);

			EditorGUI.EndProperty();
		}

		// ── TABLE mode ───────────────────────────────────────────────────────────

		private static void DrawTableHeader(Rect rect) {
			float kw = rect.width * DEFAULT_KEY_RATIO;
			EditorGUI.LabelField(new Rect(rect.x, rect.y, kw, rect.height), "Enum Key", EditorStyles.miniBoldLabel);
			EditorGUI.LabelField(new Rect(rect.x + kw + COL_GAP, rect.y, rect.width - kw, rect.height), "Binded Value", EditorStyles.miniBoldLabel);
		}

		private static ReorderableList.ElementCallbackDelegate MakeTableDrawElement(
				SerializedProperty keys, SerializedProperty values, SerializedProperty sourceProp) {
			return (rect, index, _, _) => {
				if (values.arraySize <= index) values.arraySize = keys.arraySize;
				if (sourceProp.objectReferenceValue is not EnumAsset asset) return;

				var keyProp = keys.GetArrayElementAtIndex(index);
				var valProp = values.GetArrayElementAtIndex(index);
				float valHeight = EditorGUI.GetPropertyHeight(valProp, true);
				float sl = EditorGUIUtility.singleLineHeight;

				float kw = KeyWidth(rect.width, valHeight);
				Rect keyRect = new(rect.x, rect.y + 2, kw, sl);

				float valX = rect.x + kw + COL_GAP;
				float valW = rect.width - kw - COL_GAP;

				// Support for the rare Generic-with-hidden-children case that still slips
				// through NeedsCollapseArrow (hasChildren true, hasVisibleChildren false).
				if (valProp.hasChildren && valProp.propertyType == SerializedPropertyType.Generic) {
					float indent = 15f;
					valX += indent;
					valW -= indent;
				}
				Rect valRect = new(valX, rect.y + 2, valW, valHeight);

				DrawEnumKeyPopup(keyRect, keyProp, asset);
				EditorGUI.PropertyField(valRect, valProp, GUIContent.none, true);

				// Grid lines: vertical divider between columns, horizontal divider under the
				// row — makes this read as a real table instead of a flat side-by-side layout.
				float rowH = Mathf.Max(sl, valHeight);
				Rect vDivider = new(rect.x + kw + COL_GAP * 0.5f - BOX_SEPARATOR_H * 0.5f, rect.y + 2, BOX_SEPARATOR_H, rowH);
				EditorGUI.DrawRect(vDivider, SeparatorColor);

				Rect hDivider = new(rect.x - 4f, rect.y + rowH + (ELEMENT_PADDING * 0.5f), rect.width + 8f, BOX_SEPARATOR_H);
				EditorGUI.DrawRect(hDivider, SeparatorColor);
			};
		}

		private static ReorderableList.ElementHeightCallbackDelegate MakeTableElementHeight(SerializedProperty values) =>
			i => (i < values.arraySize)
				? ElementHeight(EditorGUI.GetPropertyHeight(values.GetArrayElementAtIndex(i), true))
				: EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;

		// ── COVERING mode ────────────────────────────────────────────────────────

		// Draws the value half of a covering-mode row. Short/simple values render as a normal
		// labeled field. Foldable or tall values get their own arrow — clicking it reveals the
		// nested serialized children directly beneath.
		private static void DrawValueFieldRow(Rect rect, SerializedProperty prop) {
			if (!NeedsCollapseArrow(prop)) {
				EditorGUI.PropertyField(rect, prop, new GUIContent("Value"), true);
				return;
			}

			float headerLine = EditorGUIUtility.singleLineHeight;
			Rect headerRect = new(rect.x, rect.y, rect.width, headerLine);

			string preview = GetCollapsedPreview(prop).text;
			EditorGUI.BeginChangeCheck();
			bool expanded = EditorGUI.Foldout(headerRect, prop.isExpanded, $"Value  ({preview})", true);
			if (EditorGUI.EndChangeCheck()) {
				prop.isExpanded = expanded;
			}

			if (!prop.isExpanded) return;

			float sepY = headerRect.yMax;
			EditorGUI.DrawRect(new Rect(rect.x, sepY, rect.width, BOX_SEPARATOR_H), SeparatorColor);

			float bodyY = sepY + BOX_SEPARATOR_H + BOX_BODY_PAD_TOP;
			Rect bodyRect = new(rect.x + BOX_BODY_PAD_H, bodyY, rect.width - BOX_BODY_PAD_H, rect.yMax - bodyY);

			if (IsFoldable(prop)) {
				DrawChildrenFlattened(bodyRect, prop);
			} else {
				bodyRect.height = EditorGUI.GetPropertyHeight(prop, true);
				EditorGUI.PropertyField(bodyRect, prop, GUIContent.none, true);
			}
		}

		// Single source of truth for the value row's height — mirrors DrawValueFieldRow's
		// branching exactly so the height callback and the draw callback can never disagree.
		private static float GetFieldRowHeight(SerializedProperty prop) {
			if (!NeedsCollapseArrow(prop))
				return EditorGUI.GetPropertyHeight(prop, true);

			float headerLine = EditorGUIUtility.singleLineHeight;
			if (!prop.isExpanded)
				return headerLine;

			float bodyHeight = IsFoldable(prop) ? GetChildrenHeight(prop) : EditorGUI.GetPropertyHeight(prop, true);
			return headerLine + BOX_SEPARATOR_H + BOX_BODY_PAD_TOP + bodyHeight + BOX_BODY_PAD_BOTTOM;
		}

		// The key row is always a single-line Popup, so its section height never varies.
		private static float GetCoveringBoxHeight(SerializedProperty valProp) {
			float keySection = EditorGUIUtility.singleLineHeight + BOX_HEADER_PAD_V * 2f;
			float valSection = GetFieldRowHeight(valProp) + BOX_HEADER_PAD_V * 2f;
			return keySection + BOX_SEPARATOR_H + valSection;
		}

		private static ReorderableList.ElementCallbackDelegate MakeCoveringDrawElement(
				SerializedProperty keys, SerializedProperty values, SerializedProperty sourceProp) {
			return (rect, index, _, _) => {
				if (values.arraySize <= index) values.arraySize = keys.arraySize;
				if (sourceProp.objectReferenceValue is not EnumAsset asset) return;

				var keyProp = keys.GetArrayElementAtIndex(index);
				var valProp = values.GetArrayElementAtIndex(index);

				rect.y += 2f;
				rect.height -= ELEMENT_PADDING;

				GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

				float sl = EditorGUIUtility.singleLineHeight;
				Rect keyRowRect = new(rect.x + BOX_SIDE_MARGIN, rect.y + BOX_HEADER_PAD_V, rect.width - BOX_SIDE_MARGIN * 2f, sl);
				Rect keyFieldRect = EditorGUI.PrefixLabel(keyRowRect, new GUIContent("Key"));
				DrawEnumKeyPopup(keyFieldRect, keyProp, asset);

				float sepY = keyRowRect.yMax + BOX_HEADER_PAD_V;
				Rect sepRect = new(rect.x + 1f, sepY, rect.width - 2f, BOX_SEPARATOR_H);
				EditorGUI.DrawRect(sepRect, SeparatorColor);

				float valRowH = GetFieldRowHeight(valProp);
				Rect valRect = new(rect.x + BOX_SIDE_MARGIN, sepY + BOX_SEPARATOR_H + BOX_HEADER_PAD_V, rect.width - BOX_SIDE_MARGIN * 2f, valRowH);
				DrawValueFieldRow(valRect, valProp);
			};
		}

		private static ReorderableList.ElementHeightCallbackDelegate MakeCoveringElementHeight(SerializedProperty values) =>
			i => (i < values.arraySize)
				? GetCoveringBoxHeight(values.GetArrayElementAtIndex(i)) + ELEMENT_PADDING
				: EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;

		// ── Shared helpers ───────────────────────────────────────────────────────

		// Popup selector for the enum key, plus the "missing" (deleted-from-asset) highlight.
		// Shared by both layout modes so the selection/missing-key behaviour never diverges.
		private static void DrawEnumKeyPopup(Rect rect, SerializedProperty keyProp, EnumAsset asset) {
			string[] names = asset.Instances.Select(i => i.Alias).ToArray();
			int[] ids = asset.Instances.Select(i => i.InternalValue).ToArray();

			int currIdx = System.Array.IndexOf(ids, keyProp.intValue);
			bool missing = currIdx == -1;

			GUI.backgroundColor = missing ? new Color(1f, 0.4f, 0.4f) : Color.white;
			if (missing) {
				var display = names.Prepend($"! MISSING (ID: {keyProp.intValue})").ToArray();
				int next = EditorGUI.Popup(rect, 0, display);
				if (next > 0) keyProp.intValue = ids[next - 1];
			} else {
				keyProp.intValue = ids[EditorGUI.Popup(rect, currIdx, names)];
			}
			GUI.backgroundColor = Color.white;
		}

		// Best-effort label for a collapsed value: type name, plus element count if it's an array/list.
		private static GUIContent GetCollapsedPreview(SerializedProperty prop) {
			string typeName = ObjectNames.NicifyVariableName(prop.type);
			return new GUIContent(prop.isArray ? $"{typeName} [{prop.arraySize}]" : typeName);
		}

		// Walks the direct visible children of a Generic property without ever asking Unity to
		// draw the property's own foldout — that arrow is what we replaced with our own arrow,
		// so drawing the property itself here would give you two arrows for one value.
		private static float GetChildrenHeight(SerializedProperty parent) {
			if (!parent.hasVisibleChildren) return EditorGUIUtility.singleLineHeight;

			SerializedProperty it = parent.Copy();
			SerializedProperty end = it.GetEndProperty();
			float total = 0f;
			bool any = false;

			if (it.NextVisible(true)) {
				while (!SerializedProperty.EqualContents(it, end)) {
					any = true;
					total += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
					if (!it.NextVisible(false)) break;
				}
			}
			return any ? total - EditorGUIUtility.standardVerticalSpacing : EditorGUIUtility.singleLineHeight;
		}

		private static void DrawChildrenFlattened(Rect rect, SerializedProperty parent) {
			SerializedProperty it = parent.Copy();
			SerializedProperty end = it.GetEndProperty();
			float y = rect.y;

			if (!it.NextVisible(true)) return;
			while (!SerializedProperty.EqualContents(it, end)) {
				float h = EditorGUI.GetPropertyHeight(it, true);
				EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, h), it, true);
				y += h + EditorGUIUtility.standardVerticalSpacing;
				if (!it.NextVisible(false)) break;
			}
		}

		private ReorderableList BuildList(SerializedProperty keys, SerializedProperty values) {
			return new ReorderableList(keys.serializedObject, keys, true, true, true, true) {
				onAddCallback = l => {
					int newIdx = l.serializedProperty.arraySize;
					l.serializedProperty.arraySize++;
					values.arraySize = l.serializedProperty.arraySize;

					// Robust path-finding to initialize the new element with a valid key
					string path = l.serializedProperty.propertyPath;
					int lastDot = path.LastIndexOf('.');
					if (lastDot != -1) {
						var parentProp = l.serializedProperty.serializedObject.FindProperty(path.Substring(0, lastDot));
						var asset = parentProp?.FindPropertyRelative(SOURCE_PROP)?.objectReferenceValue as EnumAsset;
						if (asset != null && asset.Instances.Count > 0) {
							l.serializedProperty.GetArrayElementAtIndex(newIdx).intValue = asset.GetDefaultItemId();
						}
					}
				},

				onRemoveCallback = l => {
					int index = l.index;
					if (index >= 0 && index < values.arraySize) {
						// Double delete handles ObjectReferences correctly in Unity arrays
						if (values.GetArrayElementAtIndex(index).propertyType == SerializedPropertyType.ObjectReference) {
							values.DeleteArrayElementAtIndex(index);
						}
						values.DeleteArrayElementAtIndex(index);
					}
					ReorderableList.defaultBehaviours.DoRemoveButton(l);
				},

				onReorderCallbackWithDetails = (_, oldIdx, newIdx) => {
					values.MoveArrayElement(oldIdx, newIdx);
					// Clear the list cache because paths to siblings may have changed
					this._lists.Clear();
				}
			};
		}

		// ── Height Calculation ────────────────────────────────────────────────────

		private static float GetHelpBoxHeight(float width) => EditorStyles.helpBox.CalcHeight(_helpContent, width);

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			var sourceProp = property.FindPropertyRelative(SOURCE_PROP);
			var keysProp = property.FindPropertyRelative(KEYS_PROP);
			var valuesProp = property.FindPropertyRelative(VALUES_PROP);

			float h = EditorGUIUtility.singleLineHeight + SOURCE_GAP;
			if (sourceProp.objectReferenceValue == null)
				return h + 25f + BOTTOM_EXTRA_PADDING;

			bool tableMode = ComputeTableMode(valuesProp);
			int count = keysProp.arraySize;

			float elemsH;
			if (count == 0) {
				elemsH = EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;
			} else if (tableMode) {
				elemsH = Enumerable.Range(0, count).Sum(i =>
					ElementHeight(i < valuesProp.arraySize ? EditorGUI.GetPropertyHeight(valuesProp.GetArrayElementAtIndex(i), true) : EditorGUIUtility.singleLineHeight));
			} else {
				elemsH = Enumerable.Range(0, count).Sum(i =>
					(i < valuesProp.arraySize ? GetCoveringBoxHeight(valuesProp.GetArrayElementAtIndex(i)) : EditorGUIUtility.singleLineHeight) + ELEMENT_PADDING);
			}

			float headerH = tableMode ? HEADER_HEIGHT : 0f;
			return h + headerH + elemsH + FOOTER_HEIGHT + HELP_BOX_GAP + GetHelpBoxHeight(EditorGUIUtility.currentViewWidth) + BOTTOM_EXTRA_PADDING;
		}
	}
}