using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Manages the fine-grained, Tier-2 micro grid nodes for precise local pathfinding.
	/// </summary>
	/// <remarks>
	/// Handles <c>O(1)</c> spatial lookups and evaluates local neighbor connectivity (ignoring static obstacles) 
	/// for low-level path calculations like A* or Dijkstra.
	/// </remarks>
	[Serializable]
	public class MicroGraphManager {
		private static readonly Vec2Int[] CARDINAL_DIRECTIONS = new[] {
			Vec2Int.Up, Vec2Int.Down, Vec2Int.Left, Vec2Int.Right
		};

		// Reusable internal buffer to prevent heap allocations during neighbor collection
		private readonly MicroGridNode[] _neighborBuffer = new MicroGridNode[16];

		private readonly Dictionary<Vec2Int, MicroGridNode> _microNodes;
		public int MicroNodeCount => this._microNodes.Count;

		public MicroGraphManager() {
			this._microNodes = new Dictionary<Vec2Int, MicroGridNode>();
		}

		public MicroGraphManager(Dictionary<Vec2Int, MicroGridNode> microNodes) {
			this._microNodes = microNodes;
		}

		#region Management Methods
		/// <summary>
		/// Registers or overwrites a micro grid node in the graph.
		/// </summary>
		/// <param name="node">The micro node to add.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RegisterNode(MicroGridNode node) {
			this._microNodes[node.Position] = node;
		}

		/// <summary>
		/// Removes a micro node coordinate from the collection if it exists.
		/// </summary>
		/// <param name="position">The coordinate position to remove.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void RemoveNode(Vec2Int position) {
			_ = this._microNodes.Remove(position);
		}
		#endregion

		/// <summary>
		/// Attempts to retrieve a micro grid node at the specified coordinate.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetNode(Vec2Int position, out MicroGridNode node) {
			return this._microNodes.TryGetValue(position, out node);
		}

		/// <summary>
		/// Gets all adjacent, walkable neighbors for a given position (4-way directional).
		/// </summary>
		/// <param name="position">The central node position.</param>
		/// <returns>A sliced ReadOnlySpan of valid, non-obstacle neighbor nodes.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<MicroGridNode> GetWalkableNeighbors(Vec2Int position) {
			int count = 0;

			for (int i = 0; i < CARDINAL_DIRECTIONS.Length; i++) {
				Vec2Int neighborPos = position + CARDINAL_DIRECTIONS[i];
				if (TryGetNode(neighborPos, out MicroGridNode neighbor) && !neighbor.IsStaticObstacle) {
					this._neighborBuffer[count++] = neighbor;
				}
			}

			return this._neighborBuffer.AsSpan(0, count);
		}

		/// <summary>
		/// Retrieves valid walkable neighbors using custom directional offsets and optional diagonal corner-cutting rules.
		/// </summary>
		/// <param name="position">The central origin position.</param>
		/// <param name="offsets">Array of direction offsets (e.g., 4-way or 8-way).</param>
		/// <param name="cornerRules">Map defining required adjacent walkable offsets for diagonal steps.</param>
		/// <param name="visited">Optional set of already evaluated positions to skip immediately.</param>
		/// <returns>A sliced ReadOnlySpan of valid neighbor nodes that satisfy walkability and corner-cutting rules.</returns>
		public ReadOnlySpan<MicroGridNode> GetWalkableNeighborsWithRules(
		Vec2Int position,
		Vec2Int[] offsets,
		IReadOnlyDictionary<Vec2Int, (Vec2Int, Vec2Int)> cornerRules = null,
		HashSet<Vec2Int> visited = null) {

			int count = 0;

			for (int i = 0; i < offsets.Length; i++) {
				Vec2Int offset = offsets[i];
				Vec2Int neighborPos = position + offset;

				// 0. Skip immediately if neighbor has already been visited/closed
				if (visited != null && visited.Contains(neighborPos)) {
					continue;
				}

				// 1. Check if target neighbor exists and is walkable
				if (!TryGetNode(neighborPos, out MicroGridNode neighborNode) || neighborNode.IsStaticObstacle) {
					continue;
				}

				// 2. Check diagonal corner-cutting rules if provided
				if (cornerRules != null && cornerRules.TryGetValue(offset, out var requiredOffsets)) {
					Vec2Int reqPos1 = position + requiredOffsets.Item1;
					Vec2Int reqPos2 = position + requiredOffsets.Item2;

					if (!TryGetNode(reqPos1, out var reqNode1) || reqNode1.IsStaticObstacle ||
						!TryGetNode(reqPos2, out var reqNode2) || reqNode2.IsStaticObstacle) {
						continue; // Corner blocked
					}
				}

				this._neighborBuffer[count++] = neighborNode;
			}

			return this._neighborBuffer.AsSpan(0, count);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Generates a specified number of random start and end coordinate pairs from the 
		/// micro node collection for testing purposes.
		/// </summary>
		public IEnumerable<(Vec2Int startPos, Vec2Int endPos)> GiveRandomTestPointsLinq(int randomPathCount, int seed = 0) {
			if (this._microNodes == null || this._microNodes.Count < 2) {
				yield break;
			}

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
	}
}