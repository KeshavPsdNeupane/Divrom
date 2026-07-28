using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;

/// <summary>
/// Performs high-level macro pathfinding across interconnected spatial bounding boxes using Weighted A*.
/// </summary>
/// <remarks>
/// This pathfinder operates on a macro-graph (bounding box nodes) to find coarse travel corridors 
/// before local steering or tile-level pathfinding takes over. 
/// 
/// Supports adjustable greediness (<c>_greedyNess</c>) implementing <b>Weighted A*</b> (<c>F = G + w * H</c>). 
/// Inflating the heuristic ($H$) speeds up pathfinding by prioritizing nodes closer to the goal, trading a 
/// small fraction of path optimality for significantly fewer node expansions.
/// </remarks>
public class AStarMacro {
	#region Constants & Fields
	private static readonly List<BoundingBox> EMPTY_PATH = new();
	/// <summary>
	/// Default cap ratio on max search iterations relative to total nodes in the macro graph.
	/// </summary>
	public const float MAX_ITERATIONS_RATIO = 1f;

	/// <summary>
	/// Default allocation capacity for internal collections to prevent runtime garbage collection (GC) re-allocations.
	/// </summary>
	public const int DEFAULT_INITIAL_CAPACITY = 32;

	/// <summary>
	/// Maximum allowed heuristic weighting factor (w) for Weighted A*.
	/// </summary>
	public const float MAX_GREEDINESS = 1.5f;

	/// <summary>
	/// Ratio used to calculate maximum allowed iterations before aborting search: <c>_maxIterationsRatio * MacroNodeCount</c>.
	/// </summary>
	private readonly float _maxIterationsRatio;

	/// <summary>
	/// Evaluated at runtime based on <see cref="_maxIterationsRatio"/> and total macro nodes in the graph.
	/// </summary>
	private int _maxIterations = 10;

	/// <summary>
	/// The heuristic multiplier $w \ge 1.0$. 
	/// Higher values accelerate search towards goal at the potential expense of absolute path length.
	/// </summary>
	private readonly float _greedyNess = 1f;

	/// <summary>
	/// Reference to the centralized graph manager containing macro node bounds and topological connections.
	/// </summary>
	private readonly PathfindingGraphManager _graphManager;

	/// <summary>
	/// Active distance metric chosen for traversal and heuristic calculations.
	/// </summary>
	private readonly CostCalculationType _costCalculationType;

	/// <summary>
	/// Lookup table pairing distance metric types with their static cost calculation functions.
	/// </summary>
	private readonly Dictionary<CostCalculationType, Func<BoundingBox, BoundingBox, int>> _costCalculators = new() {
		{ CostCalculationType.Manhattan, MacroGridNode.GetManHattenTraversalCost },
		{ CostCalculationType.Euclidean, MacroGridNode.GetTraversalCostEuclidean },
		{ CostCalculationType.Octile, MacroGridNode.GetTraversalCostOctile }
	};

	/// <summary>
	/// Tracks evaluated path records mapped by bounding box key to support fast lookup and parent tracing.
	/// </summary>
	private readonly Dictionary<BoundingBox, MacroPathFindingNode> _nodeRecords;

	/// <summary>
	/// Closed set tracking bounding boxes whose shortest path has already been finalized.
	/// </summary>
	private readonly HashSet<BoundingBox> _closedSet;

	/// <summary>
	/// Open set priority queue (min-heap variant) holding frontier nodes sorted by total estimated cost (F = G + w * H).
	/// </summary>
	private readonly QuadPriorityQueue<MacroPathFindingNode, int> _openSet;

#if UNITY_EDITOR
	/// <summary>
	/// Reusable buffer for editor visualization tools. Stripped out completely in standalone builds.
	/// </summary>
	private readonly List<BoundingBox> _recorderOpenListCache = new(DEFAULT_INITIAL_CAPACITY);
#endif

	#endregion

	#region Constructor

