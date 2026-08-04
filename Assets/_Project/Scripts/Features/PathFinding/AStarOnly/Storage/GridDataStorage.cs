using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;
using ZLinq;

namespace Kope.Feature.PathFindingNew.Storage {

	/*
	 * ==============================================================================================
	 * CONCRETE TILE DATA STORAGE ASSET (BIT-PACKED SOA IMPLEMENTATION)
	 * ==============================================================================================
	 * 
	 * Encapsulates `TileStorageData` as its internal TStorage serialization medium.
	 * Bakes `TileTerrainData` via `TileDataCodexStorageStream.BakeStatic` and rehydrates 
	 * `GridNode` dictionaries via `TileDataCodexStorageStream.HydrateStatic`.
	 * ==============================================================================================
	 */

	/// <summary>
	/// ScriptableObject asset container storing baked terrain pathfinding grid data in a bit-packed Structure-of-Arrays format.
	/// </summary>
	[CreateAssetMenu(
		fileName = "PathFindingGridDataStorage",
		menuName = "Scriptable Objects/PathFindingNew/Storage/Grid Data Storage"
	)]
	public class GridDataStorage : GridDataStorageBase {
		[Message(
			"Note: Due to Unity Inspector limitations with dynamic-height elements, " +
			"these fields cannot be fully locked with ReadOnly and can technically be mutated.\n\n" +
			"Manual modification is strongly discouraged. This data is exposed strictly " +
			"for debugging and verifying data integrity. Please leave these fields alone, " +
			"as any manual edits will be overwritten on the next bake.",
			MessageSeverity.Warning
		)]
		[Header("Baked Global Data")]
		[SerializeField] private RegionStorageData _regionStorageData;

		/// <summary>
		/// A dictionary mapping region IDs to the list of tile positions that belong to that region.
		/// </summary>
		private RuntimeDataCache _runtimeDataCache;

		/// <inheritdoc />
		public override Dictionary<Vec2Int, GridNode> GridNodeDict {
			get {
				if (this._runtimeDataCache == null) {
					BuildRuntimeCache();
				}
				return this._runtimeDataCache.GridData;
			}
		}

#if UNITY_EDITOR
		// why this is not directly serialized from the Codex? Because Codex is for storage, 
		// and this is a editor gizmo visualization, which is not needed at runtime. So we keep 
		// it separate to avoid unnecessary data in the build.
		public override Dictionary<ushort, Vec2Int[]> RegionTilePositions {
			get {
				if (this._runtimeDataCache == null) {
					BuildRuntimeCache();
				}
				return this._runtimeDataCache.RegionData;
			}
		}
#endif

		/// <inheritdoc />
		public override void ClearRuntimeCache() {
			this._runtimeDataCache?.GridData.Clear();
			this._runtimeDataCache?.RegionData.Clear();

			this._runtimeDataCache = null;
		}


		protected override void SetGridDataInternal(IDictionary<ushort, List<(Vec2Int position, TileTerrainData data)>> gridNodeDict) {
			this._regionStorageData = TileDataCodexStorageStream.BakeStatic(gridNodeDict);
		}

		/// <summary>
		/// Hydrates internal bit-packed storage (`TileStorageData`) into the active runtime dictionary (`GridNode`).
		/// </summary>
		private void BuildRuntimeCache() {
			this._runtimeDataCache ??= TileDataCodexStorageStream.HydrateStatic(this._regionStorageData);
		}
	}
}