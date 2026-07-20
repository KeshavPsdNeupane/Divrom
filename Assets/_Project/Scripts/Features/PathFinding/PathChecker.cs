using Kope.Feature.PathFinding.Tile;
using UnityEngine;
using UnityEngine.Tilemaps;
using Kope.Core.Collections;
using Kope.Feature.PathFinding;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Feature.PathFinding.Interface;
using System.Text;
using Kope.Feature.PathFinding.Utility;





#if UNITY_EDITOR
using UnityEditor;
#endif

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


	[Header("Bouding Box Constraints")]
	[SerializeField] private Vector2Int maxBoundingBoxSize = new(16, 16);

	[Header("Error Visualizer Configurations")]
	[SerializeField] private ErrorConfiguration macroErrorConfig = new();
	[SerializeField] private ErrorConfiguration microErrorConfig = new();

	/// <summary>
	/// A dictionary mapping grid positions to their corresponding <see cref="HHSIMacroPathFindingTile"/> instances.
	/// This is used to quickly look up tile data during the pathfinding bake process.
	/// After baking it will be cleared to free up memory, as the baked data will be stored in a more efficient format.
	/// </summary>
	// Optimization Note: Constructed with Vector2IntComparer.Instance instead of the implicit
	// default comparer. Before, every ContainsKey/lookup against this dictionary during
	// PreparePathfindingData and QuoteOnQuoteBake paid for Vector2Int's collision-prone default
	// hash (see Vector2IntComparer.cs). Now, lookups on this tile grid spread evenly across
	// buckets. This required adding a comparer-accepting constructor to SerializableDictionary
	// itself, since its previous constructors always defaulted the backing Dictionary's comparer.
	private readonly SerializableDictionary<Vector2Int, HHSIMacroPathFindingTile> _macroTileDictionary = new(Vector2IntComparer.Instance);
	private readonly SerializableDictionary<Vector2Int, HHSIMicroPathFindingTile> _microTileDictionary = new(Vector2IntComparer.Instance);
	/// <summary>
	/// A dictionary mapping grid positions to their corresponding <see cref="MicroGridNode"/> instances.
	/// This is used to store the micro-level pathfinding nodes that are generated during the bake process.
	/// After baking, this dictionary will be cleared to free up memory, as the baked data will be stored
	/// in a more efficient format.
	/// </summary>
	// Optimization Note: Same reasoning as _macroTileDictionary above — this dict is rebuilt from
	// scratch every bake (QuoteOnQuoteBake step 1) with one insert per micro tile, and downstream
	// systems will look nodes up here by position, so a well-distributed hash pays off on both ends.
	private readonly SerializableDictionary<Vector2Int, MicroGridNode> _microGridNodeDict = new(Vector2IntComparer.Instance);

	// Note: left on the default comparer. Its key is BoundingBox, not Vector2Int, and I don't
	// have that type's field layout/GetHashCode here to know whether it has the same collision
	// problem or whether a custom comparer would even be safe to write for it — flagging rather
	// than guessing.
	private readonly SerializableDictionary<BoundingBox, MacroGridNode> _macroGridNodeDict = new();

	[Header("Error Tracking")]
	[SerializeField] private List<ErrorTileInfo> _macroErrors = new();
	[SerializeField] private List<ErrorTileInfo> _microErrors = new();


	private readonly RegionExtractionAlgorithm _regionExtractor = new();
	private readonly GreedyRectanglePackingAlogorithm _rectanglePacker = new();

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
		Debug.Log("[Pipeline] Starting Quote-on-Quote bake process...");
		// ==========================================
		// 1. MICRO-LEVEL NODE GENERATION
		// ==========================================
		// Why initialize the micro nodes first, even though micro nodes eventually need references 
		// back to the macro structure? Because this volatile raw tile dictionary is parsed first 
		// to instantiate the fundamental micro grid nodes. Once established, the macro layer will 
		// be built on top of this data, and parent-child cross-references will be bound back down 
		// into the micro nodes. 
		// Note: _microTileDictionary is volatile and will be cleared post-bake, but these 
		// generated MicroGridNode instances persist into the final baked navigation structure.
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
		// 2. MACRO-LEVEL NODE GENERATION (PENDING ALGORITHM)
		// ==========================================
		// Processes the macro tile map layout. Before macro nodes can be instantiated and 
		// mapped to their respective BoundingBox keys, we need to implement a greedy rectangle 
		// packing algorithm to group adjacent homogeneous tiles into optimized macro-regions.
		// Once zoning regions are calculated, macro nodes will be populated and linked downward 
		// to the micro grid nodes generated above.

		// #Region extraction
		Debug.Log("[Pipeline] Extracting macro regions from tilemap...");
		var regionTiles = this._regionExtractor.Extract(this._macroTileDictionary);

		StringBuilder regionSummary = new();
		foreach (var kvp in regionTiles) {
			Vector2Int anchorPos = kvp.Key;
			List<Vector2Int> tilesInRegion = kvp.Value;
			regionSummary.AppendLine($"Macro region starting at {anchorPos} contains {tilesInRegion.Count} tiles.\n");
		}

		Debug.Log($"[Pipeline] Region summary(total region ={regionTiles.Count}): \n" + regionSummary.ToString());

		var maxBoundSize = this.maxBoundingBoxSize;

		StringBuilder slicingSummary = new();
		Debug.Log("[Pipeline] Slicing macro regions into bounding boxes with max size " + maxBoundSize);
		var slicededRegions = this._rectanglePacker.Slice(regionTiles, maxBoundSize);
		foreach (var kvp in slicededRegions) {
			BoundingBox bounds = kvp.Key;
			var (anchor, tilesInRegion) = kvp.Value;
			slicingSummary.AppendLine($"Sliced macro region starting at {anchor} with bounds {bounds} contains {tilesInRegion.Count} tiles.\n");
		}
		Debug.Log($"[Pipeline] Slicing summary(total slices ={slicededRegions.Count}): \n" + slicingSummary.ToString());


		Debug.Log("[Pipeline] Quote-on-Quote bake process completed.");
	}

	private void OnDrawGizmos() {
		DrawTilemapErrors(macroTilemap, _macroErrors, macroErrorConfig);
		DrawTilemapErrors(microTilemap, _microErrors, microErrorConfig);
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
}


#if UNITY_EDITOR
[CustomEditor(typeof(PathChecker))]
public class PathCheckerEditor : Editor {
	public override void OnInspectorGUI() {
		DrawDefaultInspector();

		EditorGUILayout.Space();
		EditorGUILayout.HelpBox(
			"Pathfinding Preparation & Validation\n\n" +
			"• Purpose: Validates tilemaps, flags classification errors or duplicates, and builds valid datasets.\n" +
			"• Visualization: Errors are marked in the Scene view with configurable, color-coded indicators matching specific error types and tilemaps.\n\n" +
			"Workflow:\n" +
			"1. Click 'Prepare Pathfinding Data For Bake' to scan maps.\n" +
			"2. Inspect color-coded visual markers in the Scene view.\n" +
			"3. Rectify invalid slots, then re-run preparation.",
			MessageType.Info
		);

		PathChecker pathChecker = (PathChecker)target;

		EditorGUILayout.Space();
		if (GUILayout.Button("Prepare Pathfinding Data For Bake")) {
			pathChecker.PreparePathfindingData();
			EditorUtility.SetDirty(pathChecker);
		}
		if (GUILayout.Button("Perform Quote-on-Quote Bake")) {
			pathChecker.QuoteOnQuoteBake();
		}
	}
}
#endif