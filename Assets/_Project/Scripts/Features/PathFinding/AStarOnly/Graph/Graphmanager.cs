using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;


namespace Kope.Feature.PathFindingNew.Graph {

	/// <summary>
	/// Manages the runtime graph topology, offering zero-allocation, thread-safe spatial lookups 
	/// and bitmask-validated 8-directional neighbor expansion for pathfinding queries.
	/// </summary>
	public class GraphManager {

		/// <summary>
		/// Contains constants and lookup tables used for neighbor expansion and movement validation in the graph.
		/// <para>
		/// <strong>Design Rationale:</strong>
		/// <list type="bullet">
		/// <item><strong>Precomputed Offsets:</strong> The NEIGHBOR_OFFSET array defines the relative 
		/// grid offsets for all 8 neighboring positions, allowing for fast spatial calculations 
		/// without repeated arithmetic.</item>
		/// <item><strong>Movement Validation Rules:</strong> The CHECK_FOR_BLOCK_RULE and NEIGHBOR_RULES 
		/// arrays encode the rules for diagonal movement, ensuring that diagonal neighbors are only considered
		/// walkable if their required orthogonal neighbors are also walkable, preventing
		/// corner-cutting through obstacles.</item>
		/// <item><strong>Thread Safety:</strong> All constants are static and read-only, 
		/// allowing multiple threads to safely access them without locks or synchronization.</item>
		/// </list>
		/// </para>
		/// </summary>
		internal static class Constants {
			// Making them clockwise to match the neighbor rules
			public static readonly Vec2Int[] NEIGHBOR_OFFSET = new[] {
				new Vec2Int(-1, 1),  // 0: Up-Left
                new Vec2Int(0, 1),   // 1: Up
                new Vec2Int(1, 1),   // 2: Up-Right
                new Vec2Int(1, 0),   // 3: Right
                new Vec2Int(1, -1),  // 4: Down-Right
                new Vec2Int(0, -1),  // 5: Down
                new Vec2Int(-1, -1), // 6: Down-Left
                new Vec2Int(-1, 0),  // 7: Left
            };

			// true at every diagonal index (0,2,4,6), false at every cardinal index (1,3,5,7).
			// This doubles as "is this neighbor a diagonal move" outside of its original
			// corner-cutting purpose — see TryGetNeighbors' diagonalMask output.
			public static readonly bool[] CHECK_FOR_BLOCK_RULE = {
				true,  // for Up-Left
                false, // for Up
                true,  // for Up-Right
                false, // for Right
                true,  // for Down-Right
                false, // for Down
                true,  // for Down-Left
                false  // for Left
            };

			public static readonly (int req1, int req2)[] NEIGHBOR_RULES = {
                // Array indices of the two required orthogonal neighbors for diagonal movement
                (1, 7), // for Up-Left (requires Up & Left)
                (1, 3), // for Up-Right (requires Up & Right)
                (3, 5), // for Down-Right (requires Right & Down)
                (5, 7), // for Down-Left (requires Down & Left)
            };
		}

		private const int INITIAL_NODE_CAPACITY = 512;
		private const int INITIAL_REGION_CAPACITY = 32;

		private readonly Dictionary<ushort, Vec2Int[]> regionTilePositions;
		private readonly Dictionary<Vec2Int, GridNode> nodes;
		public int TotalNodeCount => nodes.Count;

		public GraphManager() {
			nodes = new Dictionary<Vec2Int, GridNode>(INITIAL_NODE_CAPACITY);
			regionTilePositions = new Dictionary<ushort, Vec2Int[]>(INITIAL_REGION_CAPACITY);
		}