	/// <summary>
	/// Initializes a reusable instance of the macro A* pathfinder.
	/// </summary>
	/// <param name="macroGraph">Graph manager holding node layouts and adjacency data.</param>
	/// <param name="greedyNess">Heuristic multiplier $w \ge 1.0$. Values $> 1.0$ enable Weighted A*.</param>
	/// <param name="costCalculationType">Heuristic metric to evaluate node-to-node distance (Manhattan, Euclidean, or Octile).</param>
	/// <param name="initialCapacity">Pre-allocation capacity for internal dictionaries and sets to minimize GC allocations.</param>
	/// <param name="maxIterations">Percentage ratio (0.1 to 1.0) of max graph nodes to evaluate before timing out.</param>
	public AStarMacro(
		PathfindingGraphManager macroGraph,
		float greedyNess = 1f,
		CostCalculationType costCalculationType = CostCalculationType.Manhattan,
		int initialCapacity = DEFAULT_INITIAL_CAPACITY,
		float maxIterations = MAX_ITERATIONS_RATIO) {

		this._graphManager = macroGraph;
		this._greedyNess = Mathf.Clamp(greedyNess, 1f, MAX_GREEDINESS);
		this._nodeRecords = new Dictionary<BoundingBox, MacroPathFindingNode>(initialCapacity);
		this._closedSet = new HashSet<BoundingBox>(initialCapacity);
		this._openSet = new QuadPriorityQueue<MacroPathFindingNode, int>(initialCapacity);
		this._costCalculationType = costCalculationType;
		this._maxIterationsRatio = Mathf.Clamp(maxIterations, 0.1f, 1f);
	}

	#endregion

	#region Pathfinding Methods

