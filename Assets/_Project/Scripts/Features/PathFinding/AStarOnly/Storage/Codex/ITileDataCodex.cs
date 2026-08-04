using System.Collections.Generic;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;

namespace Kope.Feature.PathFindingNew.Storage {

	/*
	 * ==============================================================================================
	 * ARCHITECTURAL RATIONALE: WHY A 3-TYPE GENERIC CONTRACT? <TBake, TStorage, TRuntime>
	 * ==============================================================================================
	 * 
	 * Why not 2 generic types <TStorage, TRuntime> or 1 single type?
	 * 
	 * [1. COMPLETE LIFECYCLE DECOUPLING]
	 * Pathfinding pipelines pass through 3 distinct operational phases with conflicting data goals:
	 *   - TBake    (Authoring Domain) : High-level, rich authoring data (editors, tilemaps, colors, ScriptableObjects).
	 *   - TStorage (Storage Domain)   : Bit-packed primitive arrays (long[], int[], byte[]) for $O(1)$ disk/RAM streaming.
	 *   - TRuntime (Grid Data Domain) : Immutable, cache-friendly structs (GridNode) stripped for fast A* inner loops.
	 * 
	 * [2. PREVENTING DOMAIN POLLUTION (Why TBake != TRuntime)]
	 * If we collapsed TBake and TRuntime into a single type:
	 *   - Scenario A (Runtime carries Bake data): Pathfinding search loops would waste CPU cache lines 
	 *     reading editor colors, inspector flags, and object references.
	 *   - Scenario B (Bake uses Runtime data): Level design tools lose editor context, visualization 
	 *     helpers, and procedural authoring metadata.
	 * 
	 * [3. PIPELINE EXTENSIBILITY & MOCK TESTING (Why TBake is explicit)]
	 * Hardcoding the input type to a concrete authoring struct locks the codex to one input source. 
	 * With <TBake>, the baking engine can process alternate input formats without changing runtime code:
	 *   - Baking from procedural noise maps / heightmap generators directly into TStorage.
	 *   - Baking from unit tests using mock tile structs.
	 *   - Baking from custom third-party tilemap assets or external editor formats.
	 * 
	 * ==============================================================================================
	 */

	/// <summary>
	/// Generic contract for domain-converting tile codex engines (Encoder / Decoder).
	/// Manages the full domain lifecycle: Authoring Tile Domain (<typeparamref name="TBake"/>) 
	/// <c>-&gt;</c> Primitive Storage Domain (<typeparamref name="TStorage"/>) 
	/// <c>-&gt;</c> Runtime Grid Data Domain (<typeparamref name="TRuntime"/>).
	/// </summary>
	/// <typeparam name="TBake">The authoring tile domain data type (e.g., <see cref="TileTerrainData"/>).</typeparam>
	/// <typeparam name="TStorage">The primitive storage domain container type (e.g., <see cref="GridStorageData"/>).</typeparam>
	/// <typeparam name="TRuntime">The runtime grid data domain node type (e.g., <see cref="GridNode"/>).</typeparam>
	public interface ITileDataCodex<TBaseDict, TStorage, TRuntime> {

		/// <summary>
		/// Bakes an authoring tile domain dictionary (<typeparamref name="TBake"/>) into a primitive storage domain payload (<typeparamref name="TStorage"/>).
		/// </summary>
		/// <param name="tileDict">Dictionary containing authoring tile terrain data.</param>
		/// <returns>A bit-packed primitive storage container instance.</returns>
		TStorage Bake(TBaseDict tileDict);

		/// <summary>
		/// Re-hydrates a primitive storage domain payload (<typeparamref name="TStorage"/>) into the execution grid data domain lookup dictionary mapping coordinates to <typeparamref name="TRuntime"/> nodes.
		/// </summary>
		/// <param name="storageData">The baked primitive storage container to decode.</param>
		/// <returns>An $O(1)$ spatial lookup dictionary mapping grid coordinates to runtime grid nodes.</returns>
		TRuntime Hydrate(in TStorage storageData);
	}
	public class RuntimeDataCache {
		public Dictionary<Vec2Int, GridNode> GridData { get; }
		public Dictionary<ushort, Vec2Int[]> RegionData { get; }
		public RuntimeDataCache(Dictionary<Vec2Int, GridNode> gridData,
		Dictionary<ushort, Vec2Int[]> regionData) {
			this.GridData = gridData;
			this.RegionData = regionData;
		}
	}

	public interface IRegionDataCodex :
	ITileDataCodex<IDictionary<ushort, List<(Vec2Int position, TileTerrainData data)>>,
	 RegionStorageData,
	 RuntimeDataCache> { }
}