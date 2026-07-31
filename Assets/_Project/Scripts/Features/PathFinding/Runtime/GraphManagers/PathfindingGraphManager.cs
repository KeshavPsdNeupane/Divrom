using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

namespace Project.Scripts.Features.PathFinding.GraphManager {

	/// <summary>
	/// Central manager orchestrating two-tier hierarchical graph operations:
	/// Tier-1 Macro regions and Tier-2 Micro local grid nodes.
	/// </summary>
	[Serializable]
	public class PathfindingGraphManager {

		#region Fields & Properties

		/// <summary>
		/// Tier-1 macro graph responsible for region-to-region traversal calculations.
		/// </summary>
		private readonly MacroGraphManager _macroGraph;

		/// <summary>
		/// Tier-2 micro graph responsible for tile-by-tile local navigation.
		/// </summary>
		private readonly MicroGraphManager _microGraph;

		public int MacroNodeCount => this._macroGraph.MacroNodeCount;
		public int MicroNodeCount => this._microGraph.MicroNodeCount;

		#endregion

		#region Constructors

		public PathfindingGraphManager() {
			this._macroGraph = new MacroGraphManager();
			this._microGraph = new MicroGraphManager();
		}

		public PathfindingGraphManager(MacroGraphManager macroGraph, MicroGraphManager microGraph) {
			this._macroGraph = macroGraph ?? new MacroGraphManager();
			this._microGraph = microGraph ?? new MicroGraphManager();
		}

		#endregion

		#region Micro Node Operations

		/// <summary>
		/// Attempts to retrieve a micro node at the target coordinate.
		/// </summary>
		public bool TryGetMicroNode(Vec2Int position, [MaybeNullWhen(false)] out MicroGridNode microNode) {
			return this._microGraph.TryGetNode(position, out microNode);
		}

		/// <summary>
		/// Retrieves simple cardinal (4-way) walkable neighbors for a micro position.
		/// </summary>
		public ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodes(Vec2Int position) {
			return this._microGraph.GetWalkableNeighbors(position);
		}

		/// <summary>
		/// Retrieves walkable neighbors applying custom movement directional offsets, diagonal corner-cutting rules, and closed-set filtering.
		/// </summary>
		/// <summary>
		/// Retrieves walkable neighbors applying custom movement directional offsets, diagonal corner-cutting rules, and closed-set filtering.
		/// </summary>
		public ReadOnlySpan<MicroGridNode> GetWalkableMicroNeighboringNodesWithRules(
			Vec2Int position,
			Vec2Int[] neighborOffsets,
			IReadOnlyDictionary<Vec2Int, (Vec2Int req1, Vec2Int req2)> neighborRules = null,
			HashSet<Vec2Int> visited = null) {
			return this._microGraph.GetWalkableNeighborsWithRules(position, neighborOffsets, neighborRules, visited);
		}

		/// <summary>
		/// Safely registers a micro node and automatically associates it with its parent macro region.
		/// </summary>
		/// <param name="microNode">The micro node to register.</param>
		public void RegisterMicroNode(MicroGridNode microNode) {
			// 1. Add to the Micro graph
			this._microGraph.RegisterNode(microNode);

			// 2. Add to the Parent Macro node's bounded list
			if (microNode.ParentMacroGrid != null) {
				microNode.ParentMacroGrid.AddMicroGridNodePosition(microNode.Position);

				// 3. Ensure the macro node itself is registered in the macro graph
				this._macroGraph.RegisterNode(microNode.ParentMacroGrid);
			} else {
				Debug.LogWarning($"MicroGridNode at {microNode.Position} has no ParentMacroGrid assigned.");
			}
		}

		/// <summary>
		/// Removes a micro node coordinate from the graph and unlinks it from its parent macro region.
		/// </summary>
		/// <param name="position">The micro coordinate to remove.</param>
		public void RemoveMicroNode(Vec2Int position) {
			if (this._microGraph.TryGetNode(position, out MicroGridNode microNode)) {
				this._microGraph.RemoveNode(position);
				// Unlink from Parent Macro region bounded list
				microNode.ParentMacroGrid?.RemoveMicroGridNodePosition(position);
			} else {
				Debug.LogWarning($"Attempted to remove MicroGridNode at {position}, but it does not exist.");
			}
		}

		#endregion

		#region Macro Node Operations


		public HashSet<Vec2Int> GetAllCorridorPositions(List<BoundingBox> macroNodes) {
			return this._macroGraph.GetAllCorridorPositions(macroNodes);
		}

		/// <summary>
		/// Attempts to retrieve a macro node by its bounding box identity.
		/// </summary>
		public bool TryGetMacroNode(BoundingBox box, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			return this._macroGraph.TryGetNode(box, out macroNode);
		}

		/// <summary>
		/// Registers a macro node in the macro graph.
		/// </summary>
		public void RegisterMacroNode(MacroGridNode macroNode) {
			this._macroGraph.RegisterNode(macroNode);
		}

		/// <summary>
		/// Removes a macro region node and cascades the deletion down to all constituent micro tiles registered within it.
		/// </summary>
		/// <param name="box">The bounding box defining the macro region to remove.</param>
		public void RemoveMacroNode(BoundingBox box) {
			if (this._macroGraph.TryRemoveNode(box, out var microTilesPositions)) {
				// Cascade deletion down to fine-grained micro nodes
				foreach (var pos in microTilesPositions) {
					this._microGraph.RemoveNode(pos);
				}
			}
		}

		/// <summary>
		/// Resolves the parent macro node for a specific exact micro grid coordinate.
		/// </summary>
		/// <param name="position">The micro grid coordinate.</param>
		/// <param name="macroNode">The resolved macro node, if any.</param>
		/// <returns><c>true</c> if a valid micro node and its parent macro node were found; otherwise, <c>false</c>.</returns>
		public bool TryGetMacroNodeFromPosition(Vec2Int position, [MaybeNullWhen(false)] out MacroGridNode macroNode) {
			macroNode = null;
			if (this._microGraph.TryGetNode(position, out MicroGridNode micro) && micro.ParentMacroGrid != null) {
				macroNode = micro.ParentMacroGrid;
				return true;
			}
			return false;
		}

		/// <summary>
		/// Retrieves traversable inter-region connections from a given macro region based on entity movement capabilities.
		/// </summary>
		public bool GetNeighboringMacroNodesConnectionData(
			BoundingBox box,
			MovementCapability entityMovementCapability,
			out IEnumerable<MacroConnectionData> connections) {
			return this._macroGraph.GetTraversableConnections(box, entityMovementCapability, out connections);
		}

		#endregion

		#region Debug & Test Tools

		/// <summary>
		/// Generates random micro coordinate pairs for pathfinding unit tests and benchmark evaluation.
		/// </summary>
		public IEnumerable<(Vec2Int startPos, Vec2Int endPos)> GiveRandomTestPoints(int randomPathCount, int seed = 0) {
			return this._microGraph.GiveRandomTestPointsLinq(randomPathCount, seed);
		}

		#endregion
	}
}