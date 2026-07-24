using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// Utility class for rendering macro region bounding boxes and anchor points in the Scene view.
	/// </summary>
	public static class MacroRegionSliceGizmos {

		/// <summary>
		/// Renders a translucent volume, wireframe outline, and anchor indicators for baked macro region slices.
		/// </summary>
		/// <param name="bakedSlicesCache">The cached dictionary of baked slices.</param>
		/// <param name="macroTilemap">The tilemap used for cell-to-world conversion.</param>
		/// <param name="sliceBoxColor">Color for the filled translucent volume box.</param>
		/// <param name="sliceBoxBorderColor">Color for the solid wireframe border outline.</param>
		public static void DrawRegionSlices(
			Dictionary<BoundingBox, (Vec2Int Anchor, List<Vec2Int> Tiles)> bakedSlicesCache,
			Tilemap macroTilemap,
			Color sliceBoxColor,
			Color sliceBoxBorderColor) {

			if (macroTilemap == null || bakedSlicesCache == null || bakedSlicesCache.Count == 0) return;

			const float anchorRadius = 0.25f;
			const float circleThickness = 10f;

			foreach (var kvp in bakedSlicesCache) {
				BoundingBox box = kvp.Key;
				Vec2Int anchor = kvp.Value.Tiles[0];
				Vec2Int anchorRegion = kvp.Value.Anchor;

				Vector3 regionAnchorPoint = macroTilemap.CellToWorld(
					new Vector3Int(anchorRegion.X, anchorRegion.Y, 0));

				Vector3 minWorldPos = macroTilemap.CellToWorld(
					new Vector3Int(box.Min.X, box.Min.Y, 0));
				Vector3 tileAnchorWorldPos = macroTilemap.GetCellCenterWorld(
					new Vector3Int(anchor.X, anchor.Y, 0));

				Vector3 cellSize = macroTilemap.cellSize;
				Vector3 boxSize = new(
					(box.Max.X - box.Min.X + 1) * cellSize.x,
					(box.Max.Y - box.Min.Y + 1) * cellSize.y,
					Mathf.Max(cellSize.z, 0.1f)
				);

				Vector3 boxCenter = minWorldPos + (boxSize * 0.5f);

				// 1. Draw filled translucent volume box
				Gizmos.color = sliceBoxColor;
				Gizmos.DrawCube(boxCenter, boxSize);

				// 2. Draw crisp solid wireframe border outline
				Gizmos.color = sliceBoxBorderColor;
				Gizmos.DrawWireCube(boxCenter, boxSize);

				// 3. Draw Anchor Point as a thick 2D circle at the cell center
				tileAnchorWorldPos.z = boxCenter.z - (boxSize.z * 0.5f) - 0.5f;

#if UNITY_EDITOR
				Handles.color = sliceBoxBorderColor;
				Handles.DrawWireDisc(
					tileAnchorWorldPos,
					Vector3.forward,
					anchorRadius,
					circleThickness
				);

				Handles.color = Color.white;
				Handles.DrawWireDisc(
					regionAnchorPoint,
					Vector3.forward,
					anchorRadius * 0.5f,
					circleThickness
				);
#endif
			}
		}
	}
}