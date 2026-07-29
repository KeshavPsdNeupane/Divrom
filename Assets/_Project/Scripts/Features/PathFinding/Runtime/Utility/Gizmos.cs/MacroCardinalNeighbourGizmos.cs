using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Project.Scripts.Features.PathFinding.GraphManager;
using Kope.Feature.PathFinding.Node;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFinding.Utility {

	public static class MacroCardinalNeighbourGizmos {

		/// <summary>
		/// Renders a connection line crossing the shared border between neighboring macro boxes.
		/// Prunes from the center nodes inward based on lineDrawRatio.
		/// Deduplicates bidirectional graph edges so each seam indicator is drawn exactly once.
		/// Zero GC allocations per frame.
		/// </summary>
		/// <param name="macroAdjacencyListWrapper">The adjacency list dictionary to draw seams for.</param>
		/// <param name="macroTilemap">The tilemap used for cell-to-world conversion.</param>
		/// <param name="lineColor">Gizmo line color.</param>
		/// <param name="lineThickness">Line thickness when rendered in the Unity Editor.</param>
		/// <param name="lineDrawRatio">1.0 draws center to center. 0.1 draws a small connector on the boundary.</param>
		public static void DrawNeighbourLine(
			IDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyListWrapper,
			Tilemap macroTilemap,
			Color lineColor,
			float lineThickness = 2f,
			float lineDrawRatio = 0.25f) {

			if (macroTilemap == null || macroAdjacencyListWrapper == null || macroAdjacencyListWrapper.Count == 0) return;

			// Clamp to ensure we don't accidentally invert or overshoot the lines
			lineDrawRatio = Mathf.Clamp01(lineDrawRatio);

			foreach (var kvp in macroAdjacencyListWrapper) {
				BoundingBox boxA = kvp.Key;
				List<MacroConnectionData> neighbors = kvp.Value;

				if (neighbors == null || neighbors.Count == 0) continue;

				Vector3 centerA = GetBoxCenterWorld(boxA, macroTilemap);

				foreach (var connection in neighbors) {
					BoundingBox boxB = connection.ToBound;

					// Deterministic ordering check replaces a HashSet dedup
					if (!IsPrimaryEdge(boxA, boxB)) continue;

					Vector3 centerB = GetBoxCenterWorld(boxB, macroTilemap);
					Vector3 seamPoint = GetSeamPointWorld(boxA, boxB, macroTilemap);

					// Interpolate from the exact physical seam outward to the centers.
					// This elegantly "prunes" the line from the ends if the ratio < 1.0.
					Vector3 p1 = Vector3.Lerp(seamPoint, centerA, lineDrawRatio);
					Vector3 p2 = Vector3.Lerp(seamPoint, centerB, lineDrawRatio);

#if UNITY_EDITOR
					Handles.color = lineColor;
					// Draw as a PolyLine through the seam point.
					// If the boxes are staggered, this correctly forms a "dogleg" that
					// proves the connection passes through the exact shared physical portal.
					Handles.DrawAAPolyLine(lineThickness, p1, seamPoint, p2);
#else
                    Gizmos.color = lineColor;
                    Gizmos.DrawLine(p1, seamPoint);
                    Gizmos.DrawLine(seamPoint, p2);
#endif
				}
			}
		}

		/// <summary>
		/// Deterministic comparison ensuring each undirected edge (A, B) is visited only once,
		/// without a HashSet or tuple allocation.
		/// </summary>
		private static bool IsPrimaryEdge(BoundingBox a, BoundingBox b) {
			if (a.Min.X != b.Min.X) return a.Min.X < b.Min.X;
			if (a.Min.Y != b.Min.Y) return a.Min.Y < b.Min.Y;
			if (a.Max.X != b.Max.X) return a.Max.X < b.Max.X;
			return a.Max.Y < b.Max.Y;
		}

		// Routed entirely through CellToWorld (no manual cellSize scaling) so seams stay correct
		// under cellGap, tileAnchor offsets, or non-orthogonal grid layouts.
		private static Vector3 GetSeamPointWorld(BoundingBox a, BoundingBox b, Tilemap tilemap) {
			// 1. Horizontal shared border (Left / Right)
			if (a.Max.X + 1 == b.Min.X || b.Max.X + 1 == a.Min.X) {
				int borderX = (a.Max.X + 1 == b.Min.X) ? b.Min.X : a.Min.X;
				int overlapMinY = Mathf.Max(a.Min.Y, b.Min.Y);
				int overlapMaxY = Mathf.Min(a.Max.Y, b.Max.Y);

				Vector3 p1 = tilemap.CellToWorld(new Vector3Int(borderX, overlapMinY, 0));
				Vector3 p2 = tilemap.CellToWorld(new Vector3Int(borderX, overlapMaxY + 1, 0));
				return (p1 + p2) * 0.5f;
			}

			// 2. Vertical shared border (Top / Bottom)
			int borderY = (a.Max.Y + 1 == b.Min.Y) ? b.Min.Y : a.Min.Y;
			int overlapMinX = Mathf.Max(a.Min.X, b.Min.X);
			int overlapMaxX = Mathf.Min(a.Max.X, b.Max.X);

			Vector3 q1 = tilemap.CellToWorld(new Vector3Int(overlapMinX, borderY, 0));
			Vector3 q2 = tilemap.CellToWorld(new Vector3Int(overlapMaxX + 1, borderY, 0));
			return (q1 + q2) * 0.5f;
		}

		/// <summary>
		/// Calculates the exact world position center of a macro region's bounding box, via
		/// CellToWorld on both corners so it agrees with GetSeamPointWorld under any grid configuration.
		/// </summary>
		private static Vector3 GetBoxCenterWorld(BoundingBox box, Tilemap tilemap) {
			Vector3 minCorner = tilemap.CellToWorld(new Vector3Int(box.Min.X, box.Min.Y, 0));
			Vector3 maxCorner = tilemap.CellToWorld(new Vector3Int(box.Max.X + 1, box.Max.Y + 1, 0));
			return (minCorner + maxCorner) * 0.5f;
		}
	}
}