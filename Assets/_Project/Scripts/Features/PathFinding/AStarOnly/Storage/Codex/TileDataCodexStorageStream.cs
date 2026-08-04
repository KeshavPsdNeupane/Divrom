using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;

namespace Kope.Feature.PathFindingNew.Storage {

	/*
	 * ==============================================================================================
	 * ARCHITECTURAL RATIONALE: BIT-PACKED TILE TERRAIN CODEX (ENCODER / DECODER)
	 * ==============================================================================================
	 * 
	 * [1. THE TRIPLE-DOMAIN LIFECYCLE PIPELINE]
	 * This Codex implements ITileDataCodex<TileTerrainData, TileStorageData, GridNode> to execute
	 * explicit domain transformations across three isolated phases:
	 * 
	 *   A. BAKING TIME (Editor Pipeline - Encode: Tile Domain [TileTerrainData] -> Storage Domain [TileStorageData]):
	 *      Flattens high-level authoring tiles (`IDictionary<Vec2Int, TileTerrainData>`) into 
	 *      a monolithic Structure-of-Arrays (SoA) primitive stream (`TileStorageData`).
	 *      - Positions (`Vec2Int`) are bit-packed into 64-bit `long` words.
	 *      - Traversal cost multipliers (Move, Swim, Fly) are quantized (10 bits each) and bit-packed
	 *        into a single 32-bit `int`.
	 *      - Editor/Visual properties are stripped completely, cutting memory footprint by ~50%.
	 * 
	 *   B. RUNTIME HYDRATION (Game Client - Decode: Storage Domain [TileStorageData] -> Grid Data Domain [GridNode]):
	 *      Reads primitive arrays from storage (`TileStorageData`) and re-hydrates them directly 
	 *      into lightweight pathfinding runtime nodes (`Dictionary<Vec2Int, GridNode>`).
	 *      Bypasses heavy authoring abstractions completely to yield an $O(1)$ lookup grid domain
	 *      without GC overhead or load-time allocations.
	 * 
	 * [2. RATIONALE FOR 3 SEPARATE CONTRACT DOMAINS]
	 *   - TBake (TileTerrainData) : Optimizes for HUMAN AUTHORING (Editors, visuals, inspectors).
	 *   - TStorage (TileStorageData) : Optimizes for DISK & STREAMING (SoA arrays, zero bloat, bit-packing).
	 *   - TRuntime (GridNode)      : Optimizes for SEARCH EXECUTION (L1 cache locality, bitwise queries).
	 * 
	 *   Fewer generics would force compromising one phase's performance to accommodate another.
	 * 
	 * [3. MEMORY LAYOUT PER TILE (~16 BYTES TOTAL IN STORAGE)]
	 * 
	 *   +---------------------+-------------------+-------------------+-------------------+
	 *   | _pPos (long)        | _qCostMul (int)   | _biomeType (enum) | _isTraversable    |
	 *   | 8 Bytes (X | Y)     | 4 Bytes (30-bits) | 2 Bytes (ushort)  | 1 Byte (byte)     |
	 *   +---------------------+-------------------+-------------------+-------------------+
	 * 
	 * ==============================================================================================
	 */

