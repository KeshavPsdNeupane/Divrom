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
using Project.Scripts.Features.PathFinding.GraphManager; // For MacroConnectionData & Managers

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

	/// <summary>
	/// Defines available algorithms used to group adjacent grid tiles into optimal macro regions.
	/// </summary>
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

	/// <summary>
	/// Validates and constructs the hierarchical pathfinding grid (Macro and Micro tiers) directly from Unity Tilemaps.
	/// </summary>
	/// <remarks>
	/// Operates in edit-mode to visualize slicing algorithms, report grid errors, and preview neighbor connections.
	/// Acts as the authoring tool and serialized data container for the pathfinding system.
	/// </remarks>
	[ExecuteAlways]
	public class PathChecker : MonoBehaviour {

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
		[SerializeField] private Color sliceBoxColor = new(0f, 1f, 1f, 0.3f); // Translucent Cyan
		[SerializeField] private Color sliceBoxBorderColor = new(0f, 0.8f, 0.8f, 1f); // Opaque Cyan

		[Header("Macro Neighbor Line Gizmo Settings")]
		[SerializeField] private bool showNeighborConnections = true;
		[SerializeField] private Color neighborLineColor = Color.yellow;
		[SerializeField] private float neighborLineThickness = 3f;

		// Authoring-time dictionaries for raw tiles
		private readonly SerializableDictionary<Vec2Int, HHSIMacroPathFindingTile> _macroTileDictionary = new();
		private readonly SerializableDictionary<Vec2Int, HHSIMicroPathFindingTile> _microTileDictionary = new();


		[Header("Error Tracking")]
		[SerializeField] private List<ErrorTileInfo> _macroErrors = new();
		[SerializeField] private List<ErrorTileInfo> _microErrors = new();



		// Baked graph node dictionaries (Saved in scene)
		[SerializeField, HideInInspector][ReadOnly] private SerializableDictionary<Vec2Int, MicroGridNode> _microGridNodeDict = new();
		[SerializeField, HideInInspector][ReadOnly] private SerializableDictionary<BoundingBox, MacroGridNode> _macroGridNodeDict = new();
		[SerializeField, HideInInspector][ReadOnly] private SerializableDictionary<BoundingBox, List<MacroConnectionData>> _macroAdjacencyList = new();

		private PathfindingGraphManager _runtimeGraphManager;

		private readonly RegionExtractionAlgorithm _regionExtractor = new();
		private readonly SliceAnalysisSummarizer _summarizer = new();
		private IRectangleRegionSlicer _rectanglePacker;
		private readonly IMacroNeighbourFinder _neighborFinder = new MacroCardinalNeighbourFinder();

		// Gizmos cache for baked slices to avoid recalculating during OnDrawGizmos
		private readonly Dictionary<BoundingBox, (Vec2Int Anchor, List<Vec2Int> Tiles)> _bakedSlicesCache = new();
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

		/// <summary>
		/// Constructs the pure C# runtime graph manager using the baked dictionary data.
		/// Call this at runtime during your systems initialization phase.
		/// </summary>
		public PathfindingGraphManager CreateRuntimeGraphManager() {
			if (_microGridNodeDict.Count == 0 || _macroGridNodeDict.Count == 0) {
				Debug.LogWarning("PathChecker has no baked data! Did you forget to bake the grid?");
			}

			var microGraph = new MicroGraphManager(this._microGridNodeDict);
			var macroGraph = new MacroGraphManager(this._macroGridNodeDict, this._macroAdjacencyList);

			return new PathfindingGraphManager(macroGraph, microGraph);
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
		/// Reads all raw tile data from the assigned Tilemaps and flags misconfigured or conflicting tiles.
		/// </summary>
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
			SerializableDictionary<Vec2Int, TitledTile> targetDictionary,
			List<ErrorTileInfo> targetErrorList,
			bool repaint = true)
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

#if UNITY_EDITOR
			if (repaint) {
				SceneView.RepaintAll();
			}
#endif
		}

		/// <summary>
		/// Executes the region slicing algorithm to convert raw grid tiles into a simplified hierarchical node graph.
		/// </summary>
		public void QuoteOnQuoteBake() {
			this._rectanglePacker = GetRectangleSlicer(this.rectangleSlicer);

			// Clear previous data
			this._microGridNodeDict.Clear();
			this._macroGridNodeDict.Clear();
			this._macroAdjacencyList.Clear();

			// ==========================================
			// 1. MACRO-LEVEL NODE GENERATION & SLICING
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

				Vec2Int regionAnchor = kvp.Value.regionAnchor;
				HHSIMacroPathFindingTile regionTile = this._macroTileDictionary[regionAnchor];

				TerrainType terrainType = regionTile.Data.TerrainType;
				MovementCapability movementCapability = regionTile.Data.MovementType;

				var currentMacroNode = new MacroGridNode(box, terrainType, movementCapability);
				this._macroGridNodeDict.Add(box, currentMacroNode);

				// ==========================================
				// 2. MICRO TIER POPULATION
				// ==========================================
				foreach (Vec2Int tilePos in kvp.Value.RegionTilePositions) {
					MicroGridNode microNode;

					if (this._microTileDictionary.TryGetValue(tilePos, out var microTile)) {
						microNode = new MicroGridNode(
							tilePos,
							microTile.Data.IsStaticObstacle,
							currentMacroNode
						);
					} else {
						// If no micro tile is found, default to obstacle and warn.
						microNode = new MicroGridNode(
							tilePos,
							true,
							currentMacroNode
						);
						if (this.logMicroTileNotFoundWarnings) {
							Debug.LogWarning($"Micro tile not found for position {tilePos}. Skipping micro node creation.");
						}
					}

					if (!this._microGridNodeDict.ContainsKey(tilePos)) {
						this._microGridNodeDict.Add(tilePos, microNode);

						// Safe to precheck add since we verified uniqueness in the dictionary above.
						currentMacroNode.PrecheckedAddMicroGridNodePosition(tilePos);
					} else {
						Debug.LogWarning($"Duplicate micro node detected at {tilePos}. Overwriting existing node.");
						this._microGridNodeDict[tilePos] = microNode;
					}
				}
			}

			// ==========================================
			// 3. MACRO CONNECTION BUILD PASS (ADJACENCY)
			// ==========================================
			Dictionary<BoundingBox, List<BoundingBox>> macroNeighbours =
				this._neighborFinder.FindNeighbours(microToMacroMapping, slicedRegionArray);

			this._macroNeighbourCacheForGizmos = macroNeighbours;

			// Translate raw box neighbors into MacroConnectionData structs for the runtime manager
			foreach (var kvp in macroNeighbours) {
				BoundingBox fromBox = kvp.Key;
				List<MacroConnectionData> connections = new List<MacroConnectionData>();

				// Get origin capability
				Vec2Int fromAnchor = this._bakedSlicesCache[fromBox].Anchor;
				MovementCapability fromCapability = this._macroTileDictionary[fromAnchor].Data.MovementType;
				bool toNarrativelyAccessible = this._macroTileDictionary[fromAnchor].Data.IsNarrativelyAccessible;
				foreach (BoundingBox toBox in kvp.Value) {
					// Get destination capability
					Vec2Int toAnchor = this._bakedSlicesCache[toBox].Anchor;
					MovementCapability toCapability = this._macroTileDictionary[toAnchor].Data.MovementType;

					// Combine capabilities just like MacroGraphManager.AddConnection does
					MovementCapability combinedCapability = fromCapability | toCapability;
					bool narritivelyAccessible = toNarrativelyAccessible &&
					this._macroTileDictionary[toAnchor].Data.IsNarrativelyAccessible;
					connections.Add(new MacroConnectionData(toBox, combinedCapability, narritivelyAccessible));
				}

				this._macroAdjacencyList[fromBox] = connections;
			}

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

			// Free temporary buildup data. (We keep the baked dicts)
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