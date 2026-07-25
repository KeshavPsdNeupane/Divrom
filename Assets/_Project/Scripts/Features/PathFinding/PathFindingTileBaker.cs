using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Interface;
using Kope.Feature.PathFinding.Tile;
using Kope.Feature.PathFinding.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;
using ZLinq;
using Kope.EntityIdentity;
using Project.Scripts.Features.PathFinding.GraphManager;

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
		[ReadOnly] public Vec2Int Position;
		[ReadOnly] public PathCheckerErrorType ErrorType;
		[ReadOnly] public string ErrorMessage;

		public ErrorTileInfo(Vec2Int position, PathCheckerErrorType errorType, string errorMessage) {
			this.Position = position;
			this.ErrorType = errorType;
			this.ErrorMessage = errorMessage;
		}
	}

	public enum RectangleSlicerAlgorithm {
		Greedy = 0,
		DualPhaseGreedyMeshing = 10,
		GreedyClusteringHistogramSlicer = 36,
		GreedyClusteringHistogramSlicerMarshalled = 37,
		PureHistogramSlicer = 50,
		PureHistogramSlicerMarshalled = 51
	}

	[System.Serializable]
	public class ErrorConfiguration {
		[Header("Gizmo Colors per Error Type")]
		[SerializeField] private Color invalidTileColor = Color.red;
		public Color InvalidTileColor => invalidTileColor;

		[SerializeField] private Color duplicateTileColor = new(1f, 0.5f, 0f);
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
	public class PathFindingTileBaker : MonoBehaviour {
		[Header("Bake Data Container")]
		[SerializeField] private PathFindingGridDataContainer gridDataContainer;

		[Header("Tilemap Targets")]
		[SerializeField] private Tilemap microTilemap;
		[SerializeField] private Tilemap macroTilemap;

		[Header("Rectangle Slicer Selection")]
		[SerializeField] private RectangleSlicerAlgorithm rectangleSlicer = RectangleSlicerAlgorithm.Greedy;

		[Header("Bounding Box Constraints")]
		[SerializeField] private Vec2Int maxBoundingBoxSize = new(16, 16);

		[Header("Error Visualizer Configurations")]
		[SerializeField] private bool logMicroTileNotFoundWarnings = false;
		[SerializeField] private ErrorConfiguration macroErrorConfig = new();
		[SerializeField] private ErrorConfiguration microErrorConfig = new();

		[Header("Region Slice Gizmo Settings")]
		[SerializeField] private bool showRegionSlices = true;
		[SerializeField] private Color sliceBoxColor = new(0f, 1f, 1f, 0.3f);
		[SerializeField] private Color sliceBoxBorderColor = new(0f, 0.8f, 0.8f, 1f);

		[Header("Macro Neighbor Line Gizmo Settings")]
		[SerializeField] private bool showNeighborConnections = true;
		[SerializeField] private Color neighborLineColor = Color.yellow;
		[SerializeField] private float neighborLineThickness = 3f;

		[Header("Error Tracking")]
		[SerializeField] private List<ErrorTileInfo> _macroErrors = new();
		[SerializeField] private List<ErrorTileInfo> _microErrors = new();

		// --- RESTORED: Temporary authoring dictionaries for the two-step Editor workflow ---

		private readonly Dictionary<Vec2Int, HHSIMacroPathFindingTile> _macroTileDictionary = new();
		private readonly Dictionary<Vec2Int, HHSIMicroPathFindingTile> _microTileDictionary = new();

		// Gizmo-Specific Caches
		private readonly Dictionary<BoundingBox, (Vec2Int Anchor, List<Vec2Int> Tiles)> _bakedSlicesCache = new();
		private Dictionary<BoundingBox, List<BoundingBox>> _macroNeighbourCacheForGizmos;

		// Tools
		private readonly RegionExtractionAlgorithm _regionExtractor = new();
		private readonly SliceAnalysisSummarizer _summarizer = new();
		private IRectangleRegionSlicer _rectanglePacker;
		private readonly IMacroNeighbourFinder _neighborFinder = new MacroCardinalNeighbourFinder();

		public bool ShowRegionSlices {
			get => showRegionSlices;
			set {
				showRegionSlices = value;
#if UNITY_EDITOR
				SceneView.RepaintAll();
#endif
			}
		}

		public PathfindingGraphManager CreateRuntimeGraphManager() {
			if (gridDataContainer == null || gridDataContainer.GridData.MicroGridNodeDict == null || gridDataContainer.GridData.MicroGridNodeDict.Count == 0) {
				Debug.LogWarning("PathChecker has no baked data! Did you forget to bake the grid or assign the SO?");
			}

			// var gridData = gridDataContainer.GridData;
			// var microGraph = new MicroGraphManager(gridData.MicroGridNodeDict);
			// var macroGraph = new MacroGraphManager(gridData.MacroGridNodeDict, gridData.MacroAdjacencyListWrapper);

			// return new PathfindingGraphManager(macroGraph, microGraph);
			return null;
		}

		public IRectangleRegionSlicer GetRectangleSlicer(RectangleSlicerAlgorithm slicer) {
			return slicer switch {
				RectangleSlicerAlgorithm.Greedy => new GreedyRectanglePackingAlogorithm(),
				RectangleSlicerAlgorithm.DualPhaseGreedyMeshing => new DualAxisGreedyMeshingAlgorithm(),
				RectangleSlicerAlgorithm.GreedyClusteringHistogramSlicer => new GreedyClusteringHistogramSlicer(),
				RectangleSlicerAlgorithm.GreedyClusteringHistogramSlicerMarshalled => new GreedyClusteringHistogramSlicerMarshalled(),
				RectangleSlicerAlgorithm.PureHistogramSlicer => new PureHistogramRegionSlicer(),
				RectangleSlicerAlgorithm.PureHistogramSlicerMarshalled => new PureHistogramRegionSlicerMarshalled(),
				_ => throw new System.ArgumentOutOfRangeException(nameof(slicer), slicer, null)
			};
		}

		/// <summary>
		/// Step 1: Prepares the raw tile data from the Tilemaps.
		/// Called by the "Prepare Pathfinding Data For Bake" button in the Custom Editor.
		/// </summary>
		public void PreparePathfindingData() {
			PreparePathfindingData<MacroTerrainData, HHSIMacroPathFindingTile>(
				"Macro", this.macroTilemap, this._macroTileDictionary, this._macroErrors);

			PreparePathfindingData<MicroTerrainData, HHSIMicroPathFindingTile>(
				"Micro", this.microTilemap, this._microTileDictionary, this._microErrors);

#if UNITY_EDITOR
			SceneView.RepaintAll();
#endif
		}

		private void PreparePathfindingData<TData, TitledTile>(
			string tilemapName,
			Tilemap targetTilemap,
			IDictionary<Vec2Int, TitledTile> targetDictionary,
			List<ErrorTileInfo> targetErrorList)
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
				Vec2Int cellPos = new(x, y);

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
		}

		/// <summary>
		/// Step 2: Executes the region slicing algorithm.
		/// Called by the "Perform Quote-on-Quote Bake" button in the Custom Editor.
		/// </summary>
		public void QuoteOnQuoteBake() {
			if (this.gridDataContainer == null) {
				Debug.LogError("Grid Data Container is not assigned. Please assign a PathFindingGridDataContainer ScriptableObject.");
				return;
			}

			if (this._macroTileDictionary.Count == 0 || this._microTileDictionary.Count == 0) {
				Debug.LogWarning("Tile dictionaries are empty! Please click 'Prepare Pathfinding Data For Bake' first.");
				return;
			}

			this._rectanglePacker = GetRectangleSlicer(this.rectangleSlicer);

			// Temporary dictionaries meant to be passed to the ScriptableObject
			var microGridNodeDict = new SerializableDictionary<Vec2Int, MicroGridNode>();
			var macroGridNodeDict = new SerializableDictionary<BoundingBox, MacroGridNode>();
			var macroAdjacencyList = new SerializableDictionary<BoundingBox, List<MacroConnectionData>>();

			// ==========================================
			// 1. MACRO-LEVEL NODE GENERATION & SLICING
			// ==========================================

			// Extract all contiguous regions from the macro tile dictionary
			var regionTiles = this._regionExtractor.Extract(this._macroTileDictionary);
			var maxBoundSize = this.maxBoundingBoxSize;

			// Slicing the regions into bounding boxes, and caching the results for gizmo drawing
			System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
			var slicedRegions = this._rectanglePacker.Slice(regionTiles, maxBoundSize);
			stopwatch.Stop();

			if (slicedRegions == null) return;

			// Creating the micro tile and micro-to-macro mapping dictionaries, and 
			// populating the macro grid node dictionary
			int totalSizeForReserveDict = slicedRegions.AsValueEnumerable().Sum(kvp => kvp.Value.RegionTilePositions.Count);

			BoundingBox[] slicedRegionArray = slicedRegions.Keys.AsValueEnumerable().ToArray();

			Dictionary<(int x, int y), BoundingBox> microToMacroMapping = new(totalSizeForReserveDict);

			this._bakedSlicesCache.Clear();

			foreach (var kvp in slicedRegions) {
				this._bakedSlicesCache[kvp.Key] = kvp.Value;
				BoundingBox box = kvp.Key;

				Vec2Int regionAnchor = kvp.Value.regionAnchor;
				HHSIMacroPathFindingTile regionTile = this._macroTileDictionary[regionAnchor];

				TerrainType terrainType = regionTile.Data.TerrainType;
				MovementCapability movementCapability = regionTile.Data.MovementType;

				var currentMacroNode = new MacroGridNode(box, terrainType, movementCapability);
				macroGridNodeDict.Add(box, currentMacroNode);

				// ==========================================
				// 2. MICRO TIER POPULATION
				// ==========================================
				foreach (Vec2Int tilePos in kvp.Value.RegionTilePositions) {
					// Map the individual micro tile position to its macro bounding box container
					microToMacroMapping[(tilePos.X, tilePos.Y)] = box;

					MicroGridNode microNode;

					if (this._microTileDictionary.TryGetValue(tilePos, out var microTile)) {
						microNode = new MicroGridNode(
							tilePos,
							microTile.Data.IsStaticObstacle,
							currentMacroNode
						);
					} else {
						microNode = new MicroGridNode(
							tilePos,
							true,
							currentMacroNode
						);
						if (this.logMicroTileNotFoundWarnings) {
							Debug.LogWarning($"Micro tile not found for position {tilePos}. Skipping micro node creation.");
						}
					}

					if (!microGridNodeDict.ContainsKey(tilePos)) {
						microGridNodeDict.Add(tilePos, microNode);
						currentMacroNode.PrecheckedAddMicroGridNodePosition(tilePos);
					} else {
						Debug.LogWarning($"Duplicate micro node detected at {tilePos}. Overwriting existing node.");
						microGridNodeDict[tilePos] = microNode;
					}
				}
			}

			// ==========================================
			// 3. MACRO CONNECTION BUILD PASS (ADJACENCY)
			// ==========================================
			Dictionary<BoundingBox, List<BoundingBox>> macroNeighbours =
				this._neighborFinder.FindNeighbours(microToMacroMapping, slicedRegionArray);

			this._macroNeighbourCacheForGizmos = macroNeighbours;

			foreach (var kvp in macroNeighbours) {
				BoundingBox fromBox = kvp.Key;
				List<MacroConnectionData> connections = new();

				Vec2Int fromAnchor = this._bakedSlicesCache[fromBox].Anchor;
				MovementCapability fromCapability = this._macroTileDictionary[fromAnchor].Data.MovementType;
				bool toNarrativelyAccessible = this._macroTileDictionary[fromAnchor].Data.IsNarrativelyAccessible;

				foreach (BoundingBox toBox in kvp.Value) {
					Vec2Int toAnchor = this._bakedSlicesCache[toBox].Anchor;
					MovementCapability toCapability = this._macroTileDictionary[toAnchor].Data.MovementType;
					bool fromNarrativelyAccessible = this._macroTileDictionary[toAnchor].Data.IsNarrativelyAccessible;

					MacroConnectionData mcd = MacroConnectionData.CreateConnection(
						toBox, fromCapability, toCapability, toNarrativelyAccessible, fromNarrativelyAccessible);

					connections.Add(mcd);
				}

				macroAdjacencyList[fromBox] = connections;
			}

			// Update the ScriptableObject single source of truth
			this.gridDataContainer.SetGridData(
				microGridNodeDict,
				macroGridNodeDict,
				macroAdjacencyList
			);


			// ==========================================
			// 4. UNIFIED SUMMARY REPORTING & CLEANUP
			// ==========================================
			this._summarizer.MakeSummary(
				this._rectanglePacker,
				maxBoundSize,
				regionTiles != null ? regionTiles.Count : 0,
				slicedRegions,
				stopwatch
			);

			// Cleanup the temporary class dictionaries as requested
			this._macroTileDictionary.Clear();
			this._microTileDictionary.Clear();

#if UNITY_EDITOR
			SceneView.RepaintAll();
#endif
		}

		private void OnDrawGizmos() {
			DrawTilemapErrors(macroTilemap, _macroErrors, macroErrorConfig);
			DrawTilemapErrors(microTilemap, _microErrors, microErrorConfig);

			if (showRegionSlices) {
				MacroRegionSliceGizmos.DrawRegionSlices(
					_bakedSlicesCache,
					macroTilemap,
					sliceBoxColor,
					sliceBoxBorderColor
				);
			}

			if (showNeighborConnections) {
				MacroCardinalNeighbourGizmos.DrawNeighbourLine(
					_macroNeighbourCacheForGizmos,
					macroTilemap,
					neighborLineColor,
					neighborLineThickness,
					0.1f
				);
			}
		}

		private void DrawTilemapErrors(Tilemap targetTilemap, List<ErrorTileInfo> errors, ErrorConfiguration config) {
			if (targetTilemap == null || errors == null || errors.Count == 0) return;

			foreach (var err in errors) {
				Gizmos.color = config.GetColor(err.ErrorType);

				Vector3 worldPos = targetTilemap.CellToWorld(new Vector3Int(err.Position.X, err.Position.Y, 0));
				Vector3 centerPos = worldPos + new Vector3(targetTilemap.cellSize.x * 0.5f, targetTilemap.cellSize.y * 0.5f, 0f);

				float radius = 0.4f;

#if UNITY_EDITOR
				radius = HandleUtility.GetHandleSize(centerPos) * 0.25f;
#endif
				Gizmos.DrawWireSphere(centerPos, radius);
			}
		}
	}
}