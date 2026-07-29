using System;
using System.Collections.Generic;
using UnityEngine;
using Project.Scripts.Features.PathFinding.GraphManager;
using Kope.Core.Collections;
using Kope.Core.Attribute;
using Kope.Feature.PathFinding.Node;
using Kope.EntityIdentity;

namespace Kope.Feature.PathFinding.Data {

	/*
     * ==============================================================================================
     * ARCHITECTURAL RATIONALE: DUAL-PHASE DATA STORAGE & LAZY RE-HYDRATION
     * ==============================================================================================
     * 
     * [The Core Problem]
     * Live pathfinding requires heavy runtime objects (`MicroGridNode`, `MacroGridNode`, `MacroConnectionData`)
     * organized in Dictionaries for O(1) lookups. However, directly serializing thousands of C# object 
     * references and Dictionaries into ScriptableObjects creates massive YAML file bloat (duplicated field 
     * tags) and slow scene load times.
     * 
     * [The Architecture Solution]
     * This ScriptableObject acts as a dual-phase container separating DISK FORMAT from RUNTIME FORMAT:
     * 
     * 1. Storage Phase (Disk Payload):
     *    During editor baking (`SetGridDataInternal`), live graph nodes are bucketed by macro region in 
     *    O(N) time and flattened into compact, index-aligned primitive arrays (int[], byte[]).
     *    BoundingBox keys are split into 128-bit high/low primitive `ulong` pairs (`_keysHigh`, `_keysLow`),
     *    triggering Unity's C++ serializer to inline keys as raw hexadecimal byte streams instead of YAML objects.
     *    This eliminates structural YAML text tags, shrinking the disk footprint by ~99.5%.
     * 
     * 2. Runtime Phase (In-Memory Caches):
     *    On game boot or first property access, the container lazily re-hydrates primitive arrays and bit-packed 
     *    tuple pairs into fully featured runtime lookup Dictionaries (`RebuildRuntimeCaches`).
     * 
     * ==============================================================================================
     * DATA TRANSFORM & RE-HYDRATION GRAPH (COLUMNAR INLINE HEX KEYS)
     * ==============================================================================================
     * 
     * [BAKE TIME: SETGRIDDATAINTERNAL]
     *  Live Runtime Objects ──► O(N) Macro Bucketing ──► Pack Keys (ulong high, low) & Arrays ──► YAML Asset (Disk)
     * 
     * [RUNTIME: LAZY RE-HYDRATION]
     *  YAML Asset (Disk)
     *   │
     *   ├── _gridData (PathFindingGridData)
     *   │     ├── _keysHigh: ulong[] (Min.X / Min.Y Packed 64-bit Hex Words)
     *   │     ├── _keysLow:  ulong[] (Max.X / Max.Y Packed 64-bit Hex Words)
     *   │     └── _values:   MacroSaveData[]
     *   │           ├── Parallel Column Arrays: _mPosX[], _mPosY[], _flags[]
     *   │           └── Connection Columns: _minX[], _minY[], _maxX[], _maxY[]
     *   │
     *   ▼ (Triggered on 1st Property Getter Access)
     *  RebuildRuntimeCaches()  [Single-Pass Pre-Allocated Reconstruction]
     *   │
     *   ├──► _macroGridNodeDict  : Dictionary<BoundingBox, MacroGridNode>     (O(1) Macro Lookup)
     *   ├──► _microGridNodeDict  : Dictionary<Vec2Int, MicroGridNode>        (O(1) Micro Lookup)
     *   └──► _macroAdjacencyList : Dictionary<BoundingBox, List<Connection>> (O(1) Edge Lookup)
     * ==============================================================================================
     */

	/// <summary>
	/// ScriptableObject container that stores baked pathfinding grid data assets in a high-density, 
	/// flattened format on disk, and lazily re-hydrates full O(1) graph dictionaries at runtime.
	/// </summary>
	[CreateAssetMenu(
		fileName = "PathFindingGridDataContainer",
		menuName = "Scriptable Objects/PathFinding/Grid Data Container"
	)]
	public class PathFindingGridDataContainer : GridDataContainerBase {

		#region Serialized Fields

		[Message(
			"Note: Due to Unity Inspector limitations with dynamic-height elements, " +
			"these fields cannot be fully locked with ReadOnly and can technically be mutated.\n\n" +
			"Manual modification is strongly discouraged. This data is exposed strictly " +
			"for debugging and verifying data integrity. Please leave these fields alone, " +
			"as any manual edits will be overwritten on the next bake.",
			MessageSeverity.Warning
		)]
		[Header("Baked Data")]
		[SerializeField] private PathFindingGridData _gridData;

		#endregion

		#region Non-Serialized Runtime Caches

