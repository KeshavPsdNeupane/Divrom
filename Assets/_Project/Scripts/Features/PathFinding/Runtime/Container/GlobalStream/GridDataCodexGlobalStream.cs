using System.Collections.Generic;
using Kope.Feature.PathFindingOld.Node;
using Kope.EntityIdentity;
using Project.Scripts.Features.PathFindingOld.GraphManager;

namespace Kope.Feature.PathFindingOld.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: BIT-PACKED GRAPH CODEX (ENCODER / DECODER)
     * ==============================================================================================
     * 
     * [1. THE DUAL-LIFECYCLE PATTERN]
     * This Codex manages bi-directional conversions between two distinct execution phases:
     * 
     *   A. BAKING TIME (Editor Pipeline - Encode):
     *      Flattens high-level graph dictionaries (`IDictionary<K, V>`) into monolithic master 
     *      primitive streams (SoA) and constructs bit-packed 64-bit slice range arrays (`long[]`). 
     *      This eliminates Unity serialized struct key overhead and compresses disk footprint.
     *      Connection edges store only the target BoundingBox — traversal/narrative-access per edge
     *      are never persisted, since they're a pure combination of each endpoint's own top-level
     *      `_trav`/`_narr` (see `MacroConnectionData.CreateConnection`).
     * 
     *   B. RUNTIME HYDRATION (Game Client - Decode):
     *      Reads primitive streams from disk and lazily re-hydrates runtime graph objects into $O(1)$ 
     *      lookup dictionaries without upfront scene load spikes. Per-edge capability/narrative-access
     *      are recomputed here via `CreateConnection`.
     * 
     * [2. STREAM SLICING EXECUTION FLOW]
     * 
     *   +-----------------------------------------------------------------------------------+
     *   | _globMicroSlices[i] (64-Bit Packed Long)                                           |
     *   | [ High 32-Bits: Starting Offset (uOffset) ] | [ Low 32-Bits: Slice Count (uCount) ] |
     *   +-----------------------------------------------------------------------------------+
     *                                      |
     *                                      v  Slice Bit-Unpack
     *              +-----------------------+-----------------------+
     *              |                                               |
     *              v                                               v
     *   _globMicroMPos[ uOffset ... uOffset + uCount - 1 ]   _globMicroFlags[ uOffset ... ]
     *   [ Contiguous Block of Micro Node Positions ]         [ Contiguous Flag Bytes ]
     * 
     * ==============================================================================================
     */

	/// <summary>
	/// Combined serialization and hydration engine for spatial pathfinding grids (Global Stream / Bit-Packed).
	/// Implements <see cref="IGridDataCodex{GridDataGlobalStream}"/> with zero-allocation struct dispatching.
	/// </summary>
	public readonly struct GridDataCodexGlobalStream : IGridDataCodex<GridDataGlobalStream> {

		#region Interface Implementations (Instance Dispatch)

		public GridDataGlobalStream Bake(
			IDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			IDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			IDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) => BakeStatic(microGridNodeDict, macroGridNodeDict, macroAdjacencyList, regionAnchorPoints);

		public GridDataGlobalStream Bake(in GridDataBakeInput input) =>
			BakeStatic(input.MicroGridNodeDict, input.MacroGridNodeDict, input.MacroAdjacencyList, input.RegionAnchorPoints);

		public GridDataRuntimeCache Hydrate(in GridDataGlobalStream gridData) => HydrateStatic(in gridData);

		#endregion

		#region Baking Pipeline (Encode)

		/// <summary>
		/// Flattens spatial graph dictionaries into a single bit-packed <see cref="GridDataGlobalStream"/> primitive payload.
		/// Connection edges are flattened down to their target BoundingBox only — the combined
		/// capability/narrative-access is never written, since it's derivable from each endpoint's
		/// own top-level <c>_trav</c>/<c>_narr</c> at hydration time.
		/// </summary>
		public static GridDataGlobalStream BakeStatic(
			IDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			IDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			IDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) {
			// ======================================================================================
			// STEP 1: SINGLE-PASS PRE-BUCKETING & COUNT COMPUTATION
			// Pre-sort micro-nodes under their parent macro BoundingBoxes to establish contiguous slices.
			// Calculate total global capacities upfront to execute single-allocation array instantiation.
			// ======================================================================================
			int macroCount = macroGridNodeDict != null ? macroGridNodeDict.Count : 0;
			var microNodesByMacro = new Dictionary<BoundingBox, List<MicroGridNode>>(macroCount);
			int totalMicroCount = 0;

			if (microGridNodeDict != null) {
				foreach (var microNode in microGridNodeDict.Values) {
					var bound = microNode.ParentBBox;
					if (!microNodesByMacro.TryGetValue(bound, out var list)) {
						list = new List<MicroGridNode>();
						microNodesByMacro[bound] = list;
					}
					list.Add(microNode);
					totalMicroCount++;
				}
			}

			// Sum total outgoing macro graph connection edges map-wide
			int totalConnCount = 0;
			if (macroAdjacencyList != null) {
				foreach (var connListEntry in macroAdjacencyList.Values) {
					totalConnCount += connListEntry?.Count ?? 0;
				}
			}

			// ======================================================================================
			// STEP 2: CONTIGUOUS PRIMITIVE BUFFER ALLOCATION
			// Instantiate pure primitive arrays matching exact total counts. Zero dynamically resizing lists.
			// ======================================================================================
			ulong[] keysHigh = new ulong[macroCount];
			ulong[] keysLow = new ulong[macroCount];
			TerrainType[] terrainTypes = new TerrainType[macroCount];
			MovementCapability[] allowedTraversals = new MovementCapability[macroCount];
			byte[] narrativeAccessFlags = new byte[macroCount];

			// Bit-packed slice descriptor streams (1 long per macro region: Offset << 32 | Count)
			long[] globMicroSlices = new long[macroCount];
			long[] globConnSlices = new long[macroCount];

			// Monolithic global Micro Node master buffers
			long[] globMicroMPos = new long[totalMicroCount];
			byte[] globMicroFlags = new byte[totalMicroCount];

			// Monolithic global Macro Connection master buffers — target BoundingBox only
			long[] globConnMinPos = new long[totalConnCount];
			long[] globConnMaxPos = new long[totalConnCount];

			// ======================================================================================
			// STEP 3: SEQUENTIAL STREAM WRITING & BIT-PACKING
			// Iterate over macro regions, appending child data sequentially into global master arrays,
			// and writing bit-packed (Offset << 32 | Count) descriptors into slice arrays.
			// ======================================================================================
			int currentMacroIdx = 0;
			int currentMicroOffset = 0; // Tracks write head position in master micro array
			int currentConnOffset = 0;  // Tracks write head position in master connection array

			if (macroGridNodeDict != null) {
				foreach (var kvp in macroGridNodeDict) {
					BoundingBox bbox = kvp.Key;
					MacroGridNode macroNode = kvp.Value;

					// 3a. Pack 128-bit BoundingBox key and write parallel macro metadata
					var (high, low) = SpatialBitPacker.PackBoundingBoxUnsigned(bbox);
					keysHigh[currentMacroIdx] = high;
					keysLow[currentMacroIdx] = low;
					terrainTypes[currentMacroIdx] = macroNode.TerrainType;
					allowedTraversals[currentMacroIdx] = macroNode.AllowedTraversal;
					narrativeAccessFlags[currentMacroIdx] = SpatialBitPacker.ConvertBoolToByte(macroNode.IsBlocked);

					// 3b. Pack Micro Nodes & Bit-Pack Slice Descriptor
					microNodesByMacro.TryGetValue(bbox, out List<MicroGridNode> microList);
					int microCount = microList?.Count ?? 0;

					// Bit-pack starting write head position (Offset) and slice length (Count) into 64-bit long
					globMicroSlices[currentMacroIdx] = GridDataGlobalStream.PackSlice(currentMicroOffset, microCount);

					// Copy micro node data into contiguous master primitive buffers
					for (int i = 0; i < microCount; i++) {
						var micro = microList[i];
						globMicroMPos[currentMicroOffset + i] = SpatialBitPacker.PackVec2(micro.Position);
						globMicroFlags[currentMicroOffset + i] = SpatialBitPacker.ConvertBoolToByte(micro.IsStaticObstacle);
					}
					currentMicroOffset += microCount; // Advance master micro stream write head

					// 3c. Pack Macro Connections & Bit-Pack Slice Descriptor (target box only)
					List<MacroConnectionData> connList = null;
					macroAdjacencyList?.TryGetValue(bbox, out connList);

					int connCount = connList?.Count ?? 0;

					// Bit-pack starting write head position (Offset) and slice length (Count) into 64-bit long
					globConnSlices[currentMacroIdx] = GridDataGlobalStream.PackSlice(currentConnOffset, connCount);

					// Copy connection target boxes into contiguous master primitive buffers
					for (int i = 0; i < connCount; i++) {
						var conn = connList[i];
						var (minP, maxP) = SpatialBitPacker.PackBoundingBox(conn.ToBound);

						globConnMinPos[currentConnOffset + i] = minP;
						globConnMaxPos[currentConnOffset + i] = maxP;
					}
					currentConnOffset += connCount; // Advance master connection stream write head

					currentMacroIdx++;
				}
			}

			// ======================================================================================
			// STEP 4: PAYLOAD CONSTRUCT ASSIGNMENT
			// Construct immutable baked struct payload.
			// ======================================================================================
			return new GridDataGlobalStream(
				regionAnchorPoints,
				keysHigh, keysLow, terrainTypes, allowedTraversals, narrativeAccessFlags,
				globMicroSlices, globConnSlices,
				globMicroMPos, globMicroFlags,
				globConnMinPos, globConnMaxPos
			);
		}

		#endregion

		#region Hydration Pipeline (Decode)

		/// <summary>
		/// Reads baked primitive arrays from disk, bit-unpacks 64-bit slice range descriptors, 
		/// and reconstructs high-level domain dictionaries into a unified <see cref="GridDataRuntimeCache"/>.
		/// Per-connection capability/narrative-access are rebuilt via <c>MacroConnectionData.CreateConnection</c>,
		/// combining each endpoint's own top-level <c>_trav</c>/<c>_narr</c>.
		/// </summary>
		public static GridDataRuntimeCache HydrateStatic(in GridDataGlobalStream gridData) {
			ulong[] keysHigh = gridData.KeysHigh;
			ulong[] keysLow = gridData.KeysLow;
			TerrainType[] terrainTypes = gridData.TerrainTypes;
			MovementCapability[] allowedTraversals = gridData.AllowedTraversal;
			byte[] narrativeAccessFlags = gridData.IsNarrativelyAccessible;

			long[] globMicroSlices = gridData.GlobMicroSlices;
			long[] globConnSlices = gridData.GlobConnSlices;

			long[] globMicroMPos = gridData.GlobMicroMPos;
			byte[] globMicroFlags = gridData.GlobMicroFlags;

			long[] globConnMinPos = gridData.GlobConnMinPos;
			long[] globConnMaxPos = gridData.GlobConnMaxPos;

			int macroCount = keysHigh != null ? keysHigh.Length : 0;
			int microCount = globMicroMPos != null ? globMicroMPos.Length : 0;

			// Pre-allocate exact dictionary capacities to eliminate internal rehashing/resizing overhead
			var macroDict = new Dictionary<BoundingBox, MacroGridNode>(macroCount);
			var adjacencyList = new Dictionary<BoundingBox, List<MacroConnectionData>>(macroCount);
			var microDict = new Dictionary<Vec2Int, MicroGridNode>(microCount);

			// Pre-pass: index each region's OWN traversal/narrative-access by BoundingBox. Required
			// because a connection's target region may be sequenced after the current index in the
			// arrays below — this gives O(1) lookup of either endpoint's data regardless of order.
			var ownRegionDataByBox = new Dictionary<BoundingBox, (MovementCapability trav, bool narr)>(macroCount);
			for (int i = 0; i < macroCount; i++) {
				BoundingBox box = SpatialBitPacker.UnpackBoundingBoxUnsigned(keysHigh[i], keysLow[i]);
				MovementCapability t = (allowedTraversals != null && i < allowedTraversals.Length) ? allowedTraversals[i] : default;
				bool n = (narrativeAccessFlags != null && i < narrativeAccessFlags.Length)
					&& SpatialBitPacker.ConvertByteToBool(narrativeAccessFlags[i]);
				ownRegionDataByBox[box] = (t, n);
			}

			// ======================================================================================
			// FAST SEQUENTIAL READ OVER STREAM BUFFERS
			// Iterate over all macro regions sequentially, using bitwise shifts to read contiguous slices.
			// ======================================================================================
			for (int i = 0; i < macroCount; i++) {
				// Reconstruct BoundingBox key from parallel 64-bit high/low key arrays
				BoundingBox bbox = SpatialBitPacker.UnpackBoundingBoxUnsigned(keysHigh[i], keysLow[i]);

				TerrainType tt = (terrainTypes != null && i < terrainTypes.Length) ? terrainTypes[i] : default;
				MovementCapability trav = (allowedTraversals != null && i < allowedTraversals.Length) ? allowedTraversals[i] : default;
				bool narr = (narrativeAccessFlags != null && i < narrativeAccessFlags.Length)
					&& SpatialBitPacker.ConvertByteToBool(narrativeAccessFlags[i]);


				// 2. Unpack 64-bit Micro Slice word -> (offset, count) tuple via bit shifting
				var (uOffset, uCount) = GridDataGlobalStream.UnpackSlice(globMicroSlices[i]);
				Vec2Int[] microPositions = new Vec2Int[uCount];

				// Read contiguous slice out of global master micro streams
				for (int j = 0; j < uCount; j++) {
					int idx = uOffset + j; // Index into global micro arrays
					Vec2Int pos = SpatialBitPacker.UnpackVec2(globMicroMPos[idx]);
					bool isObstacle = SpatialBitPacker.ConvertByteToBool(globMicroFlags[idx]);

					// Instantiate runtime MicroGridNode and insert into lookup map
					microDict[pos] = new MicroGridNode(pos, isObstacle, bbox);

					// Register micro node to its parent macro node
					// why prechecked? Because the micro nodes are already pre-bucketed by
					//  their parent macro node during the baking phase, so we can safely add 
					// them without checking for duplicates.
					microPositions[j] = pos;

				}
				// 1. Reconstruct MacroGridNode runtime object
				MacroGridNode macroNode = new(bbox, tt, trav, narr, microPositions);
				macroDict[bbox] = macroNode;


				// 3. Unpack 64-bit Connection Slice word -> (offset, count) tuple via bit shifting.
				// Only the target BoundingBox was persisted per edge — capability/narrative-access are
				// rebuilt from this region's own (trav, narr) — the "from" side — and the target's own
				// data looked up via the pre-pass above — the "to" side.
				var (cOffset, cCount) = GridDataGlobalStream.UnpackSlice(globConnSlices[i]);
				List<MacroConnectionData> connections = new(cCount);

				// Read contiguous slice out of global master connection streams
				for (int j = 0; j < cCount; j++) {
					int idx = cOffset + j; // Index into global connection arrays
					BoundingBox targetBound = SpatialBitPacker.UnpackBoundingBox(globConnMinPos[idx], globConnMaxPos[idx]);

					MovementCapability toCapability = default;
					bool toNarrativelyAccessible = false;
					if (ownRegionDataByBox.TryGetValue(targetBound, out var toData)) {
						toCapability = toData.trav;
						toNarrativelyAccessible = toData.narr;
					}

					connections.Add(MacroConnectionData.CreateConnection(
						targetBound, toCapability, trav, toNarrativelyAccessible, narr));
				}
				adjacencyList[bbox] = connections;
			}

			return new GridDataRuntimeCache(microDict, macroDict, adjacencyList);
		}

		#endregion
	}
}