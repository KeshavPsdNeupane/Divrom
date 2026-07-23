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
		private readonly SliceAnalysisSummarizer _summarizer = new();
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
				this._microGridNodeDict[position] = node;
			}

			// ==========================================
			// 2. MACRO-LEVEL NODE GENERATION & SLICING
			// ==========================================
			var regionTiles = this._regionExtractor.Extract(this._macroTileDictionary);
			var maxBoundSize = this.maxBoundingBoxSize;

			System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
			var slicedRegions = this._rectanglePacker.Slice(regionTiles, maxBoundSize);
			stopwatch.Stop();

			// lets fucking goo , need to create connection data using the slicedRegion and 
			// the macroTileDictionary and the microGridNodeDict to create the connection data 
			// for each macroGridNode, god forgive me for what i am going to create,
			// but i know i won't.


			// Store newly computed slices into cache for Gizmo visualization
			this._bakedSlicesCache.Clear();
			if (slicedRegions != null) {
				foreach (var kvp in slicedRegions) {
					this._bakedSlicesCache[kvp.Key] = kvp.Value;
					// first lets find the terrain type, each other type will be found step wise
					BoundingBox box = kvp.Key;

					HHSIMacroPathFindingTile regionTile = this._macroTileDictionary[kvp.Value.regionAnchor];
					// good job me to get 1st data
					TerrainType terrainType = regionTile.Data.TerrainType;

					MovementCapability movementCapability = regionTile.Data.MovementType;

					bool IsNarrativelyAccessible = regionTile.Data.IsNarrativelyAccessible;
					// now we all have data except the connection data, we will have to
					// find the connection data by checking the surrounding tiles
					// we will check the surrounding tiles and find the connection data
					// so what to do lets goo time to make another algo that need to be preprocessed
					// before this loop which will find the connection data for each tile 
					// and store it in a dictionary
					// we will use the dictionary to find the connection data for each tile
					// this._macroGridNodeDict[kvp.Key] = new MacroGridNode(kvp.Key
					// ,);
					// man i dont know why but i do be using alot of dictionary and 
					// but it is fastest for the data structure i need to use and it is 
					// easy to use and understand
				}
			}
			// ==========================================
			// 3. UNIFIED SUMMARY REPORTING
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

			// Radius for a circle with a 0.5 unit total diameter (0.25f radius)
			const float anchorRadius = 0.25f;
			const float circleThickness = 10f; // Increase pixel width here (e.g., 3f, 4f, 6f)

			foreach (var kvp in _bakedSlicesCache) {
				BoundingBox box = kvp.Key;
				Vector2Int anchor = kvp.Value.Tiles[0];
				Vector2Int anchorRegion = kvp.Value.Anchor;

				Vector3 regionAnchorPoint = macroTilemap.CellToWorld(new Vector3Int(anchorRegion.x, anchorRegion.y, 0));

				// Convert bounds and anchor to world positions (cell center for anchor)
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
				tileAnchorWorldPos.z = boxCenter.z - (boxSize.z * 0.5f) - 0.5f; // in front of box

#if UNITY_EDITOR
				Handles.color = sliceBoxBorderColor;
				Handles.DrawWireDisc(
					tileAnchorWorldPos,
					Vector3.forward,  // Normal vector facing the 2D camera
					anchorRadius,
					circleThickness   // Line width in pixels
				);

				Handles.color = Color.white;
				Handles.DrawWireDisc(
					regionAnchorPoint,
					Vector3.forward,  // Normal vector facing the 2D camera
					anchorRadius * 0.5f,
					circleThickness   // Line width in pixels
				);
#endif
			}
		}
	}
}