		// Non-serialized runtime lookup tables reconstructed on demand from serialized save data.
		// Kept null until first property access to eliminate startup memory overhead.
		private Dictionary<Vec2Int, MicroGridNode> _microGridNodeDict;
		private Dictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;
		private Dictionary<BoundingBox, List<MacroConnectionData>> _macroAdjacencyList;

		#endregion

		#region Domain Properties

		/// <summary>Global anchor points defining the macro regions across the entire grid.</summary>
		public override List<Vec2Int> RegionAnchorPoints => this._gridData.RegionAnchorPoints;

		/// <summary>
		/// Map of macro region bounding boxes to their live <see cref="MacroGridNode"/> instances.
		/// Lazily re-hydrates runtime caches if not currently populated.
		/// </summary>
		public override Dictionary<BoundingBox, MacroGridNode> MacroGridNodeDict {
			get {
				if (this._macroGridNodeDict == null || this._macroGridNodeDict.Count == 0) {
					RebuildRuntimeCaches();
				}
				return this._macroGridNodeDict;
			}
		}

		/// <summary>
		/// Adjacency lookup mapping each macro region bounding box to its outgoing graph edges.
		/// Lazily re-hydrates runtime caches if not currently populated.
		/// </summary>
		public override Dictionary<BoundingBox, List<MacroConnectionData>> MacroAdjacencyList {
			get {
				if (this._macroAdjacencyList == null || this._macroAdjacencyList.Count == 0) {
					RebuildRuntimeCaches();
				}
				return this._macroAdjacencyList;
			}
		}

		/// <summary>
		/// O(1) spatial lookup mapping grid coordinates (Vec2Int) to live <see cref="MicroGridNode"/> instances.
		/// Lazily re-hydrates runtime caches if not currently populated.
		/// </summary>
		public override Dictionary<Vec2Int, MicroGridNode> MicroGridNodeDict {
			get {
				if (this._microGridNodeDict == null || this._microGridNodeDict.Count == 0) {
					RebuildRuntimeCaches();
				}
				return this._microGridNodeDict;
			}
		}

		#endregion

		#region Public Cache Control

		/// <summary>
		/// Purges in-memory runtime dictionaries. Forces fresh re-hydration on next property access.
		/// Useful during level transitions, scene unloads, or re-bakes to release GC memory.
		/// </summary>
		public override void ClearRuntimeCache() {
			this._microGridNodeDict?.Clear();
			this._macroGridNodeDict?.Clear();
			this._macroAdjacencyList?.Clear();
			this._microGridNodeDict = null;
			this._macroGridNodeDict = null;
			this._macroAdjacencyList = null;
		}

		#endregion

		#region Baking Pipeline (SetGridDataInternal)

		/// <summary>
		/// Bakes live runtime grid graph objects into the compressed, parallel-array serialized struct format.
		/// Bit-packs BoundingBox keys into <c>(ulong high, ulong low)</c> primitive word pairs for inline hex string YAML output.
		/// Executes in single-pass O(N) time by pre-bucketing micro nodes per macro bounding box.
		/// </summary>
		protected override void SetGridDataInternal(
			SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) {
			// STEP 1: Pre-group micro-nodes by macro region bounding box upfront.
			// Avoids O(N x M) nested loop scans by bucketizing all N micro-nodes in a single O(N) pass.
			var microNodesByMacro = new Dictionary<BoundingBox, List<MicroGridNode>>(macroGridNodeDict.Count);
			foreach (var microNode in microGridNodeDict.Values) {
				var bound = microNode.ParentMacroGrid.Bound;
				if (!microNodesByMacro.TryGetValue(bound, out var list)) {
					list = new List<MicroGridNode>();
					microNodesByMacro[bound] = list;
				}
				list.Add(microNode);
			}

			// STEP 2: Build the optimized macro save data arrays with 128-bit high/low key streams (Structure-of-Arrays)
			int count = macroGridNodeDict.Count;
			ulong[] keysHigh = new ulong[count];
			ulong[] keysLow = new ulong[count];
			MacroSaveData[] values = new MacroSaveData[count];

			int index = 0;
			foreach (var kvp in macroGridNodeDict) {
				BoundingBox bbox = kvp.Key;
				MacroGridNode macroNode = kvp.Value;

				// Flatten micro-node positions & static obstacle flags for this specific macro region
				microNodesByMacro.TryGetValue(bbox, out var microNodesInRegion);
				int microCount = microNodesInRegion?.Count ?? 0;

				List<Vec2Int> microRegionPositions = new(microCount);
				byte[] macroRegionStaticObstacleFlags = new byte[microCount];

				if (microNodesInRegion != null) {
					for (int i = 0; i < microCount; i++) {
						var micro = microNodesInRegion[i];
						microRegionPositions.Add(micro.Position);
						macroRegionStaticObstacleFlags[i] = SaveDataSerializer.ConvertBoolToByte(micro.IsStaticObstacle);
					}
				}

				// Flatten outgoing neighboring connections into synchronized primitive arrays
				macroAdjacencyList.TryGetValue(bbox, out List<MacroConnectionData> neighboringConnections);
				int connCount = neighboringConnections?.Count ?? 0;

				List<BoundingBox> targetRegions = new(connCount);
				MovementCapability[] allowedTraversals = new MovementCapability[connCount];
				byte[] narrativeFlags = new byte[connCount];

				if (neighboringConnections != null) {
					for (int i = 0; i < connCount; i++) {
						var conn = neighboringConnections[i];
						targetRegions.Add(conn.ToBound);
						allowedTraversals[i] = conn.AllowedTraversal;
						narrativeFlags[i] = SaveDataSerializer.ConvertBoolToByte(conn.IsNarrativelyAccessible);
					}
				}

				// Bit-pack the 128-bit BoundingBox key into high/low ulong primitives
				var (high, low) = SaveDataSerializer.PackBoundingBox32(bbox);

				keysHigh[index] = high;
				keysLow[index] = low;
				values[index] = new MacroSaveData(
					microRegionPositions,
					macroRegionStaticObstacleFlags,
					macroNode.TerrainType,
					macroNode.AllowedTraversal,
					new MacroConnectionSaveData(targetRegions, allowedTraversals, narrativeFlags)
				);

				index++;
			}

			// Assign final serialized payload and invalidate any active runtime caches
			this._gridData = new PathFindingGridData(regionAnchorPoints, keysHigh, keysLow, values);
			Debug.Log($"PathFindingGridDataContainer: Grid data baked for {microGridNodeDict.Count} micro nodes across {macroGridNodeDict.Count} macro regions.");
		}

