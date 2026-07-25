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
	///
	/// One foldout on the dictionary field itself (universal — hides the whole list).
	/// Below that, one of two layouts is picked automatically per-dictionary:
	///
	///  - TABLE mode: used when every key AND every value fits on a single line. Renders a
	///    literal two-column "Key | Value" table with a header row and grid lines.
	///
	///  - COVERING mode: used as soon as any key or value needs more than one line. Each
	///    element becomes its own bordered box containing a Key row and a Value row, stacked.
	///    Any row whose field is itself foldable (or otherwise tall) gets its own arrow —
	///    exactly the same foldout pattern as the dictionary-level header — which toggles that
	///    field's nested serialized children independently of its sibling. This nests fine:
	///    the flattened children are drawn with ordinary PropertyField calls, so a value that is
	///    itself a SerializableDictionary&lt;,&gt; recurses back into this same drawer.
	///
	/// PAGINATION: dictionaries with more than PAGE_SIZE entries are split into pages of
	/// PAGE_SIZE rows. Only the current page's key/value properties are ever measured or drawn
	/// — Unity's SerializedProperty height/draw calls are the expensive part of this drawer, so
	/// this is what keeps a dictionary with hundreds or thousands of entries responsive. A
	/// toolbar (prev/next buttons, a type-a-page-number field, and a "showing X–Y of Z"
	/// readout) is inserted between the dictionary's foldout header and its rows whenever
	/// pagination is active. Because only the visible page is ever inspected, TABLE vs.
	/// COVERING mode is decided per-page rather than once for the whole dictionary — a huge
	/// dictionary can render as a table on one page and switch to covering mode on another if
	/// tall elements happen to land there.
	/// </summary>
	[CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
	public class SerializableDictionaryDrawer : PropertyDrawer {
		private const float ROW_SPACING = 4f;
		private const float ELEMENT_PADDING = 4f;
		private const float COL_GAP = 6f;
		private const float MIN_KEY_WIDTH = 80f;
		private const float DEFAULT_KEY_RATIO = 0.35f;
		private const float STACK_THRESHOLD = 2.5f;
		private const float FOLDOUT_WIDTH = 14f;

		// Odin-style boxed group metrics
		private const float BOX_SIDE_MARGIN = 4f;   // left/right inset for header content, inside the box border
		private const float BOX_HEADER_PAD_V = 3f;  // top/bottom padding around a header row within its strip
		private const float BOX_SEPARATOR_H = 1f;   // header/body divider line thickness
		private const float BOX_BODY_PAD_TOP = 5f;
		private const float BOX_BODY_PAD_BOTTOM = 5f;
		private const float BOX_BODY_PAD_H = 8f;    // left/right inset for body content within a box

		// Approximate left inset ReorderableList reserves for its drag handle. Unity doesn't
		// expose this, so we replicate it only to keep our own hand-drawn table column header
		// visually aligned with the row content the list draws beneath it.
		private const float LIST_CONTENT_INDENT = 20f;

		// ── Pagination ───────────────────────────────────────────────────────────
		private const int PAGE_SIZE = 20;
		private const float PAGE_NAV_BUTTON_WIDTH = 24f;
		private const float PAGE_FIELD_WIDTH = 40f;

		private static float PagerHeight => EditorGUIUtility.singleLineHeight + BOX_HEADER_PAD_V * 2f;

		// Current 0-based page per dictionary field, keyed by property path. Self-corrects via
		// GetOrClampPage — no domain-reload/owner validation needed since a stale value just
		// gets clamped into range (or back to 0) the next time it's read.
		private readonly Dictionary<string, int> _pages = new();

		// Cache per-path to handle multiple dictionaries or deeply nested dictionaries correctly.
		// Stores the owning SerializedObject alongside the list (rather than relying on
		// list.serializedProperty, which is no longer set — see BuildList) so we can still
		// detect and discard stale entries after a domain reload or a selection change.
		private sealed class CachedList {
			public ReorderableList List;
			public SerializedObject Owner;
		}
		private readonly Dictionary<string, CachedList> _lists = new();

		private static GUIStyle _collapsedPreviewStyle;
		private static GUIStyle CollapsedPreviewStyle => _collapsedPreviewStyle ??= new GUIStyle(EditorStyles.miniLabel) {
			fontStyle = FontStyle.Italic,
			alignment = TextAnchor.MiddleRight,
		};

		private static Color SeparatorColor => EditorGUIUtility.isProSkin
			? new Color(1f, 1f, 1f, 0.08f)
			: new Color(0f, 0f, 0f, 0.12f);

		private ReorderableList GetList(SerializedProperty property, SerializedProperty keysProp, SerializedProperty valuesProp) {
			string path = property.propertyPath;

			if (_lists.TryGetValue(path, out CachedList cached) && cached.Owner == property.serializedObject)
				return cached.List;

			ReorderableList list = BuildList(property, keysProp, valuesProp);
			_lists[path] = new CachedList { List = list, Owner = property.serializedObject };
			return list;
		}

		private static bool IsStacked(float valueHeight) => valueHeight > EditorGUIUtility.singleLineHeight * STACK_THRESHOLD;

		// A value is "foldable" if it's a plain class/struct with its own fields — the only
		// case where we can safely flatten-draw children ourselves instead of the value's
		// normal rendering.
		private static bool IsFoldable(SerializedProperty prop) =>
			prop.propertyType == SerializedPropertyType.Generic && prop.hasVisibleChildren;

		// Anything that needs more than one line gets its own arrow — foldable structs AND tall
		// non-foldable things (AnimationCurve, Gradient, a custom-drawn struct with no visible
		// children of its own). Applies equally to keys and values.
		private static bool NeedsCollapseArrow(SerializedProperty prop) {
			if (IsFoldable(prop)) return true;
			return IsStacked(EditorGUI.GetPropertyHeight(prop, true));
		}

		private static float CalculateKeyWidth(float totalWidth) {
			return Mathf.Max(MIN_KEY_WIDTH, totalWidth * DEFAULT_KEY_RATIO);
		}

		// TABLE mode is only valid when every row's key AND value on the CURRENT PAGE are simple
		// one-liners. Deliberately scoped to [pageStart, pageStart + pageCount) rather than the
		// whole array — scanning every element of a 1000-row dictionary just to pick a layout
		// would undo the entire point of paginating. See class doc comment for the trade-off.
		private static bool ComputeTableMode(SerializedProperty keysProp, SerializedProperty valuesProp, int pageStart, int pageCount) {
			int limit = Mathf.Min(pageStart + pageCount, Mathf.Min(keysProp.arraySize, valuesProp.arraySize));
			for (int i = pageStart; i < limit; i++) {
				SerializedProperty k = keysProp.GetArrayElementAtIndex(i);
				SerializedProperty v = valuesProp.GetArrayElementAtIndex(i);
				if (NeedsCollapseArrow(k) || NeedsCollapseArrow(v))
					return false;
			}
			return true;
		}

		private static int GetTotalPages(int totalCount) => totalCount <= 0 ? 1 : Mathf.CeilToInt(totalCount / (float)PAGE_SIZE);

		// Reads the stored page for this field, clamps it into range for the current element
		// count (writing the clamped value back), and reports the total page count.
		private int GetOrClampPage(string path, int totalCount, out int totalPages) {
			totalPages = GetTotalPages(totalCount);
			int page = _pages.TryGetValue(path, out int cachedPage) ? cachedPage : 0;
			page = Mathf.Clamp(page, 0, totalPages - 1);
			_pages[path] = page;
			return page;
		}

		private static object[] BuildPageBackingList(int pageCount) => new object[Mathf.Max(pageCount, 0)];

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			EditorGUI.BeginProperty(position, label, property);

			SerializedProperty keysProp = property.FindPropertyRelative("keys");
			SerializedProperty valuesProp = property.FindPropertyRelative("values");

			if (keysProp == null || valuesProp == null) {
				EditorGUI.LabelField(position, label.text, "keys/values fields not found — is this a SerializableDictionary<,>?");
				EditorGUI.EndProperty();
				return;
			}

			// Dict-level foldout: owns the whole header row. Collapsing this hides every
			// key/value row entirely — nothing below it gets drawn or measured.
			Rect headerRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			string headerText = $"{property.displayName} ({keysProp.arraySize})";
			property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, headerText, true, EditorStyles.foldoutHeader);

			if (property.isExpanded) {
				HashSet<int> conflicts = FindConflictedIndices(property);
				ReorderableList list = GetList(property, keysProp, valuesProp);
				string path = property.propertyPath;

				int totalCount = keysProp.arraySize;
				bool paginated = totalCount > PAGE_SIZE;
				int currentPage = GetOrClampPage(path, totalCount, out int totalPages);
				int pageStart = paginated ? currentPage * PAGE_SIZE : 0;
				int pageCount = paginated ? Mathf.Min(PAGE_SIZE, totalCount - pageStart) : totalCount;

				bool tableMode = true;

				// Freshly (re)binds the list's backing size, mode, and callbacks to the given
				// page offset. Pulled into a local function because a pagination button click
				// needs to re-run this mid-OnGUI, immediately, so the rest of this call draws
				// the newly selected page instead of lagging a frame behind.
				void RebindList(int ps, int pc) {
					tableMode = ComputeTableMode(keysProp, valuesProp, ps, pc);
					list.list = BuildPageBackingList(pc);
					list.drawElementCallback = tableMode
						? MakeTableDrawElementCallback(keysProp, valuesProp, conflicts, property, ps)
						: MakeCoveringDrawElementCallback(keysProp, valuesProp, conflicts, property, ps);
					list.elementHeightCallback = tableMode
						? MakeTableElementHeightCallback(keysProp, valuesProp, ps)
						: MakeCoveringElementHeightCallback(keysProp, valuesProp, ps);
				}

				RebindList(pageStart, pageCount);

				float y = headerRect.yMax + ROW_SPACING;

				if (paginated) {
					Rect pagerRect = new(position.x, y, position.width, PagerHeight);
					int newPage = DrawPaginationBar(pagerRect, currentPage, totalPages, pageStart, pageCount, totalCount);
					if (newPage != currentPage) {
						currentPage = newPage;
						_pages[path] = currentPage;
						pageStart = currentPage * PAGE_SIZE;
						pageCount = Mathf.Min(PAGE_SIZE, totalCount - pageStart);
						RebindList(pageStart, pageCount);
					}
					y = pagerRect.yMax + ROW_SPACING;
				}

				if (tableMode && pageCount > 0) {
					float colHeaderHeight = EditorGUIUtility.singleLineHeight + BOX_HEADER_PAD_V * 2f;
					Rect colHeaderRect = new(position.x, y, position.width, colHeaderHeight);
					float keyWidth = CalculateKeyWidth(position.width - LIST_CONTENT_INDENT);
					DrawTableColumnHeader(colHeaderRect, keyWidth);
					y = colHeaderRect.yMax + ROW_SPACING;
				}

				Rect listRect = new(position.x, y, position.width, position.yMax - y);
				list.DoList(listRect);
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			SerializedProperty keysProp = property.FindPropertyRelative("keys");
			SerializedProperty valuesProp = property.FindPropertyRelative("values");

			if (keysProp == null || valuesProp == null)
				return EditorGUIUtility.singleLineHeight;

			if (!property.isExpanded)
				return EditorGUIUtility.singleLineHeight;

			int totalCount = keysProp.arraySize;
			bool paginated = totalCount > PAGE_SIZE;
			int currentPage = GetOrClampPage(property.propertyPath, totalCount, out int totalPages);
			int pageStart = paginated ? currentPage * PAGE_SIZE : 0;
			int pageCount = paginated ? Mathf.Min(PAGE_SIZE, totalCount - pageStart) : totalCount;

			bool tableMode = ComputeTableMode(keysProp, valuesProp, pageStart, pageCount);

			ReorderableList list = GetList(property, keysProp, valuesProp);
			// The list may not have been drawn yet this frame (GetPropertyHeight commonly runs
			// before OnGUI) — assign the backing size and height callback here too so GetHeight()
			// below always reflects the page/mode we just computed rather than a stale one.
			list.list = BuildPageBackingList(pageCount);
			list.elementHeightCallback = tableMode
				? MakeTableElementHeightCallback(keysProp, valuesProp, pageStart)
				: MakeCoveringElementHeightCallback(keysProp, valuesProp, pageStart);

			float pagerBlock = paginated ? (PagerHeight + ROW_SPACING) : 0f;

			float columnHeaderBlock = 0f;
			if (tableMode && pageCount > 0) {
				columnHeaderBlock = EditorGUIUtility.singleLineHeight + BOX_HEADER_PAD_V * 2f + ROW_SPACING;
			}

			return EditorGUIUtility.singleLineHeight + ROW_SPACING + pagerBlock + columnHeaderBlock + list.GetHeight();
		}

		// ── Pagination bar ───────────────────────────────────────────────────────

		// Draws "[<] Page [__] / N  showing a–b of total [>]" and returns the (possibly
		// user-changed) current page, 0-based. Bounds checking on the typed page number happens
		// here: whatever the user enters is clamped into [1, totalPages] before being applied.
		private static int DrawPaginationBar(Rect rect, int currentPage, int totalPages, int pageStart, int pageCount, int totalCount) {
			GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

			Rect inner = new(rect.x + BOX_SIDE_MARGIN, rect.y, rect.width - BOX_SIDE_MARGIN * 2f, rect.height);

			Rect prevRect = new(inner.x, inner.y, PAGE_NAV_BUTTON_WIDTH, inner.height);
			Rect nextRect = new(inner.xMax - PAGE_NAV_BUTTON_WIDTH, inner.y, PAGE_NAV_BUTTON_WIDTH, inner.height);

			using (new EditorGUI.DisabledScope(currentPage <= 0)) {
				if (GUI.Button(prevRect, "<")) currentPage--;
			}
			using (new EditorGUI.DisabledScope(currentPage >= totalPages - 1)) {
				if (GUI.Button(nextRect, ">")) currentPage++;
			}

			GUIContent pageLabel = new("Page");
			GUIContent ofLabel = new($"/ {totalPages}");
			float pageLabelW = EditorStyles.miniLabel.CalcSize(pageLabel).x + 4f;
			float ofLabelW = EditorStyles.miniLabel.CalcSize(ofLabel).x + 4f;

			float midX = prevRect.xMax + COL_GAP;
			Rect pageLabelRect = new(midX, inner.y, pageLabelW, inner.height);
			EditorGUI.LabelField(pageLabelRect, pageLabel, EditorStyles.miniLabel);

			Rect fieldRect = new(pageLabelRect.xMax, inner.y + 1f, PAGE_FIELD_WIDTH, EditorGUIUtility.singleLineHeight);
			EditorGUI.BeginChangeCheck();
			int typedPage = EditorGUI.DelayedIntField(fieldRect, currentPage + 1);
			if (EditorGUI.EndChangeCheck()) {
				// Internal bounds check: whatever the user typed gets clamped into range rather
				// than jumping to an out-of-bounds page.
				currentPage = Mathf.Clamp(typedPage, 1, totalPages) - 1;
			}

			Rect ofLabelRect = new(fieldRect.xMax + 2f, inner.y, ofLabelW, inner.height);
			EditorGUI.LabelField(ofLabelRect, ofLabel, EditorStyles.miniLabel);

			float rangeX = ofLabelRect.xMax + COL_GAP;
			float rangeW = nextRect.x - COL_GAP - rangeX;
			if (rangeW > 20f) {
				string rangeText = totalCount > 0
					? $"showing {pageStart + 1}\u2013{pageStart + pageCount} of {totalCount}"
					: "no entries";
				Rect rangeRect = new(rangeX, inner.y, rangeW, inner.height);
				EditorGUI.LabelField(rangeRect, rangeText, CollapsedPreviewStyle);
			}

			return currentPage;
		}

		// ── TABLE mode ───────────────────────────────────────────────────────────

		private static void DrawTableColumnHeader(Rect rect, float keyWidth) {
			GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

			Rect keyLabelRect = new(rect.x + LIST_CONTENT_INDENT, rect.y, keyWidth, rect.height);
			EditorGUI.LabelField(keyLabelRect, "Key", EditorStyles.miniBoldLabel);

			float valX = rect.x + LIST_CONTENT_INDENT + keyWidth + COL_GAP;
			float valW = rect.width - LIST_CONTENT_INDENT - keyWidth - COL_GAP - BOX_SIDE_MARGIN;
			Rect valueLabelRect = new(valX, rect.y, valW, rect.height);
			EditorGUI.LabelField(valueLabelRect, "Value", EditorStyles.miniBoldLabel);

			Rect divider = new(rect.x + LIST_CONTENT_INDENT + keyWidth + COL_GAP * 0.5f - BOX_SEPARATOR_H * 0.5f, rect.y, BOX_SEPARATOR_H, rect.height);
			EditorGUI.DrawRect(divider, SeparatorColor);
		}

		// pageStart offsets the ReorderableList's local row index (0..pageCount-1) into the
		// dictionary's actual keys/values arrays.
		private static ReorderableList.ElementHeightCallbackDelegate MakeTableElementHeightCallback(SerializedProperty keysProp, SerializedProperty valuesProp, int pageStart) {
			return localIndex => {
				int index = pageStart + localIndex;
				if (index >= keysProp.arraySize || index >= valuesProp.arraySize)
					return EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;

				SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(index);
				SerializedProperty valProp = valuesProp.GetArrayElementAtIndex(index);
				float kH = EditorGUI.GetPropertyHeight(keyProp, true);
				float vH = EditorGUI.GetPropertyHeight(valProp, true);
				return Mathf.Max(kH, vH) + ELEMENT_PADDING;
			};
		}

		private static ReorderableList.ElementCallbackDelegate MakeTableDrawElementCallback(
			SerializedProperty keysProp, SerializedProperty valuesProp, HashSet<int> conflicts, SerializedProperty dictProperty, int pageStart) {
			return (rect, localIndex, active, focused) => {
				int index = pageStart + localIndex;
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
				float kw = CalculateKeyWidth(rect.width);

				EditorGUI.BeginChangeCheck();

				Rect keyRect = new(rect.x, rect.y, kw, keyHeight);
				EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);

				float valX = rect.x + kw + COL_GAP;
				float valW = rect.width - kw - COL_GAP;
				Rect valRect = new(valX, rect.y, valW, valHeight);
				EditorGUI.PropertyField(valRect, valProp, GUIContent.none, true);

				if (EditorGUI.EndChangeCheck()) {
					dictProperty.serializedObject.ApplyModifiedProperties();
				}

				// Grid lines: vertical divider between columns, horizontal divider under the
				// row — turns the flat side-by-side layout into an actual-looking table.
				float rowH = Mathf.Max(keyHeight, valHeight);
				Rect vDivider = new(rect.x + kw + COL_GAP * 0.5f - BOX_SEPARATOR_H * 0.5f, rect.y, BOX_SEPARATOR_H, rowH);
				EditorGUI.DrawRect(vDivider, SeparatorColor);

				Rect hDivider = new(rect.x - 4f, rect.y + rowH + (ELEMENT_PADDING * 0.5f) - 1f, rect.width + 8f, BOX_SEPARATOR_H);
				EditorGUI.DrawRect(hDivider, SeparatorColor);
			};
		}

		// ── COVERING mode ────────────────────────────────────────────────────────

		// Draws a single key or value field within a covering-mode row. Short/simple properties
		// render as a normal labeled field. Foldable or tall properties get their own arrow —
		// clicking it reveals that field's nested serialized children directly beneath, fully
		// independent of the sibling field's (key's or value's) expand state.
		private static void DrawFieldRow(Rect rect, SerializedProperty prop, string label) {
			if (!NeedsCollapseArrow(prop)) {
				EditorGUI.PropertyField(rect, prop, new GUIContent(label), true);
				return;
			}

			float headerLine = EditorGUIUtility.singleLineHeight;
			Rect headerRect = new(rect.x, rect.y, rect.width, headerLine);

			string preview = GetCollapsedPreview(prop).text;
			EditorGUI.BeginChangeCheck();
			bool expanded = EditorGUI.Foldout(headerRect, prop.isExpanded, $"{label}  ({preview})", true);
			if (EditorGUI.EndChangeCheck()) {
				prop.isExpanded = expanded;
			}

			if (!prop.isExpanded) return;

			float sepY = headerRect.yMax;
			Rect sepRect = new(rect.x, sepY, rect.width, BOX_SEPARATOR_H);
			EditorGUI.DrawRect(sepRect, SeparatorColor);

			float bodyY = sepY + BOX_SEPARATOR_H + BOX_BODY_PAD_TOP;
			Rect bodyRect = new(rect.x + BOX_BODY_PAD_H, bodyY, rect.width - BOX_BODY_PAD_H, rect.yMax - bodyY);

			if (IsFoldable(prop)) {
				DrawChildrenFlattened(bodyRect, prop);
			} else {
				bodyRect.height = EditorGUI.GetPropertyHeight(prop, true);
				EditorGUI.PropertyField(bodyRect, prop, GUIContent.none, true);
			}
		}

		// Single source of truth for one field's row height — mirrors DrawFieldRow's branching
		// exactly so the height callback and the draw callback can never disagree.
		private static float GetFieldRowHeight(SerializedProperty prop) {
			if (!NeedsCollapseArrow(prop))
				return EditorGUI.GetPropertyHeight(prop, true);

			float headerLine = EditorGUIUtility.singleLineHeight;
			if (!prop.isExpanded)
				return headerLine;

			float bodyHeight = IsFoldable(prop) ? GetChildrenHeight(prop) : EditorGUI.GetPropertyHeight(prop, true);
			return headerLine + BOX_SEPARATOR_H + BOX_BODY_PAD_TOP + bodyHeight + BOX_BODY_PAD_BOTTOM;
		}

		private static float GetCoveringBoxHeight(SerializedProperty keyProp, SerializedProperty valProp) {
			float keySection = GetFieldRowHeight(keyProp) + BOX_HEADER_PAD_V * 2f;
			float valSection = GetFieldRowHeight(valProp) + BOX_HEADER_PAD_V * 2f;
			return keySection + BOX_SEPARATOR_H + valSection;
		}

		private static ReorderableList.ElementHeightCallbackDelegate MakeCoveringElementHeightCallback(SerializedProperty keysProp, SerializedProperty valuesProp, int pageStart) {
			return localIndex => {
				int index = pageStart + localIndex;
				if (index >= keysProp.arraySize || index >= valuesProp.arraySize)
					return EditorGUIUtility.singleLineHeight + ELEMENT_PADDING;

				SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(index);
				SerializedProperty valProp = valuesProp.GetArrayElementAtIndex(index);
				return GetCoveringBoxHeight(keyProp, valProp) + ELEMENT_PADDING;
			};
		}

		private static ReorderableList.ElementCallbackDelegate MakeCoveringDrawElementCallback(
			SerializedProperty keysProp, SerializedProperty valuesProp, HashSet<int> conflicts, SerializedProperty dictProperty, int pageStart) {
			return (rect, localIndex, active, focused) => {
				int index = pageStart + localIndex;
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

				GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

				EditorGUI.BeginChangeCheck();

				float keyRowHeight = GetFieldRowHeight(keyProp);
				Rect keyRect = new(rect.x + BOX_SIDE_MARGIN, rect.y + BOX_HEADER_PAD_V, rect.width - BOX_SIDE_MARGIN * 2f, keyRowHeight);
				DrawFieldRow(keyRect, keyProp, "Key");

				float sepY = keyRect.yMax + BOX_HEADER_PAD_V;
				Rect sepRect = new(rect.x + 1f, sepY, rect.width - 2f, BOX_SEPARATOR_H);
				EditorGUI.DrawRect(sepRect, SeparatorColor);

				float valRowHeight = GetFieldRowHeight(valProp);
				Rect valRect = new(rect.x + BOX_SIDE_MARGIN, sepY + BOX_SEPARATOR_H + BOX_HEADER_PAD_V, rect.width - BOX_SIDE_MARGIN * 2f, valRowHeight);
				DrawFieldRow(valRect, valProp, "Value");

				if (EditorGUI.EndChangeCheck()) {
					dictProperty.serializedObject.ApplyModifiedProperties();
				}
			};
		}

		// ── Shared helpers ───────────────────────────────────────────────────────

		// Best-effort label for a collapsed field: type name, plus element count if it's an array/list.
		private static GUIContent GetCollapsedPreview(SerializedProperty prop) {
			string typeName = ObjectNames.NicifyVariableName(prop.type);
			return new GUIContent(prop.isArray ? $"{typeName} [{prop.arraySize}]" : typeName);
		}

		// Walks the direct visible children of a Generic property without ever asking Unity to
		// draw the property's own foldout — that arrow is what we replaced with our own arrow,
		// so drawing the property itself here would give you two arrows for one field.
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

		private ReorderableList BuildList(SerializedProperty dictProperty, SerializedProperty keysProp, SerializedProperty valuesProp) {
			// NOT bound to a SerializedProperty. When a ReorderableList is constructed that way,
			// `list.count` always reports the property's full arraySize, which would force it to
			// measure/draw every element regardless of how big a "page" we want. Binding instead
			// to a plain placeholder IList — resized to the current page's row count before every
			// DoList/GetHeight call (see RebindList in OnGUI and GetPropertyHeight) — lets us
			// fully control how many rows it thinks it has. Every actual data read/write below
			// still goes straight through keysProp/valuesProp using explicit indices, so this
			// placeholder's contents are never touched or relied upon.
			var list = new ReorderableList(BuildPageBackingList(0), typeof(object), true, false, true, true);
			list.headerHeight = 0f;

			string path = dictProperty.propertyPath;

			// FIX: Lock step array sizing to prevent mismatch frames
			list.onAddCallback = l => {
				int newSize = keysProp.arraySize + 1;

				// Force sizes to match simultaneously
				keysProp.arraySize = newSize;
				valuesProp.arraySize = newSize;

				// Optional: Clean up default initialization values if needed
				SerializedProperty newVal = valuesProp.GetArrayElementAtIndex(newSize - 1);

				// Reset the value to 0f for floats to prevent copying previous elements
				if (newVal.propertyType == SerializedPropertyType.Float) {
					newVal.floatValue = 0f;
				}

				dictProperty.serializedObject.ApplyModifiedProperties();

				// Jump to whichever page now holds the freshly added row so it's visible
				// immediately instead of silently landing off-screen on a big dictionary.
				_pages[path] = GetTotalPages(newSize) - 1;
			};

			// FIX: Lock step removal 
			list.onRemoveCallback = l => {
				// l.index is local to the CURRENT PAGE (the list's backing IList only ever
				// contains that page's placeholder entries) — translate it into an absolute
				// index before touching keysProp/valuesProp.
				int currentPage = GetOrClampPage(path, keysProp.arraySize, out int _);
				int pageStart = keysProp.arraySize > PAGE_SIZE ? currentPage * PAGE_SIZE : 0;

				int index = l.index >= 0 ? l.index + pageStart : keysProp.arraySize - 1;
				if (index < 0 || index >= keysProp.arraySize) return;

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

				// The page we were on may no longer exist (e.g. we removed the last row on the
				// last page) — clamp back into range for whatever's left.
				_pages[path] = Mathf.Clamp(currentPage, 0, GetTotalPages(keysProp.arraySize) - 1);
			};

			list.onReorderCallbackWithDetails = (_, oldIndex, newIndex) => {
				// Since the list is no longer bound to keysProp directly (see note above),
				// Unity no longer auto-reorders keysProp for us on drag — we now move both
				// arrays ourselves. Dragging is visually confined to rows on the current page,
				// so oldIndex/newIndex are both local to that page; offset them the same way.
				int currentPage = GetOrClampPage(path, keysProp.arraySize, out int _);
				int pageStart = keysProp.arraySize > PAGE_SIZE ? currentPage * PAGE_SIZE : 0;

				int actualOld = oldIndex + pageStart;
				int actualNew = newIndex + pageStart;

				if (actualOld < keysProp.arraySize && actualNew < keysProp.arraySize) {
					keysProp.MoveArrayElement(actualOld, actualNew);
				}
				if (actualOld < valuesProp.arraySize && actualNew < valuesProp.arraySize) {
					valuesProp.MoveArrayElement(actualOld, actualNew);
				}
				dictProperty.serializedObject.ApplyModifiedProperties();
				_lists.Clear();
			};

			return list;
		}

		// ── Reflection Path Utilities ─────────────────────────────────────────────

		// Still scans the WHOLE dictionary (not just the current page) — duplicate-key detection
		// can't be limited to one page without missing conflicts between pages. This is much
		// cheaper than the GUI height/draw calls the pagination above is protecting against
		// (plain reflection + hashcode comparisons over the runtime list, no SerializedProperty
		// traversal), so it doesn't reintroduce the perf problem pagination solves.
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