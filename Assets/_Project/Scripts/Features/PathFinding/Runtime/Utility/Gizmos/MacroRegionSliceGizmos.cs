using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Kope.Feature.PathFinding.Node;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// Utility class for rendering macro region bounding box outlines and anchor points in the Scene view.
	/// </summary>
	public static class MacroRegionSliceGizmos {

		/// <summary>
		/// Renders solid wireframe outlines and anchor indicators for baked macro region slices.
		/// </summary>
		/// <param name="macroGridNodeDict">The dictionary of macro grid nodes containing boundary and tile data.</param>
		/// <param name="regionAnchorPoints">The global list of region anchor points.</param>
		/// <param name="macroTilemap">The tilemap used for cell-to-world conversion.</param>
		/// <param name="sliceBoxBorderColor">Color for the wireframe outline.</param>
		public static void DrawRegionSlices(
			IDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			IReadOnlyList<Vec2Int> regionAnchorPoints,
			Tilemap macroTilemap,
			Color sliceBoxBorderColor) {

			if (macroTilemap == null) return;

			const float anchorRadius = 0.25f;
			const float circleThickness = 10f;

			// 1. Draw Macro Bounding Boxes and Tile Anchors
			if (macroGridNodeDict != null && macroGridNodeDict.Count > 0) {
				foreach (var kvp in macroGridNodeDict) {
					BoundingBox box = kvp.Key;
					MacroGridNode node = kvp.Value;

					Vector3 minWorldPos = macroTilemap.CellToWorld(
						new Vector3Int(box.Min.X, box.Min.Y, 0));

					Vector3 cellSize = macroTilemap.cellSize;
					Vector3 boxSize = new(
						(box.Max.X - box.Min.X + 1) * cellSize.x,
						(box.Max.Y - box.Min.Y + 1) * cellSize.y,
						Mathf.Max(cellSize.z, 0.1f)
					);

					Vector3 boxCenter = minWorldPos + (boxSize * 0.5f);

					// Draw wireframe outline only
					Gizmos.color = sliceBoxBorderColor;
					Gizmos.DrawWireCube(boxCenter, boxSize);

					// Draw Tile Anchor (First tile in the macro region)
					if (node.TotalMicroGrids > 0) {
						Vec2Int tileAnchor = node.MicroGridNodePositions[0];
						Vector3 tileAnchorWorldPos = macroTilemap.GetCellCenterWorld(
							new Vector3Int(tileAnchor.X, tileAnchor.Y, 0));

						tileAnchorWorldPos.z = boxCenter.z - (boxSize.z * 0.5f) - 0.5f;

#if UNITY_EDITOR
						Handles.color = sliceBoxBorderColor;
						Handles.DrawWireDisc(
							tileAnchorWorldPos,
							Vector3.forward,
							anchorRadius,
							circleThickness
						);
#endif
					}
				}
			}

			// 2. Draw Region Anchor Points
#if UNITY_EDITOR
			if (regionAnchorPoints != null && regionAnchorPoints.Count > 0) {
				Handles.color = Color.white;

				foreach (var anchorRegion in regionAnchorPoints) {
					Vector3 regionAnchorWorldPos = macroTilemap.CellToWorld(
						new Vector3Int(anchorRegion.X, anchorRegion.Y, 0));

					Handles.DrawWireDisc(
						regionAnchorWorldPos,
						Vector3.forward,
						anchorRadius * 0.5f,
						circleThickness
					);
				}
			}
#endif
		}
	}
}