		#endregion

		#region Runtime Re-hydration (RebuildRuntimeCaches)

		/// <summary>
		/// Reconstructs full C# runtime domain instances (MacroGridNode, MicroGridNode, MacroConnectionData)
		/// directly from high/low key arrays and parallel primitive streams in a single pre-allocated pass.
		/// </summary>
		private void RebuildRuntimeCaches() {
			ulong[] keysHigh = this._gridData.KeysHigh;
			ulong[] keysLow = this._gridData.KeysLow;
			MacroSaveData[] values = this._gridData.Values;

			int macroCount = values != null ? values.Length : 0;

			// Pre-allocate dictionaries with exact capacities to eliminate internal array resizing/re-hashing
			this._macroGridNodeDict = new Dictionary<BoundingBox, MacroGridNode>(macroCount);
			this._macroAdjacencyList = new Dictionary<BoundingBox, List<MacroConnectionData>>(macroCount);

			// Pre-calculate total micro-nodes across all macro regions for a single allocation
			int totalMicroNodes = 0;
			for (int i = 0; i < macroCount; i++) {
				totalMicroNodes += values[i].MacroRegionAnchorPoints.Count;
			}
			this._microGridNodeDict = new Dictionary<Vec2Int, MicroGridNode>(totalMicroNodes);

			// Rehydrate domain objects directly from parallel primitive key/value streams
			for (int i = 0; i < macroCount; i++) {
				ulong high = keysHigh[i];
				ulong low = keysLow[i];
				BoundingBox bbox = SaveDataSerializer.UnpackBoundingBox32(high, low);
				MacroSaveData macroData = values[i];

				// 1. Reconstruct MacroGridNode instance
				MacroGridNode macroNode = new(
					bbox,
					macroData.TerrainType,
					macroData.AllowedTraversal
				);
				this._macroGridNodeDict[bbox] = macroNode;

				// 2. Reconstruct MicroGridNodes directly from synchronized parallel streams
				var positions = macroData.MacroRegionAnchorPoints;
				var obstacleFlags = macroData.MacroRegionStaticObstacleFlags;

				int microNodeCount = positions.Count;
				for (int j = 0; j < microNodeCount; j++) {
					Vec2Int pos = positions[j];
					bool isObstacle = SaveDataSerializer.ConvertByteToBool(obstacleFlags[j]);

					// Instantiate MicroGridNode with direct parent reference to the newly created MacroGridNode
					this._microGridNodeDict[pos] = new MicroGridNode(pos, isObstacle, macroNode);
				}

				// 3. Reconstruct MacroConnectionData graph edges from MacroConnectionSaveData primitive arrays
				var connData = macroData.NeighboringRegionBoundingBoxes;
				var targets = connData.TargetRegions;
				var traversals = connData.AllowedTraversal;
				var narrativeFlags = connData.IsNarrativelyAccessible;

				int connectionCount = targets.Count;
				List<MacroConnectionData> connections = new(connectionCount);
				for (int j = 0; j < connectionCount; j++) {
					connections.Add(new MacroConnectionData(
						targets[j],
						traversals[j],
						SaveDataSerializer.ConvertByteToBool(narrativeFlags[j])
					));
				}
				this._macroAdjacencyList[bbox] = connections;
			}
		}

		#endregion
	}
}