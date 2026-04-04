#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Kope.AI.Editor {
	public class KopeCurveEditorWindow : EditorWindow {
		private CurveAsset _target;
		private int _dragIdx = -1;
		private int _dragType = 0; // 0: Main, 1: In, 2: Out
		private Vector2 _viewPan = new Vector2(50, 50);
		private float _zoom = 1f;
		private bool _showBakePreview = true;

		[MenuItem("Tools/Kope AI Curve Master")]
		public static void Open() => GetWindow<KopeCurveEditorWindow>("AI Curve Master");

		private void OnGUI() {
			_target = (CurveAsset)EditorGUILayout.ObjectField("AI Asset", _target, typeof(CurveAsset), false);
			if (!_target) {
				EditorGUILayout.HelpBox("Assign a CurveAsset to begin shaping AI behavior.", MessageType.Info);
				return;
			}

			DrawTopToolbar();

			// Layout accounting for toolbar and status bar
			Rect canvas = new Rect(0, 40, position.width, position.height - 65);
			DrawCanvas(canvas);

			DrawBottomStatus();
		}

		private void DrawTopToolbar() {
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			_target.resolution = EditorGUILayout.IntSlider("Bake Res", _target.resolution, 8, 128);
			_showBakePreview = GUILayout.Toggle(_showBakePreview, "Preview Bake", EditorStyles.toolbarButton);

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Flip Horizontal", EditorStyles.toolbarButton)) FlipCurve(true);
			if (GUILayout.Button("Flip Vertical", EditorStyles.toolbarButton)) FlipCurve(false);

			// Manual Bake Button - Updates timestamp and saves to disk
			if (GUILayout.Button("Bake & Save", EditorStyles.toolbarButton, GUILayout.Width(80))) {
				_target.Bake(isAuto: false); // false updates the timestamp string
				EditorUtility.SetDirty(_target);
				AssetDatabase.SaveAssets();
				ShowNotification(new GUIContent("AI Logic Committed & Saved!"));
			}
			EditorGUILayout.EndHorizontal();
		}

		private void DrawCanvas(Rect canvas) {
			EditorGUI.DrawRect(canvas, new Color(0.12f, 0.12f, 0.13f));
			HandleZoomPan(canvas);

			float size = Mathf.Min(canvas.width, canvas.height) * 0.85f * _zoom;
			Rect graph = new Rect(_viewPan.x, _viewPan.y, size, size);

			GUI.BeginGroup(canvas);
			Rect localG = new Rect(graph.position - canvas.position, graph.size);

			DrawGrid(localG);

			// The ghostly green dots showing the actual baked resolution
			if (_showBakePreview) DrawBakedVisual(localG);

			DrawBezierPath(localG);
			DrawPointsAndHandles(localG);

			// Handle Rectangle (The 0-1 bounds)
			Handles.color = new Color(0.2f, 1f, 0.6f, 0.2f);
			DrawWireRectangle(localG);

			HandleInput(localG);

			GUI.EndGroup();
		}

		private void DrawBakedVisual(Rect r) {
			if (_target.sampledValues == null || _target.sampledValues.Length < 2) return;
			Handles.color = new Color(0.2f, 1f, 0.5f, 0.3f);

			for (int i = 0; i < _target.sampledValues.Length; i++) {
				Vector2 p = ToScreen(r, new Vector2((float)i / (_target.sampledValues.Length - 1), _target.sampledValues[i]));
				Handles.DrawSolidDisc(p, Vector3.forward, 2f);
			}
		}

		private void DrawBezierPath(Rect r) {
			Handles.color = new Color(0.3f, 0.6f, 1f, 1f);
			for (int i = 0; i < _target.points.Length - 1; i++) {
				BezierPoint p0 = _target.points[i];
				BezierPoint p1 = _target.points[i + 1];
				Handles.DrawBezier(
					ToScreen(r, p0.pos),
					ToScreen(r, p1.pos),
					ToScreen(r, p0.pos + p0.hOut),
					ToScreen(r, p1.pos + p1.hIn),
					Handles.color, null, 3f
				);
			}
		}

		private void DrawPointsAndHandles(Rect r) {
			foreach (var p in _target.points) {
				Vector2 sPos = ToScreen(r, p.pos);
				Vector2 sIn = ToScreen(r, p.pos + p.hIn);
				Vector2 sOut = ToScreen(r, p.pos + p.hOut);

				Handles.color = new Color(1, 1, 1, 0.2f);
				Handles.DrawLine(sPos, sIn);
				Handles.DrawLine(sPos, sOut);

				Handles.color = Color.white;
				Handles.DrawSolidDisc(sPos, Vector3.forward, 4f);
				Handles.color = new Color(0.4f, 1f, 1f);
				Handles.DrawSolidDisc(sIn, Vector3.forward, 3f);
				Handles.DrawSolidDisc(sOut, Vector3.forward, 3f);
			}
		}

		private void FlipCurve(bool horizontal) {
			Undo.RecordObject(_target, "Flip Curve");
			for (int i = 0; i < _target.points.Length; i++) {
				if (horizontal) {
					_target.points[i].pos.x = 1f - _target.points[i].pos.x;
					_target.points[i].hIn.x *= -1;
					_target.points[i].hOut.x *= -1;
				} else {
					_target.points[i].pos.y = 1f - _target.points[i].pos.y;
					_target.points[i].hIn.y *= -1;
					_target.points[i].hOut.y *= -1;
				}
			}
			_target.Bake(isAuto: true);
		}

		private void DrawBottomStatus() {
			Rect r = new(0, position.height - 25, position.width, 25);
			EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f));

			bool needsBake = _target.sampledValues == null || _target.sampledValues.Length != _target.resolution;
			string status = needsBake ? "<color=#ffcc00>PENDING BAKE</color>" : "<color=#55ff55>SYNCED</color>";

			GUIStyle statusStyle = new(EditorStyles.miniLabel) { richText = true };
			string info = $"Nodes: {_target.points.Length} | Last Manual Bake: {_target.lastBakeTime} | {status}"
						+ "     <color=#aaaaaa>|  Double-click: add point  |  Drag point: move  |  Right-click point: remove  |  Alt+drag handle: break tangent  |  Scroll: zoom  |  Middle-drag: pan</color>";
			GUI.Label(r, $"  {info}", statusStyle);
		}

		private void DrawWireRectangle(Rect r) {
			Vector3[] corners = new Vector3[] {
				new Vector3(r.x, r.y, 0),
				new Vector3(r.xMax, r.y, 0),
				new Vector3(r.xMax, r.yMax, 0),
				new Vector3(r.x, r.yMax, 0),
				new Vector3(r.x, r.y, 0)
			};
			Handles.DrawPolyLine(corners);
		}

		private void DrawGrid(Rect r) {
			Handles.color = new Color(1, 1, 1, 0.05f);
			for (int i = 0; i <= 10; i++) {
				float t = i / 10f;
				Handles.DrawLine(new Vector2(r.x + t * r.width, 0), new Vector2(r.x + t * r.width, position.height));
				Handles.DrawLine(new Vector2(0, r.y + t * r.height), new Vector2(position.width, r.y + t * r.height));
			}
		}

		private void HandleInput(Rect r) {
			Event e = Event.current;
			if (e.type == EventType.MouseDown && e.button == 0) {
				_dragIdx = -1;
				for (int i = 0; i < _target.points.Length; i++) {
					if (Vector2.Distance(e.mousePosition, ToScreen(r, _target.points[i].pos)) < 10f) { _dragIdx = i; _dragType = 0; break; }
					if (Vector2.Distance(e.mousePosition, ToScreen(r, _target.points[i].pos + _target.points[i].hIn)) < 8f) { _dragIdx = i; _dragType = 1; break; }
					if (Vector2.Distance(e.mousePosition, ToScreen(r, _target.points[i].pos + _target.points[i].hOut)) < 8f) { _dragIdx = i; _dragType = 2; break; }
				}
				if (_dragIdx == -1 && e.clickCount == 2) {
					Undo.RecordObject(_target, "Add Node");
					var list = new List<BezierPoint>(_target.points);
					list.Add(new BezierPoint(ToNormalized(r, e.mousePosition)));
					list.Sort((a, b) => a.pos.x.CompareTo(b.pos.x));
					_target.points = list.ToArray();
					_target.Bake(isAuto: true);
				}
				e.Use();
			}
			if (e.type == EventType.MouseDown && e.button == 1) {
				for (int i = 0; i < _target.points.Length; i++) {
					if (Vector2.Distance(e.mousePosition, ToScreen(r, _target.points[i].pos)) < 10f) {
						if (_target.points.Length <= 2) break; // need at least 2 points
						Undo.RecordObject(_target, "Remove Node");
						var list = new List<BezierPoint>(_target.points);
						list.RemoveAt(i);
						_target.points = list.ToArray();
						_target.Bake(isAuto: true);
						break;
					}
				}
				e.Use();
			}
			if (e.type == EventType.MouseDrag && _dragIdx != -1) {
				Undo.RecordObject(_target, "Edit AI Curve");
				Vector2 norm = ToNormalized(r, e.mousePosition);
				BezierPoint p = _target.points[_dragIdx];

				if (_dragType == 0) {
					p.pos = new Vector2(Mathf.Clamp01(norm.x), Mathf.Clamp01(norm.y));
				} else if (_dragType == 1) {
					p.hIn = norm - p.pos;
					if (!e.alt) p.hOut = -p.hIn;
				} else if (_dragType == 2) {
					p.hOut = norm - p.pos;
					if (!e.alt) p.hIn = -p.hOut;
				}

				_target.points[_dragIdx] = p;
				_target.Bake(isAuto: true); // Visual feedback only
				e.Use();
			}
			if (e.type == EventType.MouseUp) _dragIdx = -1;
		}

		private void HandleZoomPan(Rect c) {
			Event e = Event.current;
			if (e.type == EventType.ScrollWheel && c.Contains(e.mousePosition)) {
				_zoom = Mathf.Clamp(_zoom - e.delta.y * 0.01f, 0.2f, 5f);
				e.Use();
			}
			if (e.type == EventType.MouseDrag && e.button == 2) {
				_viewPan += e.delta;
				e.Use();
			}
		}

		private Vector2 ToScreen(Rect r, Vector2 n) => new Vector2(r.x + n.x * r.width, r.y + (1f - n.y) * r.height);
		private Vector2 ToNormalized(Rect r, Vector2 s) => new Vector2((s.x - r.x) / r.width, 1f - (s.y - r.y) / r.height);
	}
}
#endif