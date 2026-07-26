using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;

public struct MacroPathFindingNode : IHasCost<float>, IEquatable<MacroPathFindingNode> {
	public MacroGridNode Node;
	public float GCost;
	public float HCost;
	public readonly float FCost {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get { return GCost + HCost; }
	}
	public BoundingBox? Parent;

	public MacroPathFindingNode(MacroGridNode node, float gCost, float hCost, BoundingBox? parent) {
		this.Node = node;
		this.GCost = gCost;
		this.HCost = hCost;
		this.Parent = parent;
	}

	public readonly float GetCost() => this.FCost;

	public readonly bool Equals(MacroPathFindingNode other) {
		if (this.Node == null) return other.Node == null;
		return this.Node.Equals(other.Node);
	}

	public override readonly bool Equals(object obj) => obj is MacroPathFindingNode otherNode && Equals(otherNode);
	public override readonly int GetHashCode() => Node != null ? Node.GetHashCode() : 0;

	public static bool operator ==(MacroPathFindingNode left, MacroPathFindingNode right) => left.Equals(right);
	public static bool operator !=(MacroPathFindingNode left, MacroPathFindingNode right) => !left.Equals(right);

	public override readonly string ToString() {
		string parentStr = Parent.HasValue ? Parent.Value.ToString() : "None";
		return $"MacroPathFindingNode(Node: {Node}, GCost: {GCost}, HCost: {HCost}, FCost: {FCost}, Parent: {parentStr})";
	}
}

public class AStarMacro {

	public const int MAX_ITERATIONS = 1000;
	public const int DEFAULT_INITIAL_CAPACITY = 32;
	/// <summary>
	/// The maximum number of iterations the A* algorithm will perform
	/// before giving up on finding a path.
	/// This is a safeguard against infinite loops in cases where no path exists.
	/// For macro level pathfinding, this is usually sufficient, but can be adjusted based on
	/// the size of the macro graph.
	/// </summary>
	private readonly int DefaultMAXIterations;
	private readonly PathfindingGraphManager _pathFindingGraph;
	// Pooled collections to prevent garbage allocation on every pathfinding request
	private readonly Dictionary<BoundingBox, MacroPathFindingNode> _usedNodes;
	private readonly HashSet<BoundingBox> _closedSet;
	private readonly PriorityQueueSimple<MacroPathFindingNode, float> _openSet;

	public AStarMacro(PathfindingGraphManager macroGraph,
	int initialCapacity = DEFAULT_INITIAL_CAPACITY, int maxIterations = MAX_ITERATIONS) {

		this._pathFindingGraph = macroGraph;
		this._usedNodes = new Dictionary<BoundingBox, MacroPathFindingNode>(initialCapacity);
		this._closedSet = new HashSet<BoundingBox>(initialCapacity);
		this._openSet = new PriorityQueueSimple<MacroPathFindingNode, float>(initialCapacity);
		this.DefaultMAXIterations = maxIterations;
	}

	#region Pathfinding Methods
	public List<BoundingBox> FindPath(Vec2Int start, Vec2Int end, MovementCapability entityMovementCapability) {
		if (!_pathFindingGraph.TryGetMacroNodeFromPosition(start, out MacroGridNode startMacroNode)) {
			Debug.LogWarning($"Start position {start} does not correspond to a valid macro node.");
			return null;
		}
		if (!_pathFindingGraph.TryGetMacroNodeFromPosition(end, out MacroGridNode endMacroNode)) {
			Debug.LogWarning($"End position {end} does not correspond to a valid macro node.");
			return null;
		}

		this._usedNodes.Clear();
		this._closedSet.Clear();
		this._openSet.Clear();


		float initialHCost = BoundingBox.ManhattanDistanceTo(
			startMacroNode.Bound.Center, endMacroNode.Bound.Center
		);

		MacroPathFindingNode startNode = new(startMacroNode, 0f, initialHCost, null);


		this._openSet.EnqueueOrUpdate(startNode);
		this._usedNodes[startMacroNode.Bound] = startNode;


		int iterations = 0;
		while (this._openSet.Count > 0 && iterations < this.DefaultMAXIterations) {
			MacroPathFindingNode current = this._openSet.Dequeue();
			iterations++;

			// Target reached!
			if (current.Node == endMacroNode) {
				return ReconstructPath(current, this._usedNodes);
			}

			this._closedSet.Add(current.Node.Bound);

			// // Fetch neighbors 
			// if (this._pathFindingGraph.TryGetConnections(current.Node.Bound, out IReadOnlyList<MacroConnectionData> connections)) {

			// 	foreach (MacroConnectionData connection in connections) {
			// 		BoundingBox neighborBounds = connection.ToBound;

			// 		if (this._closedSet.Contains(neighborBounds)) {
			// 			continue;
			// 		}

			// 		float tentativeGCost = current.GCost + connection.Cost;

			// 		// If it's an undiscovered node OR we found a faster route to a discovered node
			// 		if (!this._usedNodes.TryGetValue(neighborBounds, out MacroPathFindingNode neighborNodeRecord) ||
			// 			tentativeGCost < neighborNodeRecord.GCost) {

			// 			if (this._pathFindingGraph.TryGetMacroNode(neighborBounds, out MacroGridNode neighborGridNode)) {

			// 				float hCost = BoundingBox.ManhattanDistanceTo(
			// 					neighborGridNode.Bound.Center, endMacroNode.Bound.Center);

			// 				MacroPathFindingNode newNeighborRecord = new(
			// 					neighborGridNode,
			// 					tentativeGCost,
			// 					hCost,
			// 					current.Node.Bound
			// 				);

			// 				this._usedNodes[neighborBounds] = newNeighborRecord;

			// 				// MAGIC HAPPENS HERE:
			// 				// If the node is new, it enqueues. 
			// 				// If it's already in the OpenSet, it finds it via Dictionary mapping 
			// 				// and bubbles it up the heap to its new priority!
			// 				this._openSet.EnqueueOrUpdate(newNeighborRecord);
			// 			}
			// 		}
			// 	}
			// }
		}

		// Open set is empty and destination was never reached
		return null;
	}
	#endregion

	#region Utility Methods
	public List<BoundingBox> ReconstructPath(MacroPathFindingNode current, Dictionary<BoundingBox, MacroPathFindingNode> usedNodes) {
		List<BoundingBox> totalPath = new() { current.Node.Bound };
		while (current.Parent.HasValue) {
			BoundingBox parentBounds = current.Parent.Value;
			totalPath.Add(parentBounds);

			if (!usedNodes.TryGetValue(parentBounds, out current)) {
				Debug.LogWarning($"Parent node {parentBounds} not found in used nodes. Path reconstruction may be incomplete.");
				break;
			}
		}
		totalPath.Reverse();
		return totalPath;
	}
	#endregion
}