	/// <summary>
	/// Executes a Weighted A* search from a start 2D coordinate to an end 2D coordinate across macro bounding boxes.
	/// </summary>
	/// <param name="start">World grid coordinate for path start.</param>
	/// <param name="end">World grid coordinate for path target.</param>
	/// <param name="entityMovementCapability">Capability flags filtering traversable connections (e.g., ground vs. flying).</param>
	/// <param name="recorder">Optional editor-only visualization recorder. Costs zero overhead in release builds.</param>
	/// <returns>A <see cref="MacroPathFindingResult"/> containing the path corridor and search performance statistics.</returns>
	public MacroPathFindingResult FindPath(
		Vec2Int start,
		Vec2Int end,
		MovementCapability entityMovementCapability,
		MacroPathfindingRecorder recorder = null) {

		int totalNode = this._graphManager.MacroNodeCount;

		// 1. Validate start macro node
		if (!this._graphManager.TryGetMacroNodeFromPosition(start, out MacroGridNode startMacroNode)) {
			Debug.LogWarning($"Start position {start} does not correspond to a valid macro node.");
			return new MacroPathFindingResult {
				Path = EMPTY_PATH,
				TotalNodes = totalNode,
				TotalNodeEvaluations = 0,
				TotalNodeExpansions = 0,
				CostCalculationType = this._costCalculationType,
				Greediness = this._greedyNess
			};
		}

		// 2. Validate target macro node
		if (!this._graphManager.TryGetMacroNodeFromPosition(end, out MacroGridNode endMacroNode)) {
			Debug.LogWarning($"End position {end} does not correspond to a valid macro node.");
			return new MacroPathFindingResult {
				Path = EMPTY_PATH,
				TotalNodes = totalNode,
				TotalNodeEvaluations = 0,
				TotalNodeExpansions = 0,
				CostCalculationType = this._costCalculationType,
				Greediness = this._greedyNess
			};
		}

		// 3. Reset internal structures for a zero-allocation search iteration
		this._nodeRecords.Clear();
		this._closedSet.Clear();
		this._openSet.Clear();

#if UNITY_EDITOR
		recorder?.Clear();
#endif

		this._maxIterations = Mathf.CeilToInt(this._maxIterationsRatio * this._graphManager.MacroNodeCount);
		Func<BoundingBox, BoundingBox, int> costCalculator = this._costCalculators[this._costCalculationType];

		// 4. Initialize start node with weighted H-cost (Weighted A*: F = G + w * H)
		int rawInitialH = costCalculator(endMacroNode.Bound, startMacroNode.Bound);
		int weightedInitialH = Mathf.FloorToInt(rawInitialH * this._greedyNess);

		MacroPathFindingNode startNode = new(startMacroNode, gCost: 0, hCost: weightedInitialH, parent: null);

		this._openSet.EnqueueOrUpdate(startNode);
		this._nodeRecords[startMacroNode.Bound] = startNode;

		int totalExpansion = 0;
		int totalNodeEvaluation = 0;

		// 5. Main Search Loop
		while (this._openSet.Count > 0 && totalExpansion < this._maxIterations) {
			MacroPathFindingNode currentRecord = this._openSet.Dequeue();
			totalExpansion++;

#if UNITY_EDITOR
			if (recorder != null) {
				this._recorderOpenListCache.Clear();
				foreach (var node in this._openSet.GetElements()) {
					this._recorderOpenListCache.Add(node.Node.Bound);
				}
				recorder.RecordStep(currentRecord.Node.Bound, this._recorderOpenListCache, this._closedSet);
			}
#endif

			// Target condition: compare bounds rather than node references to guarantee stability
			if (currentRecord.Node.Bound == endMacroNode.Bound) {
				List<BoundingBox> path = ReconstructPath(currentRecord);
				return new MacroPathFindingResult {
					Path = path,
					TotalNodes = totalNode,
					TotalNodeEvaluations = totalNodeEvaluation,
					TotalNodeExpansions = totalExpansion,
					CostCalculationType = this._costCalculationType,
					Greediness = this._greedyNess
				};
			}

			this._closedSet.Add(currentRecord.Node.Bound);

			// Fetch adjacent macro bounds matching the entity's capabilities
			if (this._graphManager.GetNeighboringMacroNodesConnectionData(
				currentRecord.Node.Bound, entityMovementCapability, out IEnumerable<MacroConnectionData> connections)) {

				foreach (MacroConnectionData connection in connections) {
					BoundingBox neighborBounds = connection.ToBound;

					if (this._closedSet.Contains(neighborBounds)) {
						continue;
					}

					// Pure G-cost: actual travel effort accumulated from start to this neighbor
					int stepCost = costCalculator(currentRecord.Node.Bound, neighborBounds);
					int tentativeGCost = currentRecord.GCost + stepCost;

					// If neighbor hasn't been visited OR a shorter path to it was discovered
					if (!this._nodeRecords.TryGetValue(neighborBounds, out MacroPathFindingNode neighborNodeRecord)
					|| tentativeGCost < neighborNodeRecord.GCost) {

						if (this._graphManager.TryGetMacroNode(neighborBounds, out MacroGridNode neighborGridNode)) {

							// Weighted H-cost: inflate estimated remaining distance to destination (w * H)
							int rawHCost = costCalculator(endMacroNode.Bound, neighborGridNode.Bound);
							int weightedHCost = Mathf.FloorToInt(rawHCost * this._greedyNess);

							MacroPathFindingNode newNeighborRecord = new(
								neighborGridNode,
								tentativeGCost,
								weightedHCost,
								currentRecord.Node.Bound
							);

							this._nodeRecords[neighborBounds] = newNeighborRecord;
							this._openSet.EnqueueOrUpdate(newNeighborRecord);
						}
					}

					// Track all connection checks for profiling node evaluations
					totalNodeEvaluation++;
				}
			}
		}

		// Exhausted open set or hit max iteration limit without reaching goal
		return new MacroPathFindingResult {
			Path = EMPTY_PATH,
			TotalNodes = totalNode,
			TotalNodeEvaluations = totalNodeEvaluation,
			TotalNodeExpansions = totalExpansion,
			CostCalculationType = this._costCalculationType,
			Greediness = this._greedyNess
		};
	}

	#endregion

	#region Utility Methods

	/// <summary>
	/// Backtracks through parent node references from the goal node back to the start node.
	/// </summary>
	/// <param name="current">The destination node record reached by the search.</param>
	/// <returns>A ordered sequence of bounding boxes representing the macro path corridor from start to end.</returns>
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