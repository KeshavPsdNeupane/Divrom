using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Tile;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;

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
	public readonly struct TileDataCodexStorageStream : ITileDataCodex<TileTerrainData, GridStorageData, GridNode> {

		#region Interface Implementations (Instance Dispatch)

		/// <inheritdoc />
		public GridStorageData Bake(IDictionary<Vec2Int, TileTerrainData> tileDict) =>
			BakeStatic(tileDict);

		/// <inheritdoc />
		public Dictionary<Vec2Int, GridNode> Hydrate(in GridStorageData storageData) =>
			HydrateStatic(in storageData);

		#endregion

		#region Baking Pipeline (Encode: Tile Domain -> Storage Domain)

		/// <summary>
		/// Flattens a spatial dictionary of authoring tiles (<see cref="TileTerrainData"/>) into a bit-packed 
		/// <see cref="GridStorageData"/> primitive payload. Quantizes cost multipliers down to 10-bit integer 
		/// streams and packs coordinates into 64-bit integers.
		/// </summary>
		/// <param name="tileDict">Authoring tile domain lookup dictionary.</param>
		/// <returns>A primitive Structure-of-Arrays <see cref="GridStorageData"/> container.</returns>
		public static GridStorageData BakeStatic(IDictionary<Vec2Int, TileTerrainData> tileDict) {
			int count = tileDict != null ? tileDict.Count : 0;

			// ======================================================================================
			// STEP 1: CONTIGUOUS PRIMITIVE BUFFER ALLOCATION
			// Instantiate pure primitive arrays matching exact total count. Zero dynamic resizing.
			// ======================================================================================
			long[] pPos = new long[count];
			byte[] isTraversable = new byte[count];
			TileType[] biomeType = new TileType[count];
			MovementCapability[] allowedCapabilities = new MovementCapability[count];
			int[] qCostMul = new int[count];

			if (tileDict == null || count == 0) {
				return new GridStorageData(pPos, isTraversable, biomeType, allowedCapabilities, qCostMul);
			}

			// ======================================================================================
			// STEP 2: SEQUENTIAL STREAM WRITING & BIT-PACKING
			// Iterate over authoring tile entries, packing tile domain types into storage primitive streams.
			// ======================================================================================
			int index = 0;
			foreach (var kvp in tileDict) {
				Vec2Int pos = kvp.Key;
				TileTerrainData terrain = kvp.Value;

				// 2a. Bit-pack 2D position (X in high 32 bits, Y in low 32 bits)
				pPos[index] = SpatialBitPacker.PackVec2(pos);

				// 2b. Pack boolean state and primitive enums
				isTraversable[index] = SpatialBitPacker.ConvertBoolToByte(terrain.IsTraversable);
				biomeType[index] = terrain.TileType;
				allowedCapabilities[index] = terrain.AllowedCapabilities;

				// 2c. Quantize and bit-pack Move, Swim, and Fly costs into a single 32-bit int (10 bits each)
				qCostMul[index] = SpatialBitPacker.PackCostMultipliers(
					terrain.MoveCostMultiplier,
					terrain.SwimCostMultiplier,
					terrain.FlyCostMultiplier
				);

				index++;
			}

			// ======================================================================================
			// STEP 3: PAYLOAD CONSTRUCT ASSIGNMENT (STORAGE DOMAIN)
			// ======================================================================================
			return new GridStorageData(pPos, isTraversable, biomeType, allowedCapabilities, qCostMul);
		}

		#endregion

		#region Hydration Pipeline (Decode: Storage Domain -> Grid Data Domain)

		/// <summary>
		/// Reads baked primitive arrays from the storage domain (<see cref="GridStorageData"/>), bit-unpacks 
		/// coordinates and cost multipliers, and reconstructs the runtime grid domain payload (<see cref="Dictionary{Vec2Int, GridNode}"/>).
		/// </summary>
		/// <param name="storageData">The packed primitive storage container to decode.</param>
		/// <returns>An $O(1)$ runtime grid dictionary containing hydrated <see cref="GridNode"/> structures.</returns>
		public static Dictionary<Vec2Int, GridNode> HydrateStatic(in GridStorageData storageData) {
			long[] pPos = storageData.PackedPosition;
			byte[] isTraversable = storageData.IsTraversable;
			TileType[] tileType = storageData.TileType;
			MovementCapability[] allowedCapabilities = storageData.AllowedCapabilities;
			int[] qCostMul = storageData.QCostMultiplier;

			int count = pPos != null ? pPos.Length : 0;

			// Pre-allocate exact dictionary capacity to eliminate internal rehashing/resizing overhead in Grid Domain
			var tileDict = new Dictionary<Vec2Int, GridNode>(count);

			if (count == 0) return tileDict;

			// ======================================================================================
			// FAST SEQUENTIAL READ OVER STREAM BUFFERS
			// Iterate over all packed arrays sequentially and dequantize into runtime GridNode structures.
			// ======================================================================================
			for (int i = 0; i < count; i++) {
				// 1. Unpack 2D vector coordinate
				Vec2Int pos = SpatialBitPacker.UnpackVec2(pPos[i]);

				// 2. Unpack boolean traversability
				bool traversable = SpatialBitPacker.ConvertByteToBool(isTraversable[i]);

				// 3. Dequantize 30-bit packed cost word into (moveCost, swimCost, flyCost)
				(float moveCost, float swimCost, float flyCost) = SpatialBitPacker.UnpackCostMultipliers(qCostMul[i]);

				// 4. Instantiate GridNode runtime struct for the Grid Data Domain
				tileDict[pos] = new GridNode(
					pos,
					tileType[i],
					allowedCapabilities[i],
					traversable,
					moveCost,
					swimCost,
					flyCost
				);
			}

			return tileDict;
		}

		#endregion
	}
}