// AStarMacro.cs
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;
using ZLinq;

public class AStarMacro {
	private static readonly List<BoundingBox> EMPTY_PATH = new();
	public const float MAX_ITERATIONS_RATIO = 1f;
	public const int DEFAULT_INITIAL_CAPACITY = 32;

	private readonly float _maxIterationsRatio;
	private int _maxIterations = 10;

	private readonly PathfindingGraphManager _graphManager;

	private readonly Dictionary<BoundingBox, MacroPathFindingNode> _nodeRecords;
	private readonly HashSet<BoundingBox> _closedSet;
	private readonly QuadPriorityQueue<MacroPathFindingNode, float> _openSet;

#if UNITY_EDITOR
	// Only exists to feed MacroPathfindingRecorder — stripped from player builds entirely
	// along with everything else below that touches `recorder`.
	private readonly List<BoundingBox> _recorderOpenListCache = new(DEFAULT_INITIAL_CAPACITY);
#endif

	public AStarMacro(
		PathfindingGraphManager macroGraph,
		int initialCapacity = DEFAULT_INITIAL_CAPACITY,
		float maxIterations = MAX_ITERATIONS_RATIO) {
		this._graphManager = macroGraph;
		this._nodeRecords = new Dictionary<BoundingBox, MacroPathFindingNode>(initialCapacity);
		this._closedSet = new HashSet<BoundingBox>(initialCapacity);
		this._openSet = new QuadPriorityQueue<MacroPathFindingNode, float>(initialCapacity);

		this._maxIterationsRatio = Mathf.Clamp(maxIterations, 0.1f, 1f);
	}

	#region Pathfinding Methods
	/// <summary>
	/// Runs macro-level A* pathfinding.
	/// </summary>
	/// <param name="recorder">
	/// Editor-only visualization hook for scrubbing the search's open/closed sets step-by-step.
	/// Pass <c>null</c> (or compile for a player build) to skip recording entirely — every line that
	/// touches this parameter lives behind <c>#if UNITY_EDITOR</c>, so it costs nothing outside the
	/// editor regardless of what's passed in. The final path is NOT recorder-dependent — it's always
	/// returned via the result's Path, regardless of whether a recorder was passed in.
	/// </param>
	public MacroPathFindingResult FindPath(
		Vec2Int start,
		Vec2Int end,
		MovementCapability entityMovementCapability,
		MacroPathfindingRecorder recorder = null) {
		if (!this._graphManager.TryGetMacroNodeFromPosition(start, out MacroGridNode startMacroNode)) {
			Debug.LogWarning($"Start position {start} does not correspond to a valid macro node.");
			return new MacroPathFindingResult { Path = EMPTY_PATH, TotalNodeSearches = 0, TotalNodeEvaluations = 0 };
		}

		if (!this._graphManager.TryGetMacroNodeFromPosition(end, out MacroGridNode endMacroNode)) {
			Debug.LogWarning($"End position {end} does not correspond to a valid macro node.");
			return new MacroPathFindingResult { Path = EMPTY_PATH, TotalNodeSearches = 0, TotalNodeEvaluations = 0 };
		}

		this._nodeRecords.Clear();
		this._closedSet.Clear();
		this._openSet.Clear();

#if UNITY_EDITOR
		recorder?.Clear();
#endif

		this._maxIterations = Mathf.CeilToInt(this._maxIterationsRatio * this._graphManager.MacroNodeCount);

		float initialHCost = MacroGridNode.GetTraversalCost(endMacroNode.Bound, startMacroNode.Bound);
		MacroPathFindingNode startNode = new(startMacroNode, 0f, initialHCost, null);

		this._openSet.EnqueueOrUpdate(startNode);
		this._nodeRecords[startMacroNode.Bound] = startNode;

		int iterations = 0;
		int totalNodeSearches = 0;

		while (this._openSet.Count > 0 && iterations < this._maxIterations) {
			MacroPathFindingNode currentRecord = this._openSet.Dequeue();
			iterations++;

#if UNITY_EDITOR
			if (recorder != null) {
				this._recorderOpenListCache.Clear();
				foreach (var node in this._openSet.GetElements()) {
					this._recorderOpenListCache.Add(node.Node.Bound);
				}
				recorder.RecordStep(currentRecord.Node.Bound, this._recorderOpenListCache, this._closedSet);
			}
#endif

			// compare with the bounds of the end macro node instead of referencing the node object 
			// directly, to avoid potential null reference issues if the node is not found in the graph.
			if (currentRecord.Node.Bound == endMacroNode.Bound) {
				List<BoundingBox> path = ReconstructPath(currentRecord);
				return new MacroPathFindingResult { Path = path, TotalNodeSearches = totalNodeSearches, TotalNodeEvaluations = 0 };
			}

			this._closedSet.Add(currentRecord.Node.Bound);

			// Fetch neighbors
			if (this._graphManager.GetNeighboringMacroNodesConnectionData(
				currentRecord.Node.Bound, entityMovementCapability, out IEnumerable<MacroConnectionData> connections)) {
				foreach (MacroConnectionData connection in connections) {
					BoundingBox neighborBounds = connection.ToBound;

					if (this._closedSet.Contains(neighborBounds)) {
						continue;
					}

					float tentativeGCost = currentRecord.GCost + MacroGridNode.GetTraversalCost(currentRecord.Node.Bound, neighborBounds);

					if (!this._nodeRecords.TryGetValue(neighborBounds, out MacroPathFindingNode neighborNodeRecord)
					|| tentativeGCost < neighborNodeRecord.GCost) {
						if (this._graphManager.TryGetMacroNode(neighborBounds, out MacroGridNode neighborGridNode)) {
							float hCost = MacroGridNode.GetTraversalCost(endMacroNode.Bound, neighborGridNode.Bound);
							MacroPathFindingNode newNeighborRecord = new(
								neighborGridNode,
								tentativeGCost,
								hCost,
								currentRecord.Node.Bound
							);

							this._nodeRecords[neighborBounds] = newNeighborRecord;

							this._openSet.EnqueueOrUpdate(newNeighborRecord);
						}
					}
					// should also count the failed neighbor searches, since they still 
					// represent a node evaluation attempt
					totalNodeSearches++;
				}
			}
		}
		return new MacroPathFindingResult { Path = EMPTY_PATH, TotalNodeSearches = totalNodeSearches, TotalNodeEvaluations = 0 };
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