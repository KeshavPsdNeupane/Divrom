using System;
using System.Collections.Generic;
using System.Diagnostics;
using Kope.Feature.PathFindingNew.Storage;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;
using Kope.Feature.PathFindingNew.Interface;
using System.Text;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFindingNew.Baking {

	#region Enums, Interfaces & Error Configurations
	public enum PathCheckerErrorType {
		InvalidTile = 0,
		DuplicateTile = 1
	}

	[Serializable]
	public struct ErrorTileInfo {
		public Vec2Int Position;
		public PathCheckerErrorType ErrorType;
		public string ErrorMessage;

		public ErrorTileInfo(Vec2Int position, PathCheckerErrorType errorType, string errorMessage) {
			this.Position = position;
			this.ErrorType = errorType;
			this.ErrorMessage = errorMessage;
		}
	}

	[Serializable]
	public class ErrorConfiguration {
		[Header("Gizmo Colors")]
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

	#endregion

	[ExecuteAlways]
	public class PathFindingGridBaker : MonoBehaviour {

		#region Serialized Fields
		[Header("Bake Target")]
		[SerializeField] private GridDataStorageBase gridDataStorage;

		[Header("Source Data")]
		[SerializeField] private Tilemap terrainTilemap;

		[Header("Region Id Settings")]
		[SerializeField] private bool showRegion = true;
		[SerializeField] private bool showRegionLabels = false;
		[SerializeField, Range(0f, 0.6f)] private float regionGizmoAlpha = 0.6f;
		[SerializeField] private Color nonTraversableRegionColor = new(0.35f, 0.35f, 0.35f, 1f);
		[SerializeField] private Color regionLabelColor = Color.white;

		[Header("Error Tracking")]
		[SerializeField] private ErrorConfiguration errorConfig = new();
		[SerializeField] private List<ErrorTileInfo> _bakeErrors = new();




		#endregion

		#region Editor Memory Cache

		// Temporarily holds the authoring data during editor time before pushing to the SO.
		private readonly Dictionary<Vec2Int, TileTerrainData> _editorTileMemory = new();
		private Dictionary<ushort, List<(Vec2Int position, TileTerrainData data)>> _regionIdMap = new();



		private readonly RegionIdExtraction _regionExtractor = new();

		/// <summary>
		/// Stopwatch for measuring bake duration in milliseconds. Used for performance tracking and logging.
		/// Caching the Stopwatch instance to avoid repeated allocations during multiple bake operations.
		/// </summary>
		private readonly Stopwatch _stopwatch = new();

		#endregion

		#region Public Pipeline API

		/// <summary>
		/// Step 1: Prepares the raw tile data from the Tilemap into the local editor memory dictionary.
		/// Called by the Custom Editor prior to baking.
		/// </summary>
		public void PreparePathfindingData() {
			if (this.terrainTilemap == null) {
				Debug.LogError("[PathFindingGridBaker] Terrain Tilemap reference is missing.");
				return;
			}

			this._editorTileMemory.Clear();
			this._bakeErrors.Clear();

			BoundsInt bounds = this.terrainTilemap.cellBounds;
			TileBase[] allTiles = this.terrainTilemap.GetTilesBlock(bounds);

			this._stopwatch.Restart();

			for (int i = 0; i < allTiles.Length; i++) {
				TileBase tile = allTiles[i];
				if (tile == null) continue;

				// Calculate 2D position
				int x = bounds.x + (i % bounds.size.x);
				int y = bounds.y + (i / bounds.size.x);
				Vec2Int cellPos = new(x, y);

				// Validate tile type
				if (tile is not ITerrainDataTile authoringTile) {
					this._bakeErrors.Add(new ErrorTileInfo(
						cellPos,
						PathCheckerErrorType.InvalidTile,
						$"Invalid tile assigned at {cellPos}. Must implement ITerrainDataTile."
					));
					continue;
				}

				if (this._editorTileMemory.ContainsKey(cellPos)) {
					this._bakeErrors.Add(new ErrorTileInfo(
						cellPos,
						PathCheckerErrorType.DuplicateTile,
						$"Duplicate tile conflict at {cellPos}."
					));
				} else {
					this._editorTileMemory[cellPos] = authoringTile.GetTerrainData();
				}
			}

			this._stopwatch.Stop();
			Debug.Log($"[PathFindingGridBaker] Prepared {this._editorTileMemory.Count} tiles and {this._bakeErrors.Count}" +
			$" errors into editor memory in {this._stopwatch.ElapsedMilliseconds}ms.");

#if UNITY_EDITOR
			SceneView.RepaintAll();
#endif
		}

		/// <summary>
		/// Step 2: Takes the editor memory and pushes it into the ScriptableObject storage contract.
		/// </summary>
		public void QuoteOnQuoteBake() {
			if (this.gridDataStorage == null) {
				Debug.LogError("[PathFindingGridBaker] Grid Data Storage SO is missing!");
				return;
			}

			if (this._editorTileMemory.Count == 0) {
				Debug.LogWarning("[PathFindingGridBaker] Editor tile memory is empty! Did you run 'Prepare' first?");
				return;
			}

			this._regionIdMap = this._regionExtractor.ExtractRegion(this._editorTileMemory);


			StringBuilder regionSummary = new();
			this._stopwatch.Restart();

			gridDataStorage.SetGridData(this._regionIdMap);

			this._stopwatch.Stop();

			regionSummary.AppendLine($"[PathFindingGridBaker] Successfully baked {this._editorTileMemory.Count} tiles into " +
			$"Storage Domain in {this._stopwatch.ElapsedMilliseconds}ms.");

			regionSummary.AppendLine($"[PathFindingGridBaker] Extracted {this._regionIdMap.Count} regions.");
			foreach (var kvp in this._regionIdMap) {
				ushort regionId = kvp.Key;
				int tileCount = kvp.Value.Count;
				regionSummary.AppendLine($"Region ID {regionId}: {tileCount} tiles.");
			}
			Debug.Log(regionSummary.ToString());


			// Clear editor memory cache to free RAM after bake is complete
			this._editorTileMemory.Clear();
		}

		#endregion

		#region Gizmos Rendering

		private void OnDrawGizmos() {
			if (this.terrainTilemap == null) return;

#if UNITY_EDITOR
			if (this.showRegion && this.gridDataStorage != null && this.gridDataStorage.RegionTilePositions != null
			&& this.gridDataStorage.RegionTilePositions.Count > 0) {
				RegionGizmoUtility.OnGizmoDraw(
					this.gridDataStorage.RegionTilePositions,
					this.terrainTilemap,
					this.nonTraversableRegionColor,
					this.regionGizmoAlpha,
					this.showRegionLabels,
					this.regionLabelColor);
			}
#endif
			if (this._bakeErrors != null) {
				foreach (var err in this._bakeErrors) {
					Gizmos.color = this.errorConfig.GetColor(err.ErrorType);

					Vector3 worldPos = this.terrainTilemap.CellToWorld(new Vector3Int(err.Position.X, err.Position.Y, 0));
					Vector3 centerPos = worldPos + new Vector3(this.terrainTilemap.cellSize.x * 0.5f, this.terrainTilemap.cellSize.y * 0.5f, 0f);

					float radius = 0.4f;

#if UNITY_EDITOR
					radius = HandleUtility.GetHandleSize(centerPos) * 0.25f;
#endif
					Gizmos.DrawWireSphere(centerPos, radius);
				}
			}
		}

		#endregion
	}
}