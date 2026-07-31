using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;
namespace Kope.Feature.PathFinding.Algorithms {
	/// <summary>
	/// Performs high-level macro pathfinding across interconnected spatial bounding boxes using Weighted A*.
	/// </summary>
	/// <remarks>
	/// Operates on a macro-graph (bounding box nodes) to find coarse travel corridors 
	/// before tile-level micro pathfinding takes over.
	/// 
	/// Supports adjustable greediness (<c>_greedyNess</c>) implementing <b>Weighted A*</b> (<c>F = G + w * H</c>). 
	/// </remarks>
	public class AStarMacro {
		#region Constants & Fields

		private static readonly List<BoundingBox> EMPTY_PATH = new();


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
		{ CostCalculationType.Manhattan, MacroGridNode.ManHattenCost },
		{ CostCalculationType.Euclidean, MacroGridNode.EuclideanCost },
		{ CostCalculationType.Octile, MacroGridNode.OctileCost }
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

		// Search Execution Cache
		private MacroGridNode _startMacroNodeCache;
		private MacroGridNode _endMacroNodeCache;
		private int _totalNodesCache;

#if UNITY_EDITOR
		/// <summary>
		/// Reusable buffer for editor visualization tools. Stripped out completely in standalone builds.
		/// </summary>
		private readonly List<BoundingBox> _recorderOpenListCache = new(PathFindingConfig.DEFAULT_INITIAL_CAPACITY);
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
			PathFindingConfig config = default) {

			this._graphManager = macroGraph ?? throw new ArgumentNullException(nameof(macroGraph));
			this._nodeRecords = new Dictionary<BoundingBox, MacroPathFindingNode>(config.InitialCapacity);
			this._closedSet = new HashSet<BoundingBox>(config.InitialCapacity);
			this._openSet = new QuadPriorityQueue<MacroPathFindingNode, int>(config.InitialCapacity);


			this._greedyNess = config.Greediness;
			this._costCalculationType = config.CostCalculationType;
			this._maxIterationsRatio = config.MaxIterationRatio;
		}

		#endregion

		#region Pathfinding Methods
		/// <summary>
		/// Validates start and target coordinates against the macro graph structure prior to pathfinding execution.
		/// </summary>
		public bool PreCheck(Vec2Int start, Vec2Int end, MovementCapability entityMovementCapability, out MacroPathFindingResult preCheckResult) {
			this._startMacroNodeCache = default;
			this._endMacroNodeCache = default;
			this._totalNodesCache = this._graphManager.MacroNodeCount;

			bool TryValidateNode(Vec2Int pos, string pointLabel, out MacroGridNode nodeCache) {
				if (!this._graphManager.TryGetMacroNodeFromPosition(pos, out nodeCache)) {
					Debug.LogWarning($"[{pointLabel}] Position {pos} does not correspond to a valid macro node.");
					return false;
				}

				if (!nodeCache.CanTraverse(entityMovementCapability)) {
					Debug.LogWarning($"[{pointLabel}] Node at {pos} is not traversable for MovementCapability" +
					$" '{entityMovementCapability}' (IsNarrativelyAccessible: {nodeCache.IsNarrativelyAccessible}).");
					return false;
				}
				return true;
			}

			if (!TryValidateNode(start, "Start", out this._startMacroNodeCache) ||
				!TryValidateNode(end, "End", out this._endMacroNodeCache)) {
				preCheckResult = CreateFailureResult(PathFindingResultType.InvalidStartOrEnd, 0, 0);
				return false;
			}

			preCheckResult = default;
			return true;
		}
		/// <summary>
		/// Executes a Weighted A* search across macro bounding boxes using cached start/end node data from PreCheck.
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
			// if (!PreCheck(start, end, entityMovementCapability, out MacroPathFindingResult preCheckResult)) {
			// 	return preCheckResult;
			// }

			// Clear internal collections for a zero-allocation search iteration
			this._nodeRecords.Clear();
			this._closedSet.Clear();
			this._openSet.Clear();

#if UNITY_EDITOR
			recorder?.Clear();
#endif

