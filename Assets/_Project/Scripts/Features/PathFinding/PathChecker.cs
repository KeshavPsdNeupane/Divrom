using System.Collections.Generic;
using System.Text;
using Kope.Core.Attribute;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Interface;
using Kope.Feature.PathFinding.Tile;
using Kope.Feature.PathFinding.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;
using ZLinq;
using Kope.EntityIdentity;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFinding {

	public enum PathCheckerErrorType {
		InvalidTile = 0,
		DuplicateTile = 1
	}

	[System.Serializable]
	public struct ErrorTileInfo {
		[ReadOnly] public Vector2Int Position;
		[ReadOnly] public PathCheckerErrorType ErrorType;
		[ReadOnly] public string ErrorMessage;

		public ErrorTileInfo(Vector2Int position, PathCheckerErrorType errorType, string errorMessage) {
			this.Position = position;
			this.ErrorType = errorType;
			this.ErrorMessage = errorMessage;
		}
	}

	public enum RectanleSlicerAgorithm {
		GREEDY = 0,
		DUAL_PHASE_GREEDY_MESHING = 10,
		GreedyClusteringHistogramSlicer = 36,
		GreedyClusteringHistogramSlicerMarshalled = 37,
		PureHistogramSlicer = 50,
		PureHistogramSlicerMarshalled = 51
	}

	[System.Serializable]
	public class ErrorConfiguration {
		[Header("Gizmo Colors per Error Type")]
		[Tooltip("Color of the error gizmo for unassigned or unrecognized tiles.")]
		[SerializeField] private Color invalidTileColor = Color.red;
		public Color InvalidTileColor => invalidTileColor;

		[Tooltip("Color of the error gizmo for overlapping or duplicate tile placements.")]
		[SerializeField] private Color duplicateTileColor = new(1f, 0.5f, 0f); // Orange
		public Color DuplicateTileColor => duplicateTileColor;

		public Color GetColor(PathCheckerErrorType errorType) {
			return errorType switch {
				PathCheckerErrorType.InvalidTile => invalidTileColor,
				PathCheckerErrorType.DuplicateTile => duplicateTileColor,
				_ => Color.magenta
			};
		}
	}

	[ExecuteAlways]
	public class PathChecker : MonoBehaviour {
		[Header("Tilemap Targets")]
		[SerializeField] private Tilemap microTilemap;
		[SerializeField] private Tilemap macroTilemap;

		[Header("Rectangle Slicer Selection")]
		[SerializeField] private RectanleSlicerAgorithm rectangleSlicer = RectanleSlicerAgorithm.GREEDY;

		[Header("Bounding Box Constraints")]
		[SerializeField] private Vector2Int maxBoundingBoxSize = new(16, 16);

		[Header("Error Visualizer Configurations")]
		[SerializeField] private ErrorConfiguration macroErrorConfig = new();
		[SerializeField] private ErrorConfiguration microErrorConfig = new();

		[Header("Region Slice Gizmo Settings")]
		[SerializeField] private bool showRegionSlices = true;
		[SerializeField] private Color sliceBoxColor = new(0f, 1f, 1f, 0.3f); // Translucent Cyan
		[SerializeField] private Color sliceBoxBorderColor = new(0f, 0.8f, 0.8f, 1f); // Opaque Cyan

		[Header("Macro Neighbor Line Gizmo Settings")]
		[SerializeField] private bool showNeighborConnections = true;
		[SerializeField] private Color neighborLineColor = Color.yellow;
		[SerializeField] private float neighborLineThickness = 3f;

		private readonly SerializableDictionary<Vector2Int, HHSIMacroPathFindingTile> _macroTileDictionary = new(Vector2IntComparer.Instance);
		private readonly SerializableDictionary<Vector2Int, HHSIMicroPathFindingTile> _microTileDictionary = new(Vector2IntComparer.Instance);

		private readonly SerializableDictionary<Vector2Int, MicroGridNode> _microGridNodeDict = new(Vector2IntComparer.Instance);
		private readonly SerializableDictionary<BoundingBox, MacroGridNode> _macroGridNodeDict = new();

		[Header("Error Tracking")]
		[SerializeField] private List<ErrorTileInfo> _macroErrors = new();
		[SerializeField] private List<ErrorTileInfo> _microErrors = new();

		private readonly RegionExtractionAlgorithm _regionExtractor = new();
		private readonly SliceAnalysisSummarizer _summarizer = new();
		private IRectangleRegionSlicer _rectanglePacker;
		private readonly IMacroNeighbourFinder _neighborFinder = new MacroCardinalNeighbourFinder();

		// Gizmos cache for baked slices to avoid recalculating during OnDrawGizmos
		private readonly Dictionary<BoundingBox, (Vector2Int Anchor, List<Vector2Int> Tiles)> _bakedSlicesCache = new();
		private Dictionary<BoundingBox, List<BoundingBox>> _macroNeighbourCacheForGizmos;

		/// <summary>
		/// Gets or sets whether region slice bounding boxes should be drawn in the Scene view via Gizmos.
		/// </summary>
		public bool ShowRegionSlices {
			get => showRegionSlices;
			set {
				showRegionSlices = value;
#if UNITY_EDITOR
				SceneView.RepaintAll();
#endif
			}
		}

		public IRectangleRegionSlicer GetRectangleSlicer(RectanleSlicerAgorithm slicer) {
			return slicer switch {
				RectanleSlicerAgorithm.GREEDY => new GreedyRectanglePackingAlogorithm(),
				RectanleSlicerAgorithm.DUAL_PHASE_GREEDY_MESHING => new DualAxisGreedyMeshingAlgorithm(),
				RectanleSlicerAgorithm.GreedyClusteringHistogramSlicer => new GreedyClusteringHistogramSlicer(),
				RectanleSlicerAgorithm.GreedyClusteringHistogramSlicerMarshalled => new GreedyClusteringHistogramSlicerMarshalled(),
				RectanleSlicerAgorithm.PureHistogramSlicer => new PureHistogramRegionSlicer(),
				RectanleSlicerAgorithm.PureHistogramSlicerMarshalled => new PureHistogramRegionSlicerMarshalled(),
				_ => throw new System.ArgumentOutOfRangeException(nameof(slicer), slicer, null)
			};
		}

		public void PreparePathfindingData() {
			PreparePathfindingData<MacroTerrainData, HHSIMacroPathFindingTile>(
				"Macro",
				this.macroTilemap,
				this._macroTileDictionary,
				this._macroErrors,
				false
			);

			PreparePathfindingData<MicroTerrainData, HHSIMicroPathFindingTile>(
				"Micro",
				this.microTilemap,
				this._microTileDictionary,
				this._microErrors,
				false
			);
#if UNITY_EDITOR
			SceneView.RepaintAll();
#endif
		}

		public void PreparePathfindingData<TData, TitledTile>(
		string tilemapName,
		Tilemap targetTilemap,
		SerializableDictionary<Vector2Int, TitledTile> targetDictionary,
		List<ErrorTileInfo> targetErrorList, bool repaint = true)
		where TData : struct, ITerrainData<TData>
		where TitledTile : HSIPathFindingTileBase<TData> {

			if (targetTilemap == null) {
				Debug.LogError($"{tilemapName} Tilemap reference is missing.");
				return;
			}
			targetDictionary.Clear();
			targetErrorList.Clear();

			BoundsInt bounds = targetTilemap.cellBounds;
			TileBase[] allTiles = targetTilemap.GetTilesBlock(bounds);

			for (int i = 0; i < allTiles.Length; i++) {
				TileBase tile = allTiles[i];
				if (tile == null) continue;

				int x = bounds.x + (i % bounds.size.x);
				int y = bounds.y + (i / bounds.size.x);
				Vector2Int cellPos = new(x, y);

				if (tile is not TitledTile typedTile) {
					targetErrorList.Add(new ErrorTileInfo(
						cellPos,
						PathCheckerErrorType.InvalidTile,
						$"[{tilemapName}] Invalid tile assigned at {cellPos}."
					));
				} else {
					if (targetDictionary.ContainsKey(cellPos)) {
						targetErrorList.Add(new ErrorTileInfo(
							cellPos,
							PathCheckerErrorType.DuplicateTile,
							$"[{tilemapName}] Duplicate tile conflict at {cellPos}."
						));
					} else {
						targetDictionary[cellPos] = typedTile;
					}
				}
			}

#if UNITY_EDITOR
			if (repaint) {
				SceneView.RepaintAll();
			}
#endif
		}

		public void QuoteOnQuoteBake() {
			this._rectanglePacker = GetRectangleSlicer(this.rectangleSlicer);

			// ==========================================
			// 1. MICRO-LEVEL NODE GENERATION
			// ==========================================
			this._microGridNodeDict.Clear();
			foreach (var kvp in this._microTileDictionary) {
				Vector2Int position = kvp.Key;
				HHSIMicroPathFindingTile tile = kvp.Value;
				MicroGridNode node = new(position, tile.Data.IsStaticObstacle) {
					ParentMacroGrid = null
				};
				this._microGridNodeDict.Add(position, node);
			}

			// ==========================================
			// 2. MACRO-LEVEL NODE GENERATION & SLICING
			// ==========================================
			var regionTiles = this._regionExtractor.Extract(this._macroTileDictionary);
			var maxBoundSize = this.maxBoundingBoxSize;

			System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
			var slicedRegions = this._rectanglePacker.Slice(regionTiles, maxBoundSize);
			stopwatch.Stop();

			if (slicedRegions == null) return;

			int totalSizeForReserveDict = slicedRegions.AsValueEnumerable().Sum(kvp => kvp.Value.RegionTilePositions.Count);
			BoundingBox[] slicedRegionArray = slicedRegions.Keys.AsValueEnumerable().ToArray();
			Dictionary<(int x, int y), BoundingBox> microToMacroMapping = new(totalSizeForReserveDict);

			this._bakedSlicesCache.Clear();

			foreach (var kvp in slicedRegions) {
				this._bakedSlicesCache[kvp.Key] = kvp.Value;
				BoundingBox box = kvp.Key;

				HHSIMacroPathFindingTile regionTile = this._macroTileDictionary[kvp.Value.regionAnchor];

				TerrainType terrainType = regionTile.Data.TerrainType;
				MovementCapability movementCapability = regionTile.Data.MovementType;

				var currentMacroNode = new MacroGridNode(box, terrainType, movementCapability);
				this._macroGridNodeDict.Add(box, currentMacroNode);

				foreach (Vector2Int tilePos in kvp.Value.RegionTilePositions) {
					if (this._microGridNodeDict.TryGetValue(tilePos, out MicroGridNode microNode)) {
						microNode.SetParentMacroGrid(currentMacroNode);
						currentMacroNode.MicroGridsNodes.Add(microNode);
						microToMacroMapping[(tilePos.x, tilePos.y)] = box;
					}
				}
			}

			// ==========================================
			// 3. MACRO CONNECTION BUILD PASS
			// ==========================================
			Dictionary<BoundingBox, List<BoundingBox>> macroNeighbours =
				this._neighborFinder.FindNeighbours(microToMacroMapping, slicedRegionArray);

			this._macroNeighbourCacheForGizmos = macroNeighbours;

			StringBuilder debugOutput = new();
			foreach (var kvp in macroNeighbours) {
				debugOutput.AppendLine($"Macro Box {kvp.Key} has {kvp.Value.Count} neighbours.");
			}
			Debug.Log(debugOutput.ToString());

			// ==========================================
			// 4. UNIFIED SUMMARY REPORTING
			// ==========================================
			this._summarizer.MakeSummary(
				this._rectanglePacker,
				maxBoundSize,
				regionTiles != null ? regionTiles.Count : 0,
				slicedRegions,
				stopwatch
			);

#if UNITY_EDITOR
			SceneView.RepaintAll();
#endif
		}

		private void OnDrawGizmos() {
			DrawTilemapErrors(macroTilemap, _macroErrors, macroErrorConfig);
			DrawTilemapErrors(microTilemap, _microErrors, microErrorConfig);

			if (showRegionSlices) {
				DrawRegionSlices();
			}

			if (showNeighborConnections) {
				DrawNeighbourLine();
			}
		}

		private void DrawTilemapErrors(Tilemap targetTilemap, List<ErrorTileInfo> errors, ErrorConfiguration config) {
			if (targetTilemap == null || errors == null || errors.Count == 0) return;

			foreach (var err in errors) {
				Gizmos.color = config.GetColor(err.ErrorType);

				Vector3 worldPos = targetTilemap.CellToWorld(new Vector3Int(err.Position.x, err.Position.y, 0));
				Vector3 centerPos = worldPos + new Vector3(targetTilemap.cellSize.x * 0.5f, targetTilemap.cellSize.y * 0.5f, 0f);

				float radius = 0.4f;

#if UNITY_EDITOR
				radius = HandleUtility.GetHandleSize(centerPos) * 0.25f;
#endif

				Gizmos.DrawWireSphere(centerPos, radius);
			}
		}

		private void DrawRegionSlices() {
			if (macroTilemap == null || _bakedSlicesCache == null || _bakedSlicesCache.Count == 0) return;

			const float anchorRadius = 0.25f;
			const float circleThickness = 10f;

			foreach (var kvp in _bakedSlicesCache) {
				BoundingBox box = kvp.Key;
				Vector2Int anchor = kvp.Value.Tiles[0];
				Vector2Int anchorRegion = kvp.Value.Anchor;

				Vector3 regionAnchorPoint = macroTilemap.CellToWorld(new Vector3Int(anchorRegion.x, anchorRegion.y, 0));

				Vector3 minWorldPos = macroTilemap.CellToWorld(new Vector3Int(box.Min.x, box.Min.y, 0));
				Vector3 tileAnchorWorldPos = macroTilemap.GetCellCenterWorld(new Vector3Int(anchor.x, anchor.y, 0));

				Vector3 cellSize = macroTilemap.cellSize;
				Vector3 boxSize = new(
					(box.Max.x - box.Min.x + 1) * cellSize.x,
					(box.Max.y - box.Min.y + 1) * cellSize.y,
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

		/// <summary>
		/// Renders a connection line crossing the shared border between neighboring macro boxes.
		/// The line length dynamically scales to half (or a quarter) of the distance between the box centers.
		/// Deduplicates bidirectional graph edges so each seam indicator is drawn exactly once.
		/// </summary>
		private void DrawNeighbourLine() {
			if (macroTilemap == null || _macroNeighbourCacheForGizmos == null || _macroNeighbourCacheForGizmos.Count == 0) return;
			MacroCardinalNeighbourGizmos.DrawNeighbourLine(
				_macroNeighbourCacheForGizmos,
				macroTilemap,
				neighborLineColor,
				neighborLineThickness,
				0.1f
			);

			// 			// Pre-allocated set to ensure we only draw 1 edge indicator per shared border
			// 			HashSet<(BoundingBox, BoundingBox)> drawnEdges = new(_macroNeighbourCacheForGizmos.Count * 2);

			// 			// Distance multiplier for line length (0.5f = 50% of distance between box centers)
			// 			// Change to 0.25f if you prefer a quarter of the distance.
			// 			const float lineDistanceRatio = 0.25f;

			// 			foreach (var kvp in _macroNeighbourCacheForGizmos) {
			// 				BoundingBox boxA = kvp.Key;
			// 				List<BoundingBox> neighbors = kvp.Value;

			// 				if (neighbors == null || neighbors.Count == 0) continue;

			// 				Vector3 centerA = GetBoxCenterWorld(boxA);

			// 				foreach (var boxB in neighbors) {
			// 					var edge = GetUndirectedEdge(boxA, boxB);

			// 					// Skip if this shared border was already drawn
			// 					if (!drawnEdges.Add(edge)) continue;

			// 					Vector3 centerB = GetBoxCenterWorld(boxB);

			// 					// 1. Calculate total distance between box centers & normalized direction
			// 					float distance = Vector3.Distance(centerA, centerB);
			// 					Vector3 dir = (centerB - centerA) / distance; // Normalized vector

			// 					// 2. Calculate seam midpoint
			// 					Vector3 seamPoint = GetSeamPointWorld(boxA, boxB);

			// 					// 3. Scale line length based on percentage of center-to-center distance
			// 					float lineLength = distance * lineDistanceRatio;

			// 					Vector3 p1 = seamPoint - dir * (lineLength * 0.5f);
			// 					Vector3 p2 = seamPoint + dir * (lineLength * 0.5f);

			// #if UNITY_EDITOR
			// 					Handles.color = neighborLineColor;
			// 					Handles.DrawAAPolyLine(neighborLineThickness, p1, p2);
			// #else
			//                     Gizmos.color = neighborLineColor;
			//                     Gizmos.DrawLine(p1, p2);
			// #endif
			// 				}
			//}
		}

		private Vector3 GetSeamPointWorld(BoundingBox a, BoundingBox b) {
			Vector3 cellSize = macroTilemap.cellSize;
			Vector3 mapOrigin = macroTilemap.CellToWorld(new Vector3Int(0, 0, 0));

			// 1. Horizontal shared border (Left / Right)
			if (a.Max.x + 1 == b.Min.x || b.Max.x + 1 == a.Min.x) {
				int borderX = (a.Max.x + 1 == b.Min.x) ? b.Min.x : a.Min.x;
				float overlapMinY = Mathf.Max(a.Min.y, b.Min.y);
				float overlapMaxY = Mathf.Min(a.Max.y, b.Max.y);
				float midY = (overlapMinY + overlapMaxY + 1f) * 0.5f;

				return new Vector3(
					macroTilemap.CellToWorld(new Vector3Int(borderX, 0, 0)).x,
					mapOrigin.y + (midY * cellSize.y),
					0f
				);
			}

			// 2. Vertical shared border (Top / Bottom)
			int borderY = (a.Max.y + 1 == b.Min.y) ? b.Min.y : a.Min.y;
			float overlapMinX = Mathf.Max(a.Min.x, b.Min.x);
			float overlapMaxX = Mathf.Min(a.Max.x, b.Max.x);
			float midX = (overlapMinX + overlapMaxX + 1f) * 0.5f;

			return new Vector3(
				mapOrigin.x + (midX * cellSize.x),
				macroTilemap.CellToWorld(new Vector3Int(0, borderY, 0)).y,
				0f
			);
		}
		/// <summary>
		/// Calculates the exact world position center of a macro region's bounding box.
		/// </summary>
		private Vector3 GetBoxCenterWorld(BoundingBox box) {
			Vector3 minWorldPos = macroTilemap.CellToWorld(new Vector3Int(box.Min.x, box.Min.y, 0));
			Vector3 cellSize = macroTilemap.cellSize;

			Vector3 boxSize = new(
				(box.Max.x - box.Min.x + 1) * cellSize.x,
				(box.Max.y - box.Min.y + 1) * cellSize.y,
				0f
			);

			return minWorldPos + (boxSize * 0.5f);
		}

		/// <summary>
		/// Consistent hash-code ordering ensuring undirected edges (A, B) and (B, A) match.
		/// </summary>
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		private static (BoundingBox, BoundingBox) GetUndirectedEdge(BoundingBox a, BoundingBox b) {
			return a.GetHashCode() < b.GetHashCode() ? (a, b) : (b, a);
		}
	}
}