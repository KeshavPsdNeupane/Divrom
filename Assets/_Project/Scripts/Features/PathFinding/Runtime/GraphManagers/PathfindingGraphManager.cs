using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using ZLinq;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Central manager orchestrating two-tier hierarchical graph operations.
	/// Flattened to allow cross-boundary optimizations, zero-allocation lookups, and shared buffers.
	/// This acts as the single source of truth for both Micro (tile-level) and Macro (region/room-level) pathing.
	/// </summary>
	[Serializable]
	public class PathfindingGraphManager : IPathfindingGraphManager {

		#region Fields

		// --- Micro Graph State ---

		/// <summary>
		/// Cached cardinal directions to prevent array allocation during neighbor lookups.
		/// </summary>
		private static readonly Vec2Int[] CARDINAL_DIRECTIONS = new[] {
			Vec2Int.Up, Vec2Int.Down, Vec2Int.Left, Vec2Int.Right
		};

		/// <summary>
		/// Pre-allocated buffer for retrieving neighbors. Returned as a ReadOnlySpan to guarantee 
		/// zero GC allocation during aggressive A* or Dijkstra pathfinding loops.
		/// </summary>
		private readonly MicroGridNode[] _neighborBuffer = new MicroGridNode[16];

		/// <summary>
		/// The authoritative lookup for all micro nodes, mapped by their global 2D grid position.
		/// </summary>
		private readonly Dictionary<Vec2Int, MicroGridNode> _microNodes;

		// --- Macro Graph State ---

		/// <summary>
		/// The authoritative lookup for all macro nodes (regions/rooms), mapped by their bounding boxes.
		/// </summary>
		private readonly Dictionary<BoundingBox, MacroGridNode> _macroNodes;

		/// <summary>
		/// Directed adjacency list defining traversable edges between Macro nodes.
		/// </summary>
		private readonly Dictionary<BoundingBox, List<MacroConnectionData>> _adjacencyDict;

		/// <summary>
		/// Pre-allocated HashSet used to aggregate corridor positions without allocating new collections.
		/// </summary>
		private readonly HashSet<Vec2Int> _corridorPositions = new();

		public int MacroNodeCount => this._macroNodes.Count;
		public int MicroNodeCount => this._microNodes.Count;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new, empty PathfindingGraphManager.
		/// </summary>
		public PathfindingGraphManager() {
			this._microNodes = new Dictionary<Vec2Int, MicroGridNode>();
			this._macroNodes = new Dictionary<BoundingBox, MacroGridNode>();
			this._adjacencyDict = new Dictionary<BoundingBox, List<MacroConnectionData>>();
		}

		/// <summary>
		/// Initializes a PathfindingGraphManager with pre-existing graph data.
		/// </summary>
		public PathfindingGraphManager(
			Dictionary<Vec2Int, MicroGridNode> microNodes,
			Dictionary<BoundingBox, MacroGridNode> macroNodes,
			Dictionary<BoundingBox, List<MacroConnectionData>> adjacencyDict) {
			this._microNodes = microNodes;
			this._macroNodes = macroNodes;
			this._adjacencyDict = adjacencyDict;
		}

		#endregion

		#region Micro Node Operations

		/// <summary>
		/// Attempts to retrieve a micro node at the specified position.
		/// Inlined for maximum performance during continuous pathfinding queries.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetMicroNode(Vec2Int position, [MaybeNullWhen(false)] out MicroGridNode microNode) {
			return this._microNodes.TryGetValue(position, out microNode);
		}


		/// <summary>
		/// Retrieves walkable cardinal neighbors for a given position.
		/// </summary>
		/// <returns>A zero-allocation Span slicing into the pre-allocated neighbor buffer.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodes(Vec2Int position) {
			int count = 0;
			for (int i = 0; i < CARDINAL_DIRECTIONS.Length; i++) {
				Vec2Int neighborPos = position + CARDINAL_DIRECTIONS[i];

				// Only yield the node if it exists and is not statically blocked
				if (TryGetMicroNode(neighborPos, out MicroGridNode neighbor) && !neighbor.IsStaticObstacle) {
					this._neighborBuffer[count++] = neighbor;
				}
			}
			return this._neighborBuffer.AsSpan(0, count);
		}

		/// <summary>
		/// Retrieves walkable neighbors based on custom offsets and specific geometric clearance rules 
		/// (e.g., preventing diagonal movement that clips through corners).
		/// </summary>
		public ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodesWithRules(
			Vec2Int position,
			Vec2Int[] neighborOffsets,
			IReadOnlyDictionary<Vec2Int, (Vec2Int req1, Vec2Int req2)> neighborRules = null,
			HashSet<Vec2Int> visited = null) {

			int count = 0;
			for (int i = 0; i < neighborOffsets.Length; i++) {
				Vec2Int offset = neighborOffsets[i];
				Vec2Int neighborPos = position + offset;

				// Skip if already visited or if the node is missing/blocked
				if (visited != null && visited.Contains(neighborPos)) continue;
				if (!TryGetMicroNode(neighborPos, out MicroGridNode neighborNode) || neighborNode.IsStaticObstacle) continue;

				// Evaluate conditional movement rules (e.g., diagonal movement requires both adjacent cardinals to be open)
				if (neighborRules != null && neighborRules.TryGetValue(offset, out var requiredOffsets)) {
					Vec2Int reqPos1 = position + requiredOffsets.req1;
					Vec2Int reqPos2 = position + requiredOffsets.req2;

					if (!TryGetMicroNode(reqPos1, out var reqNode1) || reqNode1.IsStaticObstacle ||
						!TryGetMicroNode(reqPos2, out var reqNode2) || reqNode2.IsStaticObstacle) {
						continue;
					}
				}

				this._neighborBuffer[count++] = neighborNode;
			}
			return this._neighborBuffer.AsSpan(0, count);
		}

		#endregion

		#region Macro Node Operations

		/// <summary>
		/// Attempts to retrieve a macro node by its BoundingBox footprint.
		/// </summary>
		public bool TryGetMacroNode(BoundingBox box, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			return this._macroNodes.TryGetValue(box, out macroNode);
		}

		/// <summary>
		/// Reverse lookup: Finds which Macro region a specific Micro position belongs to.
		/// </summary>
		public bool TryGetMacroNodeFromPosition(Vec2Int position, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			macroNode = null;
			if (this._microNodes.TryGetValue(position, out MicroGridNode micro) && micro.ParentBBox != null) {
				if (this._macroNodes.TryGetValue(micro.ParentBBox, out macroNode)) {
					return true;
				}
			}
			return false;
		}


		/// <summary>
		/// Aggregates all Micro grid positions contained within a span of Macro nodes.
		/// Zero-allocation input and output processing.
		/// </summary>
		public HashSet<Vec2Int> GetAllCorridorPositions(List<BoundingBox> macroNodes) {
			this._corridorPositions.Clear();

			var macroDict = this._macroNodes;
			for (int i = 0; i < macroNodes.Count; i++) {
				if (macroDict.TryGetValue(macroNodes[i], out MacroGridNode node)) {
					ReadOnlySpan<Vec2Int> positions = node.MicroGridNodePositions;
					for (int j = 0; j < positions.Length; j++) {
						this._corridorPositions.Add(positions[j]);
					}
				}
			}

			return this._corridorPositions;
		}

		#endregion

		#region Macro Adjacency & Traversal Operations
		/// <summary>
		/// Gets all outbound macro connections from a given box that satisfy the requested movement capability.
		/// Note: Uses ZLinq's AsValueEnumerable to evaluate the struct sequence without allocating enumerators.
		/// </summary>
		public bool GetNeighboringMacroNodesConnectionData(
			BoundingBox box, MovementCapability capability,
			out IReadOnlyList<MacroConnectionData> connections) {
			connections = null;
			if (!this._adjacencyDict.TryGetValue(box, out var list)) return false;

			connections = list.AsValueEnumerable().Where(c => c.IsTraversable(capability)).ToList();
			return true;
		}

		/// <summary>
		/// Dynamically locks or unlocks an edge based on gameplay/narrative conditions 
		/// (e.g., locking a door until a key is found).
		/// </summary>
		public void SetNarrativeAccess(BoundingBox from, BoundingBox to, bool isAccessible, bool isBidirectional = true) {
			ToggleConnectionAccess(from, to, isAccessible);
			if (isBidirectional) {
				ToggleConnectionAccess(to, from, isAccessible);
			}
		}

		/// <summary>
		/// Internal helper mutating the struct state for narrative access.
		/// </summary>
		private void ToggleConnectionAccess(BoundingBox from, BoundingBox to, bool isAccessible) {
			if (this._adjacencyDict.TryGetValue(from, out var connections)) {
				int index = connections.FindIndex(c => c.ToBound == to);
				if (index >= 0) {
					// Update the struct in-place by creating a modified copy
					connections[index] = connections[index].WithNarrativeAccess(isAccessible);
				}
			}
		}

		#endregion

		#region Debug & Test Tools

#if UNITY_EDITOR
		/// <summary>
		/// Generates random micro coordinate pairs for pathfinding unit tests and benchmark evaluation.
		/// Useful for testing A* permutations and stress-testing the zero-allocation buffers.
		/// </summary>
		public IEnumerable<(Vec2Int startPos, Vec2Int endPos)> GiveRandomTestPoints(int randomPathCount, int seed = 0) {
			if (this._microNodes == null || this._microNodes.Count < 2) yield break;

			var keys = this._microNodes.Keys.ToArray();
			int count = keys.Length;
			UnityEngine.Random.InitState(seed);

			for (int i = 0; i < randomPathCount; i++) {
				int startIdx = UnityEngine.Random.Range(0, count);
				int endIdx;
				do {
					endIdx = UnityEngine.Random.Range(0, count);
				} while (endIdx == startIdx && count > 1);

				yield return (keys[startIdx], keys[endIdx]);
			}
		}
#endif

		#endregion
	}
}