			this._maxIterations = Mathf.CeilToInt(this._maxIterationsRatio * this._totalNodesCache);
			Func<BoundingBox, BoundingBox, int> costCalculator = this._costCalculators[this._costCalculationType];

			BoundingBox startBound = this._startMacroNodeCache.Bound;
			BoundingBox endBound = this._endMacroNodeCache.Bound;


			// Initialize start node with weighted H-cost (Weighted A*: F = G + w * H)
			int rawInitialH = costCalculator(endBound, startBound);
			int weightedInitialH = Mathf.FloorToInt(rawInitialH * this._greedyNess);

			MacroPathFindingNode startNode = new(startBound, gCost: 0, hCost: weightedInitialH, parent: null);

			this._openSet.EnqueueOrUpdate(startNode);
			this._nodeRecords[startBound] = startNode;

			int totalExpansion = 0;
			int totalNodeEvaluation = 0;

			// Main Search Loop
			while (this._openSet.Count > 0 && totalExpansion < this._maxIterations) {
				MacroPathFindingNode currentRecord = this._openSet.Dequeue();
				totalExpansion++;

#if UNITY_EDITOR
				if (recorder != null) {
					this._recorderOpenListCache.Clear();
					foreach (var node in this._openSet.GetElements()) {
						this._recorderOpenListCache.Add(node.NodeBox);
					}
					recorder.RecordStep(currentRecord.NodeBox, this._recorderOpenListCache, this._closedSet);
				}
#endif

				// Target condition: reached destination macro region
				if (currentRecord.NodeBox == endBound) {
					List<BoundingBox> path = ReconstructPath(currentRecord);
					return new MacroPathFindingResult(
						PathFindingResultType.Success,
						path, this._totalNodesCache, totalNodeEvaluation,
						totalExpansion, this._costCalculationType, this._greedyNess);
				}

				this._closedSet.Add(currentRecord.NodeBox);

				// Fetch adjacent macro bounds matching the entity's capabilities
				if (this._graphManager.GetNeighboringMacroNodesConnectionData(
					currentRecord.NodeBox, entityMovementCapability, out IEnumerable<MacroConnectionData> connections)) {

					foreach (MacroConnectionData connection in connections) {
						BoundingBox neighborBounds = connection.ToBound;

						if (this._closedSet.Contains(neighborBounds)) {
							continue;
						}

						int stepCost = costCalculator(currentRecord.NodeBox, neighborBounds);
						int tentativeGCost = currentRecord.GCost + stepCost;

						// If neighbor is unvisited OR a cheaper path to it was discovered
						if (!this._nodeRecords.TryGetValue(neighborBounds, out MacroPathFindingNode neighborNodeRecord)
							|| tentativeGCost < neighborNodeRecord.GCost) {

							if (this._graphManager.TryGetMacroNode(neighborBounds, out MacroGridNode _)) {
								int rawHCost = costCalculator(endBound, neighborBounds);
								int weightedHCost = Mathf.FloorToInt(rawHCost * this._greedyNess);

								MacroPathFindingNode newNeighborRecord = new(
									neighborBounds,
									tentativeGCost,
									weightedHCost,
									currentRecord.NodeBox
								);

								this._nodeRecords[neighborBounds] = newNeighborRecord;
								this._openSet.EnqueueOrUpdate(newNeighborRecord);
							}
						}

						totalNodeEvaluation++;
					}
				}
			}

			// Search exhausted or reached max iterations without finding destination
			return CreateFailureResult(PathFindingResultType.NoPathFound, totalNodeEvaluation, totalExpansion);
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// Backtracks through parent node references from the goal node back to the start node.
		/// </summary>
		public List<BoundingBox> ReconstructPath(MacroPathFindingNode current) {
			List<BoundingBox> totalPath = new() { current.NodeBox };

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


		private MacroPathFindingResult CreateFailureResult(PathFindingResultType resultType, int totalNodeEvaluations, int totalNodeExpansions) {
			return new MacroPathFindingResult(
				resultType,
				EMPTY_PATH,
				this._totalNodesCache,
				totalNodeEvaluations,
				totalNodeExpansions,
				this._costCalculationType,
				this._greedyNess
			);
		}

		#endregion
	}
}