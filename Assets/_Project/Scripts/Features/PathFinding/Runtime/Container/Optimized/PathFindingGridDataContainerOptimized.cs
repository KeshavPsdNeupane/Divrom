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
     *    O(N) time and flattened into compact, index-aligned primitive arrays (List<Vec2Int>, List<byte>).
     *    This minimizes YAML text tags, shrinking the disk footprint by ~97-99%.
     * 
     * 2. Runtime Phase (In-Memory Caches):
     *    On game boot or first property access, the container lazily re-hydrates primitive arrays into 
     *    fully featured runtime lookup Dictionaries (`RebuildRuntimeCaches`).
     * 
     * ==============================================================================================
     * DATA TRANSFORM & RE-HYDRATION GRAPH
     * ==============================================================================================
     * 
     * [BAKE TIME: SETGRIDDATAINTERNAL]
     *  Live Runtime Objects ────► O(N) Macro Bucketing ────► Flatten to Primitive Lists ────► YAML Asset (Disk)
     * 
     * [RUNTIME: LAZY RE-HYDRATION]
     *  YAML Asset (Disk)
     *    │
     *    ├── _gridData (PathFindingGridDataOptimized)
     *    │     └── SerializableDictionary<BoundingBox, MacroSaveDataOptimized>
     *    │           ├── Parallel Lists: Vec2Int[], byte[] (Micro Nodes)
     *    │           └── Parallel Lists: Target[], Traversal[], Flags[] (Connections)
     *    │
     *    ▼ (Triggered on 1st Property Getter Access)
     *  RebuildRuntimeCaches()  [Single-Pass Pre-Allocated Reconstruction]
     *    │
     *    ├──► _macroGridNodeDict  : Dictionary<BoundingBox, MacroGridNode>     (O(1) Macro Lookup)
     *    ├──► _microGridNodeDict  : Dictionary<Vec2Int, MicroGridNode>       (O(1) Micro Lookup)
     *    └──► _macroAdjacencyList : Dictionary<BoundingBox, List<Connection>> (O(1) Edge Lookup)
     * ==============================================================================================
     */

	/// <summary>
	/// ScriptableObject container that stores baked pathfinding grid data assets in a high-density, 
	/// flattened format on disk, and lazily re-hydrates full O(1) graph dictionaries at runtime.
	/// </summary>
	[CreateAssetMenu(
		fileName = "PathFindingGridDataContainerOptimized",
		menuName = "Scriptable Objects/PathFinding/Grid Data Container Optimized"
	)]
	public class PathFindingGridDataContainerOptimized : GridDataContainerBase {

		[Message(
			"Note: Due to Unity Inspector limitations with dynamic-height elements, " +
			"these fields cannot be fully locked with ReadOnly and can technically be mutated.\n\n" +
			"Manual modification is strongly discouraged. This data is exposed strictly " +
			"for debugging and verifying data integrity. Please leave these fields alone, " +
			"as any manual edits will be overwritten on the next bake.",
			MessageSeverity.Warning
		)]
		[Header("Baked Data")]
		[SerializeField] private PathFindingGridDataOptimized _gridData;

		// Non-serialized runtime lookup tables reconstructed on demand from serialized save data.
		// Kept null until first property access to eliminate startup memory overhead.
		private Dictionary<Vec2Int, MicroGridNode> _microGridNodeDict;
		private Dictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;
		private Dictionary<BoundingBox, List<MacroConnectionData>> _macroAdjacencyList;

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

		/// <summary>
		/// Bakes live runtime grid graph objects into the compressed, parallel-list serialized struct format.
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

			// STEP 2: Build the optimized macro save data dictionary
			var macroGridNodeSaveDataDict = new SerializableDictionary<BoundingBox, MacroSaveDataOptimized>(macroGridNodeDict.Count);

			foreach (var kvp in macroGridNodeDict) {
				BoundingBox bbox = kvp.Key;
				MacroGridNode macroNode = kvp.Value;

				// Flatten micro-node positions & static obstacle flags for this specific macro region
				microNodesByMacro.TryGetValue(bbox, out var microNodesInRegion);
				int microCount = microNodesInRegion?.Count ?? 0;

				List<Vec2Int> microRegionPositions = new(microCount);
				List<byte> macroRegionStaticObstacleFlags = new(microCount);

				if (microNodesInRegion != null) {
					for (int i = 0; i < microCount; i++) {
						var micro = microNodesInRegion[i];
						microRegionPositions.Add(micro.Position);
						macroRegionStaticObstacleFlags.Add(MacroSaveDataOptimized.ConvertBoolToByte(micro.IsStaticObstacle));
					}
				}

				// Flatten outgoing neighboring connections into synchronized primitive arrays
				macroAdjacencyList.TryGetValue(bbox, out List<MacroConnectionData> neighboringConnections);
				neighboringConnections ??= new List<MacroConnectionData>();

				int connCount = neighboringConnections.Count;
				List<BoundingBox> targetRegions = new(connCount);
				List<MovementCapability> allowedTraversals = new(connCount);
				List<byte> narrativeFlags = new(connCount);

				for (int i = 0; i < connCount; i++) {
					var conn = neighboringConnections[i];
					targetRegions.Add(conn.ToBound);
					allowedTraversals.Add(conn.AllowedTraversal);
					narrativeFlags.Add(MacroSaveDataOptimized.ConvertBoolToByte(conn.IsNarrativelyAccessible));
				}

				// Construct bucketed save struct for this macro region
				macroGridNodeSaveDataDict[bbox] = new MacroSaveDataOptimized(
					microRegionPositions,
					macroRegionStaticObstacleFlags,
					macroNode.TerrainType,
					macroNode.AllowedTraversal,
					new MacroConnectionSaveData(targetRegions, allowedTraversals, narrativeFlags)
				);
			}

			// Assign final serialized struct payload and invalidate any old runtime caches
			this._gridData = new PathFindingGridDataOptimized(regionAnchorPoints, macroGridNodeSaveDataDict);
			Debug.Log($"PathFindingGridDataContainerOptimized: Grid data baked for {microGridNodeDict.Count} micro nodes across {macroGridNodeDict.Count} macro regions.");
		}

		/// <summary>
		/// Reconstructs full C# runtime domain instances (MacroGridNode, MicroGridNode, MacroConnectionData)
		/// directly from the serialized parallel primitive lists in a single pre-allocated pass.
		/// </summary>
		private void RebuildRuntimeCaches() {
			var saveDataDict = this._gridData.MicroGridNodeSaveDataDict;

			// Pre-allocate dictionaries with exact capacities to eliminate internal array resizing/re-hashing
			this._macroGridNodeDict = new Dictionary<BoundingBox, MacroGridNode>(saveDataDict.Count);
			this._macroAdjacencyList = new Dictionary<BoundingBox, List<MacroConnectionData>>(saveDataDict.Count);

			// Pre-calculate exact total micro-nodes across all macro regions for single-allocation capacity
			int totalMicroNodes = 0;
			foreach (var kvp in saveDataDict) {
				totalMicroNodes += kvp.Value.MacroRegionAnchorPoints.Count;
			}
			this._microGridNodeDict = new Dictionary<Vec2Int, MicroGridNode>(totalMicroNodes);

			// Rehydrate domain objects from parallel primitive lists
			foreach (var kvp in saveDataDict) {
				BoundingBox bbox = kvp.Key;
				MacroSaveDataOptimized macroData = kvp.Value;

				// 1. Reconstruct MacroGridNode instance
				MacroGridNode macroNode = new MacroGridNode(
					bbox,
					macroData.TerrainType,
					macroData.AllowedTraversal
				);
				this._macroGridNodeDict[bbox] = macroNode;

				// 2. Reconstruct MicroGridNodes directly from synchronized parallel lists
				var positions = macroData.MacroRegionAnchorPoints;
				var obstacleFlags = macroData.MacroRegionStaticObstacleFlags;

				for (int i = 0; i < positions.Count; i++) {
					Vec2Int pos = positions[i];
					bool isObstacle = obstacleFlags[i] != 0;

					// Instantiate MicroGridNode with direct parent reference to the newly created MacroGridNode
					this._microGridNodeDict[pos] = new MicroGridNode(pos, isObstacle, macroNode);
				}

				// 3. Reconstruct MacroConnectionData graph edges from MacroConnectionSaveData lists
				var connData = macroData.NeighboringRegionBoundingBoxes;
				var targets = connData.TargetRegion;
				var traversals = connData.AllowedTraversal;
				var narrativeFlags = connData.IsNarrativelyAccessible;

				List<MacroConnectionData> connections = new(targets.Count);
				for (int i = 0; i < targets.Count; i++) {
					connections.Add(new MacroConnectionData(
						targets[i],
						traversals[i],
						narrativeFlags[i] != 0
					));
				}
				this._macroAdjacencyList[bbox] = connections;
			}
		}
	}
}