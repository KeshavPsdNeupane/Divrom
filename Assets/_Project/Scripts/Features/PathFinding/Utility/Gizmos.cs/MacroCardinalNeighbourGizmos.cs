using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFinding.Utility {
	public static class MacroCardinalNeighbourGizmos {

		/// <summary>
		/// Renders a connection line crossing the shared border between neighboring macro boxes.
		/// The line length dynamically scales to a fraction of the distance between the box centers.
		/// Deduplicates bidirectional graph edges so each seam indicator is drawn exactly once.
		/// Zero GC allocations per frame.
		/// </summary>
		/// <param name="neighbourCache">The connection dictionary to draw seams for.</param>
		/// <param name="macroTilemap">The tilemap used for cell-to-world conversion.</param>
		/// <param name="lineColor">Gizmo line color.</param>
		/// <param name="lineThickness">Line thickness when rendered in the Unity Editor.</param>
		/// <param name="lineDistanceRatio">Line length relative to center-to-center distance (0.5f = 50%, 0.25f = 25%).</param>
		public static void DrawNeighbourLine(
			Dictionary<BoundingBox, List<BoundingBox>> neighbourCache,
			Tilemap macroTilemap,
			Color lineColor,
			float lineThickness = 2f,
			float lineDistanceRatio = 0.25f) {

			if (macroTilemap == null || neighbourCache == null || neighbourCache.Count == 0) return;

			foreach (var kvp in neighbourCache) {
				BoundingBox boxA = kvp.Key;
				List<BoundingBox> neighbors = kvp.Value;

				if (neighbors == null || neighbors.Count == 0) continue;

				Vector3 centerA = GetBoxCenterWorld(boxA, macroTilemap);

				foreach (var boxB in neighbors) {
					// Deterministic ordering check replaces a HashSet dedup — each undirected
					// edge is only processed from its "primary" side, so no per-frame set
					// allocation or hashing is needed to skip the mirror direction.
					if (!IsPrimaryEdge(boxA, boxB)) continue;

					Vector3 centerB = GetBoxCenterWorld(boxB, macroTilemap);

					// 1. Calculate total distance between box centers & normalized direction
					float distance = Vector3.Distance(centerA, centerB);
					if (distance <= Mathf.Epsilon) continue; // guards against div-by-zero on coincident centers

					Vector3 dir = (centerB - centerA) / distance; // Normalized vector

					// 2. Calculate seam midpoint
					Vector3 seamPoint = GetSeamPointWorld(boxA, boxB, macroTilemap);

					// 3. Scale line length based on percentage of center-to-center distance
					float lineLength = distance * lineDistanceRatio;

					Vector3 p1 = seamPoint - dir * (lineLength * 0.5f);
					Vector3 p2 = seamPoint + dir * (lineLength * 0.5f);

#if UNITY_EDITOR
					Handles.color = lineColor;
					Handles.DrawAAPolyLine(lineThickness, p1, p2);
#else
                    Gizmos.color = lineColor;
                    Gizmos.DrawLine(p1, p2);
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