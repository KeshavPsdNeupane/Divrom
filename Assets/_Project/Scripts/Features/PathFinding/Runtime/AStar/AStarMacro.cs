using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;

public readonly struct MacroPathFindingNode : IHasCost<float>, IEquatable<MacroPathFindingNode> {
	public readonly MacroGridNode Node;
	public readonly float GCost;
	public readonly float HCost;
	public readonly BoundingBox? Parent;

	public float FCost {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => GCost + HCost;
	}

	public MacroPathFindingNode(MacroGridNode node, float gCost, float hCost, BoundingBox? parent) {
		Node = node;
		GCost = gCost;
		HCost = hCost;
		Parent = parent;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public float GetCost() => FCost;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(MacroPathFindingNode other) {
		// why only compare the Node? Because the other fields (GCost, HCost, Parent)
		// are not unique identifiers for the node. The Node itself is the unique 
		// identifier in this context.
		if (Node == null) return other.Node == null;
		return Node.Equals(other.Node);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj) => obj is MacroPathFindingNode otherNode && Equals(otherNode);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => Node != null ? Node.GetHashCode() : 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(MacroPathFindingNode left, MacroPathFindingNode right) => left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(MacroPathFindingNode left, MacroPathFindingNode right) => !left.Equals(right);

	public override string ToString() {
		string parentStr = Parent.HasValue ? Parent.Value.ToString() : "None";
		return $"MacroPathFindingNode(Node: {Node}, GCost: {GCost}, HCost: {HCost}, FCost: {FCost}, Parent: {parentStr})";
	}
}

public class AStarMacro {
	public const float MAX_ITERATIONS_RATIO = 1f;
	public const int DEFAULT_INITIAL_CAPACITY = 32;

	/// <summary>
	/// The maximum number of iterations the A* algorithm will perform
	/// before giving up on finding a path.
	/// This is a safeguard against infinite loops in cases where no path exists.
	/// For macro level pathfinding, this is usually sufficient, but can be adjusted based on
	/// the size of the macro graph.
	/// </summary>
	private readonly float _maxIterationsRatio;

	// default maximum iterations is set to 10, but can be adjusted 
	// based on the size of the macro graph.
	private int _maxIterations = 10;

	private readonly PathfindingGraphManager _graphManager;

	// Pooled collections to prevent garbage allocation on every pathfinding request
	private readonly Dictionary<BoundingBox, MacroPathFindingNode> _nodeRecords;
	private readonly HashSet<BoundingBox> _closedSet;
	private readonly QuadPriorityQueue<MacroPathFindingNode, float> _openSet;

	public AStarMacro(
		PathfindingGraphManager macroGraph,
		int initialCapacity = DEFAULT_INITIAL_CAPACITY,
		float maxIterations = MAX_ITERATIONS_RATIO) {
		this._graphManager = macroGraph;
		this._nodeRecords = new Dictionary<BoundingBox, MacroPathFindingNode>(initialCapacity);
		this._closedSet = new HashSet<BoundingBox>(initialCapacity);
		this._openSet = new QuadPriorityQueue<MacroPathFindingNode, float>(initialCapacity);

		//clamping to 0.1 to 1 becuase if ratio is 1, then it will treverse all nodes, and 
		// if it's 0.1, then it will traverse 10% of the nodes.
		// as default the MAX_ITERATIONS_RATIO is set to 1, but can be adjusted based 
		// on the size of the macro graph.
		this._maxIterationsRatio = Mathf.Clamp(maxIterations, 0.1f, 1f);
	}

	#region Pathfinding Methods
	public List<BoundingBox> FindPath(Vec2Int start, Vec2Int end, MovementCapability entityMovementCapability) {
		if (!this._graphManager.TryGetMacroNodeFromPosition(start, out MacroGridNode startMacroNode)) {
			Debug.LogWarning($"Start position {start} does not correspond to a valid macro node.");
			return null;
		}

		if (!this._graphManager.TryGetMacroNodeFromPosition(end, out MacroGridNode endMacroNode)) {
			Debug.LogWarning($"End position {end} does not correspond to a valid macro node.");
			return null;
		}

		this._nodeRecords.Clear();
		this._closedSet.Clear();
		this._openSet.Clear();

		// not fully caching the max iterations because the macro graph can 
		// change in size, so we need to recalculate it each time.
		// this is like 2 integer multiplications and a ceil, so it's not that expensive.
		// will be done in like 10,000th of a second, so it's not a big deal.
		this._maxIterations = Mathf.CeilToInt(this._maxIterationsRatio * this._graphManager.MacroNodeCount);

		float initialHCost = MacroGridNode.GetTraversalCost(endMacroNode.Bound, startMacroNode.Bound);

		MacroPathFindingNode startNode = new(startMacroNode, 0f, initialHCost, null);

		this._openSet.EnqueueOrUpdate(startNode);
		this._nodeRecords[startMacroNode.Bound] = startNode;

		int iterations = 0;
		while (this._openSet.Count > 0 && iterations < this._maxIterations) {
			MacroPathFindingNode currentRecord = this._openSet.Dequeue();
			iterations++;

			// Target reached!
			if (currentRecord.Node == endMacroNode) {
				return ReconstructPath(currentRecord);
			}

			this._closedSet.Add(currentRecord.Node.Bound);

			// Fetch neighbors 
			if (this._graphManager.GetNeighboringMacroNodesConnectionData(currentRecord.Node.Bound, entityMovementCapability, out IEnumerable<MacroConnectionData> connections)) {
				foreach (MacroConnectionData connection in connections) {
					BoundingBox neighborBounds = connection.ToBound;

					if (this._closedSet.Contains(neighborBounds)) {
						continue;
					}

					float tentativeGCost = currentRecord.GCost + MacroGridNode.GetTraversalCost(currentRecord.Node.Bound, neighborBounds);

					// If it's an undiscovered node OR we found a faster route to a discovered node
					if (!this._nodeRecords.TryGetValue(neighborBounds,
					out MacroPathFindingNode neighborNodeRecord) || tentativeGCost < neighborNodeRecord.GCost) {

						if (this._graphManager.TryGetMacroNode(neighborBounds, out MacroGridNode neighborGridNode)) {
							float hCost = MacroGridNode.GetTraversalCost(endMacroNode.Bound, neighborGridNode.Bound);

							MacroPathFindingNode newNeighborRecord = new(
								neighborGridNode,
								tentativeGCost,
								hCost,
								currentRecord.Node.Bound
							);

							this._nodeRecords[neighborBounds] = newNeighborRecord;

							// # Magic Happens Here
							// If the neighbor is already in the open set, it will update
							// its priority (FCost)
							// If it's not in the open set, it will be added.
							// Praise to our lord and savior, the QuadPriorityQueue.
							// for making this so simple and efficient.
							this._openSet.EnqueueOrUpdate(newNeighborRecord);
						}
					}
				}
			}
		}

		// Open set is empty and destination was never reached
		return null;
	}
	#endregion

	#region Utility Methods
	public List<BoundingBox> ReconstructPath(MacroPathFindingNode current) {
		List<BoundingBox> totalPath = new() { current.Node.Bound };

		while (current.Parent.HasValue) {
			BoundingBox parentBounds = current.Parent.Value;
			totalPath.Add(parentBounds);

			if (!this._nodeRecords.TryGetValue(parentBounds, out current)) {
				Debug.LogWarning($"Parent node {parentBounds} not found in used nodes. Path reconstruction may be incomplete.");
				break;
			}
		}

		totalPath.Reverse();
		return totalPath;
	}
	#endregion
}