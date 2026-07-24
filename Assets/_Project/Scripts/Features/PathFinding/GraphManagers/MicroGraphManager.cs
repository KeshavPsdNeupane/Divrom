using System;
using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.Feature.PathFinding;
using UnityEngine;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Manages the fine-grained, Tier-2 micro grid nodes for precise local pathfinding.
	/// </summary>
	/// <remarks>
	/// Handles <c>O(1)</c> spatial lookups and evaluates local neighbor connectivity (ignoring static obstacles) 
	/// for low-level path calculations like A* or Dijkstra.
	/// </remarks>
	public class MicroGraphManager {
		private readonly SerializableDictionary<Vec2Int, MicroGridNode> _microNodes;

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


	/// <summary>
	/// Centralized orchestrator for the hierarchical pathfinding system.
	/// </summary>
	/// <remarks>
	/// Acts as a Facade that encapsulates both Tier-1 (Macro) and Tier-2 (Micro) graphs. 
	/// External systems (like AI controllers) should query this manager rather than interacting 
	/// with the underlying micro/macro dictionaries directly.
	/// </remarks>
	public class PathfindingGraphManager {

		/// <summary>
		/// Gets the Tier-1 macro graph responsible for region-to-region traversal calculations.
		/// </summary>
		public MacroGraphManager MacroGraph { get; }

		/// <summary>
		/// Gets the Tier-2 micro graph responsible for tile-by-tile local navigation.
		/// </summary>
		public MicroGraphManager MicroGraph { get; }

		public PathfindingGraphManager() {
			MacroGraph = new MacroGraphManager();
			MicroGraph = new MicroGraphManager();
		}

		public PathfindingGraphManager(MacroGraphManager macroGraph, MicroGraphManager microGraph) {
			MacroGraph = macroGraph ?? new MacroGraphManager();
			MicroGraph = microGraph ?? new MicroGraphManager();
		}

		/// <summary>
		/// Safely registers a micro node and automatically associates it with its parent macro region.
		/// </summary>
		/// <param name="microNode">The micro node to register.</param>
		public void RegisterMicroNode(MicroGridNode microNode) {
			// 1. Add to the Micro graph
			MicroGraph.RegisterNode(microNode);

			// 2. Add to the Parent Macro node's bounded list
			if (microNode.ParentMacroGrid != null) {
				microNode.ParentMacroGrid.AddMicroGridNodePosition(microNode.Position);

				// 3. Ensure the macro node itself is registered in the macro graph
				MacroGraph.RegisterNode(microNode.ParentMacroGrid);
			} else {
				Debug.LogWarning($"MicroGridNode at {microNode.Position} has no ParentMacroGrid assigned.");
			}
		}

		/// <summary>
		/// Registers a macro node. (Usually handled automatically by <see cref="RegisterMicroNode"/>).
		/// </summary>
		public void RegisterMacroNode(MacroGridNode macroNode) {
			MacroGraph.RegisterNode(macroNode);
		}

		/// <summary>
		/// Resolves the parent macro node for a specific exact micro grid coordinate.
		/// </summary>
		/// <param name="position">The micro grid coordinate.</param>
		/// <param name="macroNode">The resolved macro node, if any.</param>
		/// <returns><c>true</c> if a valid micro node and its parent macro node were found; otherwise, <c>false</c>.</returns>
		public bool TryGetMacroNodeFromPosition(Vec2Int position, out MacroGridNode macroNode) {
			macroNode = null;
			if (MicroGraph.TryGetNode(position, out MicroGridNode micro) && micro.ParentMacroGrid != null) {
				macroNode = micro.ParentMacroGrid;
				return true;
			}
			return false;
		}
	}
}