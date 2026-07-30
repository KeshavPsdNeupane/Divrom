using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;

public class AStarMicro {
	private static readonly List<Vec2Int> EMPTY_PATH = new();
	private static readonly (int, int)[] _neighborOffset = new[]{
		(0, 1),   // Up
		(1, 0),   // Right
		(0, -1),  // Down
		(-1, 0),  // Left
		(1, 1),   // Up-Right
		(1, -1),  // Down-Right
		(-1, -1), // Down-Left
		(-1, 1)   // Up-Left
	};
	private static readonly Dictionary<(int, int), ((int, int), (int, int))> _nebouringRuleMap = new(){
		{ (1, 1), ((1, 0), (0, 1)) }, // Up-Right rule: both right and up must be walkable
		{ (1, -1), ((1, 0), (0, -1)) }, // Down-Right: both right and down must be walkable
		{ (-1, -1), ((-1, 0), (0, -1)) }, // Down-Left : both left and down must be walkable
		{ (-1, 1), ((-1, 0), (0, 1)) } // Up-Left : both left and up must be walkable
	};
	private readonly Dictionary<CostCalculationType, Func<Vec2Int, Vec2Int, int>> _costCalculators = new() {
		{ CostCalculationType.Manhattan, MicroGridNode.ManhattanCost },
		{ CostCalculationType.Euclidean, MicroGridNode.EuclideanCost },
		{ CostCalculationType.Octile, MicroGridNode.OctileCost }
	};

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

	private readonly CostCalculationType _costCalculationType = CostCalculationType.Manhattan;

	/// <summary>
	/// Reference to the centralized graph manager containing macro node bounds and topological connections.
	/// </summary>
	private readonly PathfindingGraphManager _graphManager;

	/// <summary>
	/// Tracks evaluated path records mapped by bounding box key to support fast lookup and parent tracing.
	/// </summary>
	private readonly Dictionary<Vec2Int, MicroPathFindingNode> _nodeRecords;

	/// <summary>
	/// Closed set tracking bounding boxes whose shortest path has already been finalized.
	/// </summary>
	private readonly HashSet<Vec2Int> _closedSet;

	/// <summary>
	/// Open set priority queue (min-heap variant) holding frontier nodes sorted by total estimated cost (F = G + w * H).
	/// </summary>
	private readonly QuadPriorityQueue<MicroPathFindingNode, int> _openSet;


	//caching
	private MicroGridNode _startMicroNodeCache;
	private MicroGridNode _endMicroNodeCache;
	private int _totalNodesCache;

	public AStarMicro(PathfindingGraphManager graphManager,
	CostCalculationType ca = CostCalculationType.Manhattan,
		float maxIterationsRatio = MAX_ITERATIONS_RATIO, float greedyNess = 1f,
	 int initialCapacity = DEFAULT_INITIAL_CAPACITY) {
		this._graphManager = graphManager ?? throw new ArgumentNullException(nameof(graphManager));
		this._costCalculationType = ca;
		this._maxIterationsRatio = Math.Max(0f, maxIterationsRatio);
		this._greedyNess = Math.Clamp(greedyNess, 1f, MAX_GREEDINESS);
		this._nodeRecords = new Dictionary<Vec2Int, MicroPathFindingNode>(initialCapacity);
		this._closedSet = new HashSet<Vec2Int>(initialCapacity);
		this._openSet = new QuadPriorityQueue<MicroPathFindingNode, int>(initialCapacity);
	}

	public bool PreCheck(Vec2Int start, Vec2Int end, MovementCapability _, out MicroPathFindingResult preCheckResult) {
		this._startMicroNodeCache = default;
		this._endMicroNodeCache = default;
		this._totalNodesCache = this._graphManager.MicroNodeCount;

		if (!this._graphManager.TryGetMicroNode(start, out var startMicroNode)) {
			this._startMicroNodeCache = startMicroNode;
			if (startMicroNode.IsStaticObstacle) {
				Debug.LogWarning($"Start position {start} is a static obstacle and cannot be used as a pathfinding node.");

			} else {
				Debug.LogWarning($"Start position {start} is not a valid micro node in the graph.");
			}
			preCheckResult = CreateFailureResult(0, 0);
			return false;
		}
		if (!this._graphManager.TryGetMicroNode(end, out var endMicroNode)) {
			if (endMicroNode.IsStaticObstacle) {
				Debug.LogWarning($"End position {end} is a static obstacle and cannot be used as a pathfinding node.");
			} else {
				Debug.LogWarning($"End position {end} is not a valid micro node in the graph.");
			}
			preCheckResult = CreateFailureResult(0, 0);
			return false;
		}
		preCheckResult = default;
		return true;
	}



