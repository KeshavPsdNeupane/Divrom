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
		ADAPTIVE_DUAL_PHASE_GREEDY_MESHING_NON_PERM = 30,
		ADAPTIVE_DUAL_PHASE_GREEDY_MESHING_PERF = 31,
		ADAPTIVE_CLUSTERED_SLICER = 32,
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

		private readonly SerializableDictionary<Vector2Int, HHSIMacroPathFindingTile> _macroTileDictionary = new(Vector2IntComparer.Instance);
		private readonly SerializableDictionary<Vector2Int, HHSIMicroPathFindingTile> _microTileDictionary = new(Vector2IntComparer.Instance);
		private readonly SerializableDictionary<Vector2Int, MicroGridNode> _microGridNodeDict = new(Vector2IntComparer.Instance);
		private readonly SerializableDictionary<BoundingBox, MacroGridNode> _macroGridNodeDict = new();

		[Header("Error Tracking")]
		[SerializeField] private List<ErrorTileInfo> _macroErrors = new();
		[SerializeField] private List<ErrorTileInfo> _microErrors = new();

		// Storage cache for the most recently baked region slices to render via gizmos
		private readonly Dictionary<BoundingBox, (Vector2Int Anchor, List<Vector2Int> Tiles)> _bakedSlicesCache = new();

		private readonly RegionExtractionAlgorithm _regionExtractor = new();
		private IRectangleRegionSlicer _rectanglePacker;

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
				RectanleSlicerAgorithm.ADAPTIVE_DUAL_PHASE_GREEDY_MESHING_NON_PERM => new AdaptiveDualAxisGreedyMeshingAlgorithm(),
				RectanleSlicerAgorithm.ADAPTIVE_DUAL_PHASE_GREEDY_MESHING_PERF => new AdaptiveDualAxisGreedyMeshingAlgorithmPERFOPTIMIZED(),
				RectanleSlicerAgorithm.ADAPTIVE_CLUSTERED_SLICER => new AdaptiveDualAxisGreedyMeshingAlgorithmClustered(),
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

			Debug.Log($"[Pipeline] {tilemapName} preparation complete. Collected {targetDictionary.Count} valid " +
					  $"tiles. Flagged {targetErrorList.Count} structural errors.");

#if UNITY_EDITOR
			if (repaint) {
				SceneView.RepaintAll();
			}
#endif
		}

		public void QuoteOnQuoteBake() {
			this._rectanglePacker = GetRectangleSlicer(this.rectangleSlicer);
			//	Debug.Log("[Pipeline] Starting Quote-on-Quote bake process...");

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
				this._microGridNodeDict[position] = node;
			}

			// ==========================================
			// 2. MACRO-LEVEL NODE GENERATION & SLICING
			// ==========================================
			//Debug.Log("[Pipeline] Extracting macro regions from tilemap...");
			var regionTiles = this._regionExtractor.Extract(this._macroTileDictionary);
			string[] regionSummaries = new string[regionTiles.Count];

			for (int i = 0; i < regionTiles.Count; i++) {
				var kvp = regionTiles.AsValueEnumerable().ElementAt(i);
				Vector2Int anchorPos = kvp.Key;
				List<Vector2Int> tilesInRegion = kvp.Value;

				// // Build a string specifically for this one region
				// StringBuilder singleRegionLog = new StringBuilder();
				// singleRegionLog.Append($"Tiles in region starting at {anchorPos}: ");

				// bool first = true;
				// foreach (var tilePos in tilesInRegion) {
				// 	if (first) {
				// 		singleRegionLog.Append($"{tilePos}");
				// 		first = false;
				// 	} else {
				// 		singleRegionLog.Append($", {tilePos}");
				// 	}
				// }
				// // Log each region independently so nothing gets truncated
				// regionSummaries[i] = singleRegionLog.ToString();
			}
			//Debug.Log($"[Pipeline] Region summary (total regions = {regionTiles.Count}):");
			// for (int i = 0; i < regionSummaries.Length; i++) {
			// 	Debug.Log($"[Pipeline] Region {i + 1}/{regionSummaries.Length}: {regionSummaries[i]}");
			// }


			var maxBoundSize = this.maxBoundingBoxSize;

			StringBuilder slicingSummary = new();
			Debug.Log($"[Pipeline] Slicing macro regions using {this._rectanglePacker.GetType().Name} " +
			$"into bounding boxes with max size{maxBoundSize}");



			var stopwatch = System.Diagnostics.Stopwatch.StartNew();

			var slicedRegions = this._rectanglePacker.Slice(regionTiles, maxBoundSize);

			stopwatch.Stop();

			Debug.Log($"Slice execution time: {stopwatch.ElapsedMilliseconds} ms");

			// Store the newly computed slice bounding boxes and tile sets into our persistent instance cache for Gizmo visualization
			_bakedSlicesCache.Clear();
			foreach (var kvp in slicedRegions) {
				_bakedSlicesCache[kvp.Key] = kvp.Value;
				BoundingBox bounds = kvp.Key;
				var (anchor, tilesInRegion) = kvp.Value;
				//slicingSummary.AppendLine($"Sliced macro region starting at {anchor} with bounds {bounds} contains {tilesInRegion.Count} tiles.\n");
			}

			Debug.Log($"[Pipeline] Slicing summary (total slices = {slicedRegions.Count}): \n" + slicingSummary.ToString());

			// Debug.Log("[Pipeline] Quote-on-Quote bake process completed.");

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

		/// <summary>
		/// Renders visual Gizmo boxes and volume overlays for each stored region slice using the macroTilemap layout context.
		/// </summary>
		private void DrawRegionSlices() {
			if (macroTilemap == null || _bakedSlicesCache == null || _bakedSlicesCache.Count == 0) return;

			foreach (var kvp in _bakedSlicesCache) {
				BoundingBox box = kvp.Key;

				// Calculate world positions for the min and max bounds corner cells
				Vector3 minWorldPos = macroTilemap.CellToWorld(new Vector3Int(box.Min.x, box.Min.y, 0));

				Vector3 cellSize = macroTilemap.cellSize;
				Vector3 boxSize = new(
					(box.Max.x - box.Min.x + 1) * cellSize.x,
					(box.Max.y - box.Min.y + 1) * cellSize.y,
					Mathf.Max(cellSize.z, 0.1f)
				);

				Vector3 boxCenter = minWorldPos + (boxSize * 0.5f);

				// Draw filled translucent volume box
				Gizmos.color = sliceBoxColor;
				Gizmos.DrawCube(boxCenter, boxSize);

				// Draw crisp solid wireframe border outline
				Gizmos.color = sliceBoxBorderColor;
				Gizmos.DrawWireCube(boxCenter, boxSize);
			}
		}
	}
}
