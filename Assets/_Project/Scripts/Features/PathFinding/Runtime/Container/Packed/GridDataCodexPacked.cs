using System.Collections.Generic;
using Project.Scripts.Features.PathFinding.GraphManager;
using Kope.Feature.PathFinding.Node;
using Kope.EntityIdentity;

namespace Kope.Feature.PathFinding.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: DUAL-PHASE DATA STORAGE & LAZY RE-HYDRATION (PACKED)
     * ==============================================================================================
     * 
     * [1. THE CORE PROBLEM]
     * Live pathfinding requires heavy runtime objects (`MicroGridNode`, `MacroGridNode`, `MacroConnectionData`)
     * organized in Dictionaries for O(1) lookups. However, directly serializing thousands of C# object 
     * references and Dictionaries into ScriptableObjects creates massive YAML file bloat (duplicated field 
     * tags) and slow scene load times.
     * 
     * [2. THE ARCHITECTURAL SOLUTION]
     * This Codex separates DISK FORMAT from RUNTIME FORMAT across two execution phases:
     * 
     *   A. BAKING TIME (Editor Pipeline - Encode):
     *      During editor baking (`Bake`), live graph nodes are bucketed by macro region in O(N) time 
     *      and flattened into compact, index-aligned primitive arrays (long[], byte[]). BoundingBox 
     *      keys are split into 128-bit high/low primitive `ulong` pairs (`keysHigh`, `keysLow`), and
     *      region metadata (`terrainTypes`, `allowedTraversals`, `isNarrativelyAccessible`) is aligned
     *      in parallel columns on `PathFindingGridData` — ONCE per region, since it's each region's own
     *      data. Outgoing connections store only the target region's BoundingBox; the per-edge combined
     *      capability/narrative-access is never persisted, since it's fully derivable from the two
     *      endpoints' own top-level values.
     * 
     *   B. RUNTIME HYDRATION (Game Client - Decode):
     *      On game boot or first property access, `Hydrate` lazily reconstructs primitive arrays and 
     *      bit-packed tuple pairs into fully featured runtime lookup Dictionaries without upfront 
     *      scene loading hitches. Per-edge `MacroConnectionData` values are recomputed here via
     *      `MacroConnectionData.CreateConnection`, combining each endpoint's own top-level data.
     * 
     * ==============================================================================================
     * DATA TRANSFORM & RE-HYDRATION GRAPH (PACKED PRIMITIVE STREAMING)
     * ==============================================================================================
     * 
     * [BAKE TIME: BAKE()]
     *  Live Runtime Objects ──► O(N) Macro Bucketing ──► Pack Keys (ulong high, low) & Arrays ──► YAML Asset (Disk)
     * 
     * [RUNTIME: LAZY RE-HYDRATION: HYDRATE()]
     *  YAML Asset (Disk)
     *   │
     *   ├── _gridData (PathFindingGridData)
     *   │     ├── _anchors:  List<Vec2Int>
     *   │     ├── _keysHigh: ulong[]  (Min.X / Min.Y Packed 64-bit Hex Words)
     *   │     ├── _keysLow:  ulong[]  (Max.X / Max.Y Packed 64-bit Hex Words)
     *   │     ├── _tt:       TerrainType[]
     *   │     ├── _trav:     MovementCapability[]   (each region's OWN capability)
     *   │     ├── _narr:     byte[]                 (each region's OWN narrative access)
     *   │     └── _values:   MacroSaveData[]
     *   │           ├── Parallel Column Arrays: _mPos[] (Vec2Int), _flags[] (byte)
     *   │           └── Connection Columns: _targetRegions (List<BoundingBox>) ONLY
     *   │
     *   ▼ (Triggered on 1st Property Getter Access)
     *  Hydrate()  [Single-Pass Pre-Allocated Reconstruction]
     *   │
     *   ├──► MicroGridNodeDict  : Dictionary<Vec2Int, MicroGridNode>        (O(1) Micro Lookup)
     *   ├──► MacroGridNodeDict  : Dictionary<BoundingBox, MacroGridNode>     (O(1) Macro Lookup)
     *   └──► MacroAdjacencyList : Dictionary<BoundingBox, List<Connection>> (edges rebuilt via
     *          CreateConnection, combining each endpoint's own _trav/_narr looked up by BoundingBox)
     * ==============================================================================================
     */

	/// <summary>
	/// Combined encoding and re-hydration engine for packed spatial pathfinding grid assets.
	/// Implements <see cref="IGridDataCodex{GridDataPacked}"/> with zero-allocation struct dispatching.
	/// </summary>
	public readonly struct GridDataCodexPacked : IGridDataCodex<GridDataPacked> {

		#region Interface Implementations (Instance Dispatch)

		public GridDataPacked Bake(
			IDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			IDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			IDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) => BakeStatic(microGridNodeDict, macroGridNodeDict, macroAdjacencyList, regionAnchorPoints);

		public GridDataPacked Bake(in GridDataBakeInput input) =>
			BakeStatic(input.MicroGridNodeDict, input.MacroGridNodeDict, input.MacroAdjacencyList, input.RegionAnchorPoints);

		public GridDataRuntimeCache Hydrate(in GridDataPacked gridData) => HydrateStatic(in gridData);

		#endregion

		#region Baking Pipeline (Encode)

		/// <summary>
		/// Bakes live runtime grid graph objects into compressed, parallel-array serialized struct streams.
		/// Bit-packs BoundingBox keys into <c>(ulong high, ulong low)</c> primitive pairs and stores 
		/// region data in parallel value structs. Executes in single-pass O(N) time by pre-bucketing micro nodes.
		/// Per-connection capability/narrative-access are NOT stored — only the target BoundingBox is —
		/// since they're a pure combination of each endpoint's own <c>_trav</c>/<c>_narr</c>, recomputed at hydrate time.
		/// </summary>
		public static GridDataPacked BakeStatic(
			IDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			IDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			IDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) {
			// STEP 1: Pre-group micro-nodes by macro region bounding box upfront (O(N) single-pass)
			int macroCount = macroGridNodeDict != null ? macroGridNodeDict.Count : 0;
			var microNodesByMacro = new Dictionary<BoundingBox, List<MicroGridNode>>(macroCount);

			if (microGridNodeDict != null) {
				foreach (var microNode in microGridNodeDict.Values) {
					var bound = microNode.ParentBBox;
					if (!microNodesByMacro.TryGetValue(bound, out var list)) {
						list = new List<MicroGridNode>();
						microNodesByMacro[bound] = list;
					}
					list.Add(microNode);
				}
			}

			// STEP 2: Allocate top-level parallel key & metadata streams + values array
			ulong[] keysHigh = new ulong[macroCount];
			ulong[] keysLow = new ulong[macroCount];
			TerrainType[] terrainTypes = new TerrainType[macroCount];
			MovementCapability[] allowedTraversals = new MovementCapability[macroCount];
			byte[] narrativeAccessFlags = new byte[macroCount];
			MacroSaveDataPacked[] values = new MacroSaveDataPacked[macroCount];

			int index = 0;
			if (macroGridNodeDict != null) {
				foreach (var kvp in macroGridNodeDict) {
					BoundingBox bbox = kvp.Key;
					MacroGridNode macroNode = kvp.Value;

					// Flatten micro-node positions & static obstacle flags for this macro region
					microNodesByMacro.TryGetValue(bbox, out var microNodesInRegion);
					int microCount = microNodesInRegion?.Count ?? 0;

					List<Vec2Int> microRegionPositions = new(microCount);
					byte[] macroRegionStaticObstacleFlags = new byte[microCount];

					if (microNodesInRegion != null) {
						for (int i = 0; i < microCount; i++) {
							var micro = microNodesInRegion[i];
							microRegionPositions.Add(micro.Position);
							macroRegionStaticObstacleFlags[i] = SpatialBitPacker.ConvertBoolToByte(micro.IsStaticObstacle);
						}
					}

					// Flatten outgoing connections down to just their target BoundingBox — the combined
					// capability/narrative-access per edge is derived at hydrate time, not stored.
					List<MacroConnectionData> neighboringConnections = null;
					macroAdjacencyList?.TryGetValue(bbox, out neighboringConnections);
					int connCount = neighboringConnections?.Count ?? 0;

					List<BoundingBox> targetRegions = new(connCount);
					if (neighboringConnections != null) {
						for (int i = 0; i < connCount; i++) {
							targetRegions.Add(neighboringConnections[i].ToBound);
						}
					}

					// Bit-pack the 128-bit BoundingBox key into high/low ulong primitives
					var (high, low) = SpatialBitPacker.PackBoundingBoxUnsigned(bbox);

					keysHigh[index] = high;
					keysLow[index] = low;
					terrainTypes[index] = macroNode.TerrainType;
					allowedTraversals[index] = macroNode.AllowedTraversal;
					narrativeAccessFlags[index] = SpatialBitPacker.ConvertBoolToByte(macroNode.IsBlocked);

					values[index] = new MacroSaveDataPacked(
						microRegionPositions,
						macroRegionStaticObstacleFlags,
						new MacroConnectionSaveDataPacked(targetRegions)
					);

					index++;
				}
			}

			return new GridDataPacked(
				regionAnchorPoints, keysHigh, keysLow, terrainTypes, allowedTraversals, narrativeAccessFlags, values);
		}

		#endregion

		#region Hydration Pipeline (Decode)

		/// <summary>
		/// Reconstructs full C# runtime domain instances (MacroGridNode, MicroGridNode, MacroConnectionData)
		/// directly from high/low key arrays, parallel metadata streams, and primitive value structs into a unified <see cref="GridDataRuntimeCache"/>.
		/// Per-connection capability/narrative-access are rebuilt via <c>MacroConnectionData.CreateConnection</c>,
		/// combining each endpoint's own top-level <c>_trav</c>/<c>_narr</c>.
		/// </summary>
		public static GridDataRuntimeCache HydrateStatic(in GridDataPacked gridData) {
			ulong[] keysHigh = gridData.KeysHigh;
			ulong[] keysLow = gridData.KeysLow;
			TerrainType[] terrainTypes = gridData.TerrainTypes;
			MovementCapability[] allowedTraversals = gridData.AllowedTraversal;
			byte[] narrativeAccessFlags = gridData.IsNarrativelyAccessible;
			MacroSaveDataPacked[] values = gridData.Values;

			int macroCount = values != null ? values.Length : 0;

			// Pre-allocate dictionaries with exact capacities to eliminate internal array resizing/re-hashing
			var macroGridNodeDict = new Dictionary<BoundingBox, MacroGridNode>(macroCount);
			var macroAdjacencyList = new Dictionary<BoundingBox, List<MacroConnectionData>>(macroCount);

			// Pre-calculate total micro-nodes across all macro regions for a single allocation
			int totalMicroNodes = 0;
			if (values != null) {
				for (int i = 0; i < macroCount; i++) {
					if (values[i].MacroRegionAnchorPoints != null) {
						totalMicroNodes += values[i].MacroRegionAnchorPoints.Count;
					}
				}
			}
			var microGridNodeDict = new Dictionary<Vec2Int, MicroGridNode>(totalMicroNodes);

			// Pre-pass: index each region's OWN traversal/narrative-access by BoundingBox. Needed because
			// a connection's target region may not have been visited yet in the main loop below — this
			// gives O(1) lookup of either endpoint's data regardless of iteration order.
			var ownRegionDataByBox = new Dictionary<BoundingBox, (MovementCapability trav, bool narr)>(macroCount);
			for (int i = 0; i < macroCount; i++) {
				BoundingBox box = SpatialBitPacker.UnpackBoundingBoxUnsigned(keysHigh[i], keysLow[i]);
				MovementCapability t = (allowedTraversals != null && i < allowedTraversals.Length) ? allowedTraversals[i] : default;
				bool n = (narrativeAccessFlags != null && i < narrativeAccessFlags.Length)
					&& SpatialBitPacker.ConvertByteToBool(narrativeAccessFlags[i]);
				ownRegionDataByBox[box] = (t, n);
			}



			// Rehydrate domain objects directly from parallel primitive key/metadata/value streams
			for (int i = 0; i < macroCount; i++) {
				ulong high = keysHigh[i];
				ulong low = keysLow[i];
				BoundingBox bbox = SpatialBitPacker.UnpackBoundingBoxUnsigned(high, low);
				MacroSaveDataPacked macroData = values[i];

				TerrainType tt = (terrainTypes != null && i < terrainTypes.Length) ? terrainTypes[i] : default;
				MovementCapability trav = (allowedTraversals != null && i < allowedTraversals.Length) ? allowedTraversals[i] : default;
				bool narr = narrativeAccessFlags != null && i < narrativeAccessFlags.Length
					&& SpatialBitPacker.ConvertByteToBool(narrativeAccessFlags[i]);



				// 2. Reconstruct MicroGridNodes directly from synchronized parallel streams
				var positions = macroData.MacroRegionAnchorPoints;
				var obstacleFlags = macroData.MacroRegionStaticObstacleFlags;

				int microNodeCount = positions != null ? positions.Count : 0;

				Vec2Int[] microNodesInRegion = new Vec2Int[microNodeCount];

				for (int j = 0; j < microNodeCount; j++) {
					Vec2Int pos = positions[j];
					bool isObstacle = SpatialBitPacker.ConvertByteToBool(obstacleFlags[j]);

					// Instantiate MicroGridNode with direct parent reference to the newly created MacroGridNode
					microGridNodeDict[pos] = new MicroGridNode(pos, isObstacle, bbox);
					// Register micro node to its parent macro node
					// why prechecked? Because the micro nodes are already pre-bucketed by
					// their parent macro node during the baking phase, so we can safely add 
					// them without checking for duplicates.
					microNodesInRegion[j] = pos;

				}
				MacroGridNode macroNode = new(bbox, tt, trav, narr, microNodesInRegion);
				macroGridNodeDict[bbox] = macroNode;
				// 3. Reconstruct MacroConnectionData graph edges. Only the target BoundingBox was persisted;
				// the combined capability/narrative-access are rebuilt here from this region's own data
				// (trav/narr, the "from" side) and the target's own data looked up above (the "to" side).
				var connData = macroData.NeighboringRegionBoundingBoxes;
				var targets = connData.TargetRegions;

				int connectionCount = targets != null ? targets.Count : 0;
				List<MacroConnectionData> connections = new(connectionCount);
				for (int j = 0; j < connectionCount; j++) {
					BoundingBox toBox = targets[j];

					MovementCapability toCapability = default;
					bool toNarrativelyAccessible = false;
					if (ownRegionDataByBox.TryGetValue(toBox, out var toData)) {
						toCapability = toData.trav;
						toNarrativelyAccessible = toData.narr;
					}

					connections.Add(MacroConnectionData.CreateConnection(
						toBox, toCapability, trav, toNarrativelyAccessible, narr));
				}
				macroAdjacencyList[bbox] = connections;
			}

			return new GridDataRuntimeCache(microGridNodeDict, macroGridNodeDict, macroAdjacencyList);
		}

		#endregion
	}
}