	/// <summary>
	/// Finds a path from the specified start position to the end position, considering the entity's movement capabilities.
	/// </summary>
	/// <param name="start">The starting position.</param>
	/// <param name="end">The destination position.</param>
	/// <param name="entityMovementCapability">The movement capabilities of the entity.</param>
	/// <returns>The result of the pathfinding operation.</returns>
	public MicroPathFindingResult FindPath(
			Vec2Int start,
			Vec2Int end,
			MovementCapability _) {


		//clear the previous state
		this._nodeRecords.Clear();
		this._closedSet.Clear();
		this._openSet.Clear();

		this._maxIterations = Mathf.CeilToInt(this._maxIterationsRatio * this._graphManager.MacroNodeCount);
		Func<Vec2Int, Vec2Int, int> costCalculator = this._costCalculators[this._costCalculationType];

		int rawInitialH = costCalculator(start, end);
		int weightedInitialH = Mathf.FloorToInt(rawInitialH * this._greedyNess);


		MicroPathFindingNode startNode = new(start, 0, weightedInitialH, null);

		this._openSet.EnqueueOrUpdate(startNode);
		this._nodeRecords[this._startMicroNodeCache.Position] = startNode;

		int totalExpansion = 0;
		int totalNodeEvaluation = 0;

		while (this._openSet.Count > 0 && totalNodeEvaluation < this._maxIterations) {
			totalNodeEvaluation++;
			MicroPathFindingNode currentRecord = this._openSet.Dequeue();

			if (currentRecord.NodePosition == this._endMicroNodeCache.Position) {
				List<Vec2Int> path = ReconstructPath(currentRecord);
				return new MicroPathFindingResult {
					Path = path,
					TotalNodes = this._totalNodesCache,
					TotalNodeEvaluations = totalNodeEvaluation,
					TotalNodeExpansions = totalExpansion,
					Success = true,
					CostCalculationType = this._costCalculationType,
					Greediness = this._greedyNess,
				};
			}

			this._closedSet.Add(currentRecord.NodePosition);
			totalExpansion++;

			foreach (var offset in _neighborOffset) {
				Vec2Int neighborPos = currentRecord.NodePosition + new Vec2Int(offset.Item1, offset.Item2);

				if (this._closedSet.Contains(neighborPos)) {
					continue; // if contain skip the bastard.
				}


				if (!this._graphManager.TryGetMicroNode(neighborPos, out var neighborNode)) {
					continue; // Skip if neighbor is not a valid micro node
				}

				if (neighborNode.IsStaticObstacle) {
					continue; // Skip if entity cannot traverse to the neighbor
				}

				bool ruleCheck = _nebouringRuleMap.TryGetValue((offset.Item1, offset.Item2),
				 out var requiredOffsets);
				// if there is a rule for this neighbor, check if the required positions are walkable
				if (ruleCheck) {
					Vec2Int requiredPos1 = currentRecord.NodePosition +
					new Vec2Int(requiredOffsets.Item1.Item1, requiredOffsets.Item1.Item2);
					Vec2Int requiredPos2 = currentRecord.NodePosition +
					new Vec2Int(requiredOffsets.Item2.Item1, requiredOffsets.Item2.Item2);

					if (!this._graphManager.TryGetMicroNode(requiredPos1, out var requiredNode1)
					|| requiredNode1.IsStaticObstacle ||
						!this._graphManager.TryGetMicroNode(requiredPos2, out var requiredNode2)
						|| requiredNode2.IsStaticObstacle) {
						continue;
					}
				}


				int stepCost = costCalculator(currentRecord.NodePosition, neighborPos);
				int tentativeGCost = currentRecord.GCost + stepCost;



				// the neighbor is either not in the records or we found a better path to it
				if (this._nodeRecords.TryGetValue(neighborPos, out var existingNeighborRecord)
				|| tentativeGCost < existingNeighborRecord.GCost) {
					int rawHCost = costCalculator(neighborPos, end);
					int weightedHCost = Mathf.FloorToInt(rawHCost * this._greedyNess);

					MicroPathFindingNode neighborRecord = new(
						neighborPos,
						tentativeGCost,
						weightedHCost,
						currentRecord.NodePosition
					);

					this._nodeRecords[neighborPos] = neighborRecord;
					this._openSet.EnqueueOrUpdate(neighborRecord);

				}
				totalNodeEvaluation++;
			}
		}

		return CreateFailureResult(totalNodeEvaluation, totalExpansion);
	}
	private List<Vec2Int> ReconstructPath(MicroPathFindingNode current) {
		List<Vec2Int> totalPath = new() { current.NodePosition };

		while (current.Parent.HasValue) {
			Vec2Int parentPos = current.Parent.Value;
			totalPath.Add(parentPos);
			current = this._nodeRecords[parentPos];
		}

		totalPath.Reverse();
		return totalPath;
	}

	private MicroPathFindingResult CreateFailureResult(int totalNodeEvaluations, int totalNodeExpansions) {
		return new MicroPathFindingResult {
			Path = EMPTY_PATH,
			TotalNodes = this._totalNodesCache,
			TotalNodeEvaluations = totalNodeEvaluations,
			TotalNodeExpansions = totalNodeExpansions,
			Success = false,
			CostCalculationType = this._costCalculationType,
			Greediness = this._greedyNess,
		};
	}
}

