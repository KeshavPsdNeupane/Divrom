#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Kope.AI.Editor {
	public class KopeCurveEditorWindow : EditorWindow {
		private CurveAsset _target;
		private int _draggingIndex = -1;
		private int _selectedIndex = -1;
		private const float POINT_RADIUS = 7f;
		private const float GRID_PADDING = 30f;

		[MenuItem("Tools/Curve Editor")]
		public static void Open() =>
			GetWindow<KopeCurveEditorWindow>("Kope Curve Editor");

		private void OnGUI() {
			_target = (CurveAsset)EditorGUILayout.ObjectField(
				"Curve Asset", _target, typeof(CurveAsset), false);

			if (_target == null) {
				EditorGUILayout.HelpBox(
					"Create a CurveAsset via Assets > Create > Kope > Curve Asset",
					MessageType.Info);
				return;
			}

			DrawToolbar();
			DrawCurveCanvas();
			DrawInstructions();
		}

		private void DrawToolbar() {
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			_target.resolution = EditorGUILayout.IntSlider(
				"Resolution", _target.resolution, 8, 256);

			if (GUILayout.Button("Bake", EditorStyles.toolbarButton, GUILayout.Width(60))) {
				_target.Bake();
				EditorUtility.SetDirty(_target);
				AssetDatabase.SaveAssets();
				Debug.Log($"[KopeCurve] Baked {_target.resolution} samples.");
			}

			if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(60))) {
				Undo.RecordObject(_target, "Reset Curve");
				_target.controlPoints = new Vector2[]
					{ new Vector2(0f, 0f), new Vector2(1f, 1f) };
				_target.Bake();
				EditorUtility.SetDirty(_target);
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawCurveCanvas() {
			Rect canvasRect = GUILayoutUtility.GetRect(
				position.width, position.height - 90f);

			// Background
			EditorGUI.DrawRect(canvasRect, new Color(0.15f, 0.15f, 0.15f));

			Rect graphRect = new(
				canvasRect.x + GRID_PADDING,
				canvasRect.y + GRID_PADDING,
				canvasRect.width - GRID_PADDING * 2,
				canvasRect.height - GRID_PADDING * 2);

			DrawGrid(graphRect);
			DrawBakedCurve(graphRect);
			DrawControlPointLines(graphRect);
			DrawControlPoints(graphRect);
			HandleInput(canvasRect, graphRect);
		}


		private void DrawGrid(Rect r) {
			Handles.color = new Color(1f, 1f, 1f, 0.08f);
			int divisions = 50;

			for (int i = 0; i <= divisions; i++) {
				float t = (float)i / divisions;
				float x = r.x + t * r.width;
				float y = r.y + t * r.height;
				Handles.DrawLine(new Vector3(x, r.y), new Vector3(x, r.yMax));
				Handles.DrawLine(new Vector3(r.x, y), new Vector3(r.xMax, y));
			}

			// Axes
			Handles.color = new Color(1f, 1f, 1f, 0.25f);
			Handles.DrawLine(new Vector3(r.x, r.yMax), new Vector3(r.xMax, r.yMax));
			Handles.DrawLine(new Vector3(r.x, r.y), new Vector3(r.x, r.yMax));

			// Labels
			GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel) {
				normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
				fontSize = 9,
				alignment = TextAnchor.MiddleCenter
			};

			int labelStep = 4; // label every 0.2
			for (int i = 0; i <= divisions; i += labelStep) {
				float t = (float)i / divisions;
				float val = Mathf.Round(t * 10f) / 10f;

				// X axis labels (bottom)
				float sx = r.x + t * r.width;
				GUI.Label(new Rect(sx - 14f, r.yMax + 2f, 28f, 16f),
					val.ToString("0.0"), labelStyle);

				// Y axis labels (left) — 0 at bottom, 1 at top
				float sy = r.y + (1f - t) * r.height;
				GUI.Label(new Rect(r.x - GRID_PADDING, sy - 8f, GRID_PADDING - 3f, 16f),
					val.ToString("0.0"), labelStyle);
			}
		}

		private void DrawBakedCurve(Rect r) {
			if (_target.sampledValues == null
				|| _target.sampledValues.Length < 2) return;

			Handles.color = new Color(0.4f, 0.8f, 0.6f, 0.9f);
			float[] v = _target.sampledValues;

			Vector3[] pts = new Vector3[v.Length];
			for (int i = 0; i < v.Length; i++) {
				float t = (float)i / (v.Length - 1);
				pts[i] = new Vector3(
					r.x + t * r.width,
					r.y + (1f - Mathf.Clamp01(v[i])) * r.height);
			}
			Handles.DrawAAPolyLine(2.5f, pts);
		}

		private void DrawControlPointLines(Rect r) {
			if (_target.controlPoints == null
				|| _target.controlPoints.Length < 2) return;

			Handles.color = new Color(1f, 1f, 1f, 0.2f);
			for (int i = 0; i < _target.controlPoints.Length - 1; i++) {
				Vector3 a = ToScreen(r, _target.controlPoints[i]);
				Vector3 b = ToScreen(r, _target.controlPoints[i + 1]);
				Handles.DrawDottedLine(a, b, 4f);
			}
		}

		private void DrawControlPoints(Rect r) {
			if (_target.controlPoints == null) return;

			for (int i = 0; i < _target.controlPoints.Length; i++) {
				Vector2 sp = ToScreen(r, _target.controlPoints[i]);
				bool isSelected = i == _selectedIndex;

				Handles.color = isSelected
					? new Color(1f, 0.85f, 0.3f)
					: new Color(0.9f, 0.9f, 0.9f);

				Handles.DrawSolidDisc(sp, Vector3.forward, POINT_RADIUS);
				Handles.color = new Color(0f, 0f, 0f, 0.5f);
				Handles.DrawWireDisc(sp, Vector3.forward, POINT_RADIUS);
			}
		}

		private void HandleInput(Rect canvasRect, Rect graphRect) {
			Event e = Event.current;
			Vector2 mousePos = e.mousePosition;

			if (e.type == EventType.MouseDown && graphRect.Contains(mousePos)) {
				// Check for hitting existing point
				int hit = GetPointAt(graphRect, mousePos);

				if (hit >= 0) {
					if (e.button == 0) {
						_draggingIndex = hit;
						_selectedIndex = hit;
					} else if (e.button == 1 && hit > 0
							   && hit < _target.controlPoints.Length - 1) {
						// Right-click removes non-endpoint points
						Undo.RecordObject(_target, "Remove Curve Point");
						var list = new System.Collections.Generic.List<Vector2>(
							_target.controlPoints);
						list.RemoveAt(hit);
						_target.controlPoints = list.ToArray();
						_target.Bake();
						EditorUtility.SetDirty(_target);
						_selectedIndex = -1;
					}
				} else if (e.button == 0) {
					// Double-click or shift-click to add point
					if (e.clickCount == 2 || e.shift) {
						Undo.RecordObject(_target, "Add Curve Point");
						Vector2 newPt = ToNormalized(graphRect, mousePos);
						newPt.x = Mathf.Clamp01(newPt.x);
						newPt.y = Mathf.Clamp01(newPt.y);

						var list = new System.Collections.Generic.List<Vector2>(
							_target.controlPoints);
						list.Add(newPt);
						list.Sort((a, b) => a.x.CompareTo(b.x));
						_target.controlPoints = list.ToArray();
						_target.Bake();
						EditorUtility.SetDirty(_target);
					}
				}

				e.Use();
				Repaint();
			}

			if (e.type == EventType.MouseDrag && _draggingIndex >= 0) {
				Undo.RecordObject(_target, "Move Curve Point");
				Vector2 newPos = ToNormalized(graphRect, mousePos);

				// Clamp x to stay between neighbours
				float xMin = _draggingIndex > 0
					? _target.controlPoints[_draggingIndex - 1].x + 0.01f : 0f;
				float xMax = _draggingIndex < _target.controlPoints.Length - 1
					? _target.controlPoints[_draggingIndex + 1].x - 0.01f : 1f;

				_target.controlPoints[_draggingIndex] = new Vector2(
					Mathf.Clamp(newPos.x, xMin, xMax),
					Mathf.Clamp01(newPos.y));

				_target.Bake();
				EditorUtility.SetDirty(_target);
				e.Use();
				Repaint();
			}

			if (e.type == EventType.MouseUp) {
				_draggingIndex = -1;
				e.Use();
			}
		}

		private int GetPointAt(Rect r, Vector2 mousePos) {
			for (int i = 0; i < _target.controlPoints.Length; i++) {
				Vector2 sp = ToScreen(r, _target.controlPoints[i]);
				if (Vector2.Distance(sp, mousePos) <= POINT_RADIUS + 3f)
					return i;
			}
			return -1;
		}

		private Vector2 ToScreen(Rect r, Vector2 normalizedPt) =>
			new Vector2(
				r.x + normalizedPt.x * r.width,
				r.y + (1f - normalizedPt.y) * r.height);

		private Vector2 ToNormalized(Rect r, Vector2 screenPt) =>
			new Vector2(
				(screenPt.x - r.x) / r.width,
				1f - (screenPt.y - r.y) / r.height);

		private void DrawInstructions() {
			EditorGUILayout.HelpBox(
				"Double-click or Shift+click: add point  |  " +
				"Drag: move point  |  Right-click: remove point  |  " +
				"Always Bake after editing.",
				MessageType.None);
		}
	}
}
#endif