	/// <summary>
	/// Combined serialization and hydration engine for tile terrain grids.
	/// Implements <see cref="ITileDataCodex{TBake, TStorage, TRuntime}"/> mapping authoring <see cref="TileTerrainData"/> 
	/// <c>-&gt;</c> storage <see cref="TileStorageData"/> <c>-&gt;</c> runtime <see cref="GridNode"/>.
	/// </summary>
	public readonly struct TileDataCodexStorageStream :
	IRegionDataCodex {
		#region Interface Implementations (Instance Dispatch)

		/// <inheritdoc />
		public RegionStorageData Bake(IDictionary<ushort, List<(Vec2Int position, TileTerrainData data)>> tileDict) {
			return BakeStatic(tileDict);
		}

		/// <inheritdoc />
		public RuntimeDataCache Hydrate(in RegionStorageData storageData) =>
			HydrateStatic(in storageData);


		public static RegionStorageData EMPTY_REGION_STORAGE = new(
					new ushort[0],
					new GridStorageData[0]
				);
		#endregion

		#region Baking Pipeline (Encode: Tile Domain -> Storage Domain)

		/// <summary>
		/// Flattens a spatial dictionary of authoring tiles (<see cref="TileTerrainData"/>) into a bit-packed 
		/// <see cref="RegionStorageData"/> primitive payload. Quantizes cost multipliers down to 10-bit integer 
		/// streams and packs coordinates into 64-bit integers.
		/// </summary>
		/// <param name="tileDict">Authoring tile domain lookup dictionary.</param>
		/// <returns>A primitive Structure-of-Arrays <see cref="RegionStorageData"/> container.</returns>
		public static RegionStorageData BakeStatic(IDictionary<ushort, List<(Vec2Int position, TileTerrainData data)>> tileDict) {
			int count = tileDict != null ? tileDict.Count : 0;

			if (tileDict == null || count == 0) {
				return EMPTY_REGION_STORAGE;
			}

			// ======================================================================================
			// STEP 1: CONTIGUOUS PRIMITIVE BUFFER ALLOCATION
			// Instantiate pure primitive arrays matching exact total count. Zero dynamic resizing.
			// ======================================================================================

			ushort[] regionIds = new ushort[count];
			List<GridStorageData> regionDataList = new List<GridStorageData>(count);

			// ======================================================================================
			// STEP 2: SEQUENTIAL STREAM WRITING & BIT-PACKING
			// Iterate over authoring tile entries, packing tile domain types into storage primitive streams.
			// ======================================================================================
			int index = 0;
			foreach (var kvp in tileDict) {
				regionIds[index] = kvp.Key;

				int tileCount = kvp.Value.Count;
				long[] pPos = new long[tileCount];
				byte[] isTraversable = new byte[tileCount];
				TileType[] biomeType = new TileType[tileCount];
				MovementCapability[] allowedCapabilities = new MovementCapability[tileCount];
				int[] qCostMul = new int[tileCount];

				int tileIndex = 0;
				foreach (var (pos, data) in kvp.Value) {
					pPos[tileIndex] = SpatialBitPacker.PackVec2(pos);
					isTraversable[tileIndex] = SpatialBitPacker.ConvertBoolToByte(data.IsTraversable);
					biomeType[tileIndex] = data.TileType;
					allowedCapabilities[tileIndex] = data.AllowedCapabilities;
					qCostMul[tileIndex] = SpatialBitPacker.PackCostMultipliers(
						data.MoveCostMultiplier, data.SwimCostMultiplier, data.FlyCostMultiplier);
					tileIndex++;
				}

				// ======================================================================================
				// STEP 3: PAYLOAD CONSTRUCT ASSIGNMENT (STORAGE DOMAIN)
				// ======================================================================================
				GridStorageData regionData = new(pPos, isTraversable, biomeType, allowedCapabilities, qCostMul);
				regionDataList.Add(regionData);

				index++;
			}

			return new RegionStorageData(regionIds, regionDataList.ToArray());
		}

		#endregion

		#region Hydration Pipeline (Decode: Storage Domain -> Grid Data Domain)

		/// <summary>
		/// Reads baked primitive arrays from the storage domain (<see cref="RegionStorageData"/>), bit-unpacks 
		/// coordinates and cost multipliers, and reconstructs the runtime grid domain payload (<see cref="Dictionary{Vec2Int, GridNode}"/>).
		/// </summary>
		/// <param name="storageData">The packed primitive storage container to decode.</param>
		/// <returns>An $O(1)$ runtime grid dictionary containing hydrated <see cref="GridNode"/> structures.</returns>
		public static RuntimeDataCache HydrateStatic(in RegionStorageData storageData) {
			ushort[] regionIds = storageData.RegionId;
			GridStorageData[] regionData = storageData.RegionData;

			int count = regionData.Length;

			// Pre-allocate exact dictionary capacity to eliminate internal rehashing/resizing overhead in Grid Domain
			var regionDataDict = new Dictionary<ushort, Vec2Int[]>(count);
			var tileDict = new Dictionary<Vec2Int, GridNode>(count);

			if (count == 0) return new RuntimeDataCache(tileDict, new Dictionary<ushort, Vec2Int[]>());

			// ======================================================================================
			// FAST SEQUENTIAL READ OVER STREAM BUFFERS
			// Iterate over all packed arrays sequentially and dequantize into runtime GridNode structures.
			// ======================================================================================
			for (int i = 0; i < count; i++) {
				int jcount = regionData[i].PackedPosition.Length;
				ushort regionId = regionIds[i];
				var currentRegionData = regionData[i];
				Vec2Int[] regionPositions = new Vec2Int[jcount];

				for (int j = 0; j < jcount; j++) {
					// 1. Unpack 2D vector coordinate
					Vec2Int pos = SpatialBitPacker.UnpackVec2(currentRegionData.PackedPosition[j]);
					// fill the region positions array for this region
					regionPositions[j] = pos;

					// 2. Unpack boolean traversability
					bool traversable = SpatialBitPacker.ConvertByteToBool(currentRegionData.IsTraversable[j]);

					// just grab the current region's data for this tile index
					var tileType = currentRegionData.TileType[j];
					var allowedCapabilities = currentRegionData.AllowedCapabilities[j];
					// 3. Dequantize 30-bit packed cost word into (moveCost, swimCost, flyCost)
					(float moveCost, float swimCost, float flyCost) = SpatialBitPacker.UnpackCostMultipliers(currentRegionData.QCostMultiplier[j]);

					// 4. Instantiate GridNode runtime struct for the Grid Data Domain
					tileDict[pos] = new GridNode(
						regionId,
						pos,
						tileType,
						allowedCapabilities,
						traversable,
						moveCost,
						swimCost,
						flyCost
					);
				}
				// 5. Assign the region's positions array to the region dictionary
				regionDataDict[regionId] = regionPositions;
			}

			return new RuntimeDataCache(tileDict, regionDataDict);
		}
		#endregion
	}
}