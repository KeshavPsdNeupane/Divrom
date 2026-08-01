using System;
using System.Collections.Generic;
using System.Diagnostics;
using Kope.Core.Attribute;
using Kope.Feature.PathFindingNew.Storage;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;
using Kope.Feature.PathFindingNew.Interface;


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

		[Message(
			"Bakes authoring tiles into the high-performance bit-packed Storage Domain.\n" +
			"Step 1: Prepare (Reads Tilemap into memory)\n" +
			"Step 2: Bake (Pushes memory to ScriptableObject and triggers serialization)",
			MessageSeverity.Info
		)]
		[Header("Bake Target")]
		[SerializeField] private GridDataStorageBase gridDataStorage;

		[Header("Source Data")]
		[SerializeField] private Tilemap terrainTilemap;

		[Header("Error Tracking")]
		[SerializeField] private ErrorConfiguration errorConfig = new();
		[SerializeField] private List<ErrorTileInfo> _bakeErrors = new();

		#endregion

		#region Editor Memory Cache

		// Temporarily holds the authoring data during editor time before pushing to the SO.
		private readonly Dictionary<Vec2Int, TileTerrainData> _editorTileMemory = new();

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

			this._stopwatch.Restart();

			// Push memory dictionary directly across the storage contract boundary
			this.gridDataStorage.SetGridData(this._editorTileMemory);

			this._stopwatch.Stop();
			Debug.Log($"[PathFindingGridBaker] Successfully baked {this._editorTileMemory.Count} tiles into " +
			$"Storage Domain in {this._stopwatch.ElapsedMilliseconds}ms.");

			// Clear editor memory cache to free RAM after bake is complete
			this._editorTileMemory.Clear();
		}

		#endregion

		#region Gizmos Rendering

		private void OnDrawGizmos() {
			if (this.terrainTilemap == null || this._bakeErrors == null || this._bakeErrors.Count == 0) return;

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

		#endregion
	}
}