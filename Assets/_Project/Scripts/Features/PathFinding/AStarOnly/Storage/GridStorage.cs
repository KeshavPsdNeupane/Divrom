using System.Collections.Generic;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFindingNew.Storage {

	/*
	 * ==============================================================================================
	 * ARCHITECTURAL RATIONALE: STORAGE CONTAINER CONTRACT (TEditor -> TRuntime)
	 * ==============================================================================================
	 * 
	 * WHY IS THERE NO TStorage GENERIC ON THIS BASE CLASS?
	 * 
	 * [1. ENCAPSULATION OF SERIALIZATION MEDIUM]
	 * The ScriptableObject container exposes a public two-point interface:
	 *   - Input Boundary  : Receives high-level authoring data (TEditor) during the bake phase.
	 *   - Output Boundary : Exposes high-performance execution nodes (TRuntime) at runtime.
	 * 
	 * How the concrete asset subclass encodes, packs, or compresses data into serialized fields 
	 * (TStorage) is a private internal detail. The public consumer (Pathfinding Engine or Editor) 
	 * should never need to know whether data is stored as a bit-packed array, RLE stream, or binary blob.
	 * 
	 * [2. PREVENTING GENERIC CONTAGION]
	 * Omitting TStorage prevents generic clutter from propagating into client managers and services.
	 * Clients only need to query `GridNodeDict` without declaring serialization type constraints.
	 * ==============================================================================================
	 */

	/// <summary>
	/// Abstract generic ScriptableObject container for baked graph/grid data assets.
	/// Bridges authoring tile inputs (<typeparamref name="TEditor"/>) to execution grid outputs (<typeparamref name="TRuntime"/>).
	/// </summary>
	/// <typeparam name="TEditor">The authoring tile domain data type (e.g., <see cref="TileTerrainData"/>).</typeparam>
	/// <typeparam name="TRuntime">The runtime grid domain node data type (e.g., <see cref="GridNode"/>).</typeparam>
	public abstract class GridDataStorageBaseGeneric<TEditor, TRuntime> : ScriptableObject {

		/// <summary>
		/// Gets the $O(1)$ runtime spatial lookup dictionary mapping grid coordinates to execution nodes.
		/// Lazily rehydrates from internal storage upon first access.
		/// </summary>
		public abstract Dictionary<Vec2Int, TRuntime> GridNodeDict { get; }

		/// <summary>
		/// Clears non-serialized runtime caches (e.g., rehydrated runtime dictionaries) to reclaim memory.
		/// </summary>
		public abstract void ClearRuntimeCache();

		/// <summary>
		/// Bakes fresh authoring tile data into internal persistent storage and forces rehydration of runtime caches.
		/// </summary>
		/// <param name="gridNodeDict">Authoring tile domain lookup dictionary.</param>
		public void SetGridData(TEditor gridNodeDict) {
			// Clear existing runtime cache prior to mutation
			ClearRuntimeCache();

			SetGridDataInternal(gridNodeDict);

			// Invalidate cache to ensure subsequent reads pull fresh data from internal storage
			ClearRuntimeCache();

#if UNITY_EDITOR
			// Save and mark dirty only within Editor builds
			SaveAndDirtyAsset();
#endif
		}

		/// <summary>
		/// Internal serialization handler implemented by concrete storage assets to bake <typeparamref name="TEditor"/> data.
		/// </summary>
		/// <param name="gridNodeDict">Authoring tile domain lookup dictionary to serialize.</param>
		protected abstract void SetGridDataInternal(TEditor gridNodeDict);

#if UNITY_EDITOR
		/// <summary>
		/// Marks the ScriptableObject dirty and persists serialized changes to disk within Unity Editor.
		/// </summary>
		protected void SaveAndDirtyAsset() {
			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssets();
		}
#endif

		/// <summary>
		/// Unity lifecycle hook. Clears runtime caches when the asset is disabled or unloaded.
		/// </summary>
		protected virtual void OnDisable() {
			ClearRuntimeCache();
		}
	}

	/// <summary>
	/// Concrete non-generic base class alias for standard tile terrain storage assets.
	/// Maps <see cref="TileTerrainData"/> authoring data to <see cref="GridNode"/> runtime execution nodes.
	/// </summary>
	public abstract class GridDataStorageBase : GridDataStorageBaseGeneric
	<IDictionary<ushort, List<(Vec2Int position, TileTerrainData data)>>, GridNode> {
		public abstract Dictionary<ushort, Vec2Int[]> RegionTilePositions { get; }
	}
}