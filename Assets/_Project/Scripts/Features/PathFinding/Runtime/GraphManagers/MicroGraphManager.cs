using System;
using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Node;

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
		private readonly SerializableDictionary<Vec2Int, MicroGridNode> _microNodes;
		public int MicroNodeCount => this._microNodes.Count;

		public MicroGraphManager() {
			this._microNodes = new SerializableDictionary<Vec2Int, MicroGridNode>();
		}

		public MicroGraphManager(SerializableDictionary<Vec2Int, MicroGridNode> microNodes) {
			this._microNodes = microNodes;
		}

		/// <summary>
		/// Registers or overwrites a micro grid node in the graph.
		/// </summary>
		/// <param name="node">The micro node to add.</param>
		public void RegisterNode(MicroGridNode node) {
			// Uses indexer [] to safely overwrite if it exists, avoiding ArgumentException crashes.
			this._microNodes[node.Position] = node;
		}

		/// <summary>
		/// Removes a micro node coordinate from the collection if it exists.
		/// </summary>
		/// <param name="position">The coordinate position to remove.</param>
		public void RemoveNode(Vec2Int position) {
			// Collection Remove operations are inherently safe and idempotent; 
			// calling it on a non-existent entry simply returns false without throwing.
			// We explicitly discard the return value using '_' since confirmation
			// is not required here.
			_ = this._microNodes.Remove(position);
		}

		/// <summary>
		/// Attempts to retrieve a micro grid node at the specified coordinate.
		/// </summary>
		public bool TryGetNode(Vec2Int position, out MicroGridNode node) {
			return this._microNodes.TryGetValue(position, out node);
		}

		/// <summary>
		/// Gets all adjacent, walkable neighbors for a given position (4-way directional).
		/// </summary>
		/// <param name="position">The central node position.</param>
		/// <returns>An enumeration of valid, non-obstacle neighbor nodes.</returns>
		public IEnumerable<MicroGridNode> GetWalkableNeighbors(Vec2Int position) {
			// Checks 4 cardinal directions using your predefined Vec2Int statics
			Vec2Int[] directions = { Vec2Int.Up, Vec2Int.Down, Vec2Int.Left, Vec2Int.Right };

			foreach (var dir in directions) {
				Vec2Int neighborPos = position + dir;
				if (TryGetNode(neighborPos, out MicroGridNode neighbor) && !neighbor.IsStaticObstacle) {
					yield return neighbor;
				}
			}
		}
	}
}