using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;

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
		[SerializeField] private GridStorageData _gridStorageData;

		private Dictionary<Vec2Int, GridNode> _gridNodeDict;

		/// <inheritdoc />
		public override Dictionary<Vec2Int, GridNode> GridNodeDict {
			get {
				if (this._gridNodeDict == null) {
					BuildRuntimeCache();
				}
				return this._gridNodeDict;
			}
		}

		/// <inheritdoc />
		public override void ClearRuntimeCache() {
			this._gridNodeDict?.Clear();
			this._gridNodeDict = null;
		}

		/// <inheritdoc />
		protected override void SetGridDataInternal(Dictionary<Vec2Int, TileTerrainData> gridNodeDict) {
			this._gridStorageData = TileDataCodexStorageStream.BakeStatic(gridNodeDict);
		}

		/// <summary>
		/// Hydrates internal bit-packed storage (`TileStorageData`) into the active runtime dictionary (`GridNode`).
		/// </summary>
		private void BuildRuntimeCache() {
			this._gridNodeDict ??= TileDataCodexStorageStream.HydrateStatic(this._gridStorageData);
		}
	}
}