		public GraphManager(Dictionary<Vec2Int, GridNode> nodes, Dictionary<ushort, Vec2Int[]> regionTilePositions) {
			this.nodes = nodes;
			this.regionTilePositions = regionTilePositions;
			//LogSize();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsValidNode(Vec2Int position) {
			return nodes.ContainsKey(position);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetNode(Vec2Int position, out GridNode node) {
			return nodes.TryGetValue(position, out node);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsWalkable(Vec2Int position, MovementCapability movementCapability) {
			return nodes.TryGetValue(position, out GridNode node) && node.IsTraversable(movementCapability);
		}




		/// <summary>
		/// Tries to find and filter valid 8-directional neighbors for a node at the specified position.
		/// <para>
		/// <strong>Performance &amp; Thread Safety Rationale:</strong>
		/// <list type="bullet">
		/// <item><strong>Zero Allocations:</strong> By passing pre-allocated scratchpad buffers 
		/// (<paramref name="fetchBuffer"/> and <paramref name="neighborsBuffer"/>), 
		/// this method eliminates heap allocations during high-frequency A* expansion loops,
		/// completely preventing Garbage Collection pressure.</item>
		/// <item><strong>Thread Safety:</strong> The method maintains zero internal mutable state. 
		/// All temporary state is isolated to the caller-owned buffers, 
		/// allowing multiple worker threads to execute concurrent pathfinding queries on the same
		/// graph safely without race conditions or locks.</item>
		/// </list>
		/// </para>
		/// </summary>
		/// <param name="position">The grid coordinate of the node whose neighbors are being evaluated.</param>\
		/// <param name="movementCapability">The movement capability used to filter walkable neighbors.</param>
		/// <param name="fetchBuffer">A scratchpad array (at least length 8) used to temporarily hold raw spatial lookups.</param>
		/// <param name="neighborsBuffer">A span/array container where final walkability-filtered and rule-validated neighbors are written.</param>
		/// <param name="diagonalMask">
		/// Bit <c>j</c> is set if the neighbor written to <c>neighborsBuffer[j]</c> in the returned span
		/// is a diagonal move (as opposed to cardinal). Callers use this to price the edge with
		/// <see cref="GridNode.DIAGONAL_COST"/> vs <see cref="GridNode.DIRECT_COST"/> without having to
		/// re-derive direction from the two positions.
		/// </param>
		/// <returns>A zero-allocation <see cref="ReadOnlySpan{GridNode}"/> view representing the valid subset of filtered neighbors.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<GridNode> TryGetNeighbors(Vec2Int position, MovementCapability movementCapability,
		 GridNode[] fetchBuffer, Span<GridNode> neighborsBuffer, out byte diagonalMask) {
			byte walkableMask = 0;
			for (int i = 0; i < 8; i++) {
				Vec2Int nPos = position + Constants.NEIGHBOR_OFFSET[i];

				if (nodes.TryGetValue(nPos, out GridNode nNode) && nNode.IsTraversable(movementCapability)) {
#if UNITY_EDITOR
					if (nNode.Position != nPos) {
						Debug.LogError(
							$"[Graphmanager] Node position mismatch: dictionary key {nPos} holds a GridNode " +
							$"whose own Position field is {nNode.Position}. AStar.FindPath trusts " +
							$"GridNode.Position (not the dictionary key) when building neighborPos, so this " +
							$"mismatch will make the search silently jump to the wrong cell. Check whatever " +
							$"populates GridNodeDict for {nPos}."
						);
					}
#endif
					fetchBuffer[i] = nNode;
					walkableMask |= (byte)(1 << i);
				}
			}

			int count = 0;
			diagonalMask = 0;

			for (int i = 0; i < 8; i++) {
				if ((walkableMask & (1 << i)) == 0) continue;

				bool isDiagonal = Constants.CHECK_FOR_BLOCK_RULE[i];

				if (isDiagonal) {
					var (req1, req2) = Constants.NEIGHBOR_RULES[i >> 1];

					bool req1Walkable = (walkableMask & (1 << req1)) != 0;
					bool req2Walkable = (walkableMask & (1 << req2)) != 0;

					if (!req1Walkable || !req2Walkable) continue;

					diagonalMask |= (byte)(1 << count);
				}

				neighborsBuffer[count++] = fetchBuffer[i];
			}
			return neighborsBuffer[..count];
		}

		/// <summary>
		/// Generates random start/end coordinate pairs drawn from the currently loaded nodes, for
		/// use by the benchmark suite (<c>PathFindingNewDebugger.RunBenchmarkSuite</c>). Both ends
		/// of a pair are drawn independently, so a pair may occasionally land on the same node or
		/// on two disconnected nodes — the benchmark's warmup call already treats
		/// <see cref="PathFinding.PathFindingStatus.InvalidStartOrEnd"/> as a skip, and a
		/// same-node or no-path pair is still a valid (if trivial/zero-length) timing sample, so
		/// no extra filtering is done here.
		/// </summary>
		/// <param name="pairCount">How many start/end pairs to generate.</param>
		/// <returns>Up to <paramref name="pairCount"/> pairs. Empty if fewer than 2 nodes are loaded.</returns>
		public List<(Vec2Int start, Vec2Int end)> GiveRandomTestPoints(int pairCount) {
			var pairs = new List<(Vec2Int start, Vec2Int end)>(Math.Max(pairCount, 0));
			if (this.nodes.Count < 2 || pairCount <= 0) return pairs;

			var keys = new List<Vec2Int>(this.nodes.Keys);
			var rng = new System.Random();

			for (int i = 0; i < pairCount; i++) {
				Vec2Int start = keys[rng.Next(keys.Count)];
				Vec2Int end = keys[rng.Next(keys.Count)];
				pairs.Add((start, end));
			}

			return pairs;
		}


		private void LogSize() {
			// --- 1. NODES DICTIONARY CALCULATION ---
			// Change 'isGridNodeClass' to true if GridNode is a 'class', false if it is a 'struct'
			bool isGridNodeClass = false;

			int nodeCapacity = nodes.Count; // Estimate (Dictionaries allocate in capacity buckets)
			int vec2Size = 8; // Vec2Int is 2x int32 = 8 bytes
			int gridNodeValueSize = isGridNodeClass ? 8 : Marshal.SizeOf<GridNode>(); // Pointer vs Struct

			// Entry struct = int hashCode (4B) + int next (4B) + Vec2Int key (8B) + GridNode value
			int nodeEntrySize = 16 + gridNodeValueSize;

			long nodesDictMemory = 64                               // Dictionary instance overhead
								 + 24 + (nodeCapacity * 4)        // int[] buckets array header + elements
								 + 24 + (nodeCapacity * nodeEntrySize); // Entry[] entries array header + elements

			if (isGridNodeClass) {
				// If GridNode is a class, add heap overhead (24B header + fields) per instance
				nodesDictMemory += nodes.Count * (24 + Marshal.SizeOf<GridNode>());
			}

			// --- 2. REGION DICTIONARY CALCULATION ---
			int regionCapacity = regionTilePositions.Count;
			// Entry struct = int hashCode (4B) + int next (4B) + ushort key (2B + 2B pad) + Vec2Int[] ref (8B) = 20B (padded to 24B)
			int regionEntrySize = 24;

			long regionDictMemory = 64
								  + 24 + (regionCapacity * 4)
								  + 24 + (regionCapacity * regionEntrySize);

			// Add actual heap memory used by all Vec2Int[] child arrays
			foreach (var tileArray in regionTilePositions.Values) {
				if (tileArray != null) {
					// 24-byte C# array header + (length * 8 bytes per Vec2Int)
					regionDictMemory += 24 + (tileArray.Length * vec2Size);
				}
			}

			Debug.Log($"[GraphManager] Initialized with {nodes.Count} nodes and {regionTilePositions.Count} regions.");
			Debug.Log($"Nodes Dict: {nodesDictMemory} bytes (~{nodesDictMemory / 1024f:F1} KB) | Region Dict: {regionDictMemory} bytes (~{regionDictMemory / 1024f:F1} KB)");

		}
	}
}
