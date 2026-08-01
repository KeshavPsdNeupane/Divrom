using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Graph;
using Kope.Feature.PathFindingNew.Utility;
using ThirdParty.PriorityQueeu;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.PathFinding {

	/// <summary>
	/// Implements high-performance, zero-allocation Weighted A* pathfinding.
	/// <para>
	/// Leverages a 4-ary implicit heap priority queue (<see cref="QuadPriorityQueue{TElement, TPriority}"/>),
	/// transient value-type node records (<see cref="PathFindingNode"/>), and stack/buffer-backed spatial neighbor lookups
	/// via <see cref="Graphmanager"/> to eliminate Garbage Collection overhead during search loops.
	/// </para>
	/// </summary>
	public class AStar {

		// NOTE: this table is exclusively for estimating H-cost (distance-to-goal). It must never be
		// used to price an actual G-cost edge step — see the rationale comment in the neighbor loop
		// below for why that distinction matters and previously wasn't respected.
		private static readonly Dictionary<CostCalculationType, Func<Vec2Int, Vec2Int, float, int>>
		HEURISTIC_FUNCTIONS = new() {
			{ CostCalculationType.Manhattan, GridNode.ManhattanDistanceTo },
			{ CostCalculationType.Euclidean, GridNode.EuclideanDistanceTo },
			{ CostCalculationType.Octile, GridNode.OctileDistanceTo }
		};

		public static readonly List<Vec2Int> EmptyPath = new(0);

		private readonly Graphmanager _graphManager;
		private PathFindingConfig _config;
		private int _maxIterations;

		private readonly Dictionary<Vec2Int, PathFindingNode> _nodeRecords;
		private readonly QuadPriorityQueue<PathFindingNode, int> _openSet;
		private readonly HashSet<Vec2Int> _closedSet;

		#region Buffers for Zero-Allocation Neighbor Expansion
		private readonly GridNode[] _fetchBuffer = new GridNode[8];
		private readonly GridNode[] _neighbourBuffer = new GridNode[8];
		#endregion

#if UNITY_EDITOR
		/// <summary>
		/// Reusable buffer for editor visualizer tools. Excluded in standalone builds.
		/// </summary>
		private readonly List<Vec2Int> _recorderOpenListCache;
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="AStar"/> pathfinder.
		/// </summary>
		/// <param name="graphManager">The graph topology provider.</param>
		/// <param name="config">Configuration settings controlling search limits, heuristic types, and greediness.</param>
		public AStar(Graphmanager graphManager, PathFindingConfig config) {
			this._graphManager = graphManager;
			this._config = config;

			int capacity = config.InitialCapacity;
			this._nodeRecords = new Dictionary<Vec2Int, PathFindingNode>(capacity);
			this._openSet = new QuadPriorityQueue<PathFindingNode, int>(capacity);
			this._closedSet = new HashSet<Vec2Int>(capacity);

			int totalNodes = this._graphManager.TotalNodeCount;
			this._maxIterations = Mathf.Max(
				PathFindingConfig.MIN_NODE_SEARCH,
				Mathf.CeilToInt(PathFindingConfig.MIN_NODE_SEARCH_RATIO * totalNodes),
				Mathf.CeilToInt(PathFindingConfig.MAX_NODE_SEARCH_RATIO * totalNodes)
			);

#if UNITY_EDITOR
			this._recorderOpenListCache = new List<Vec2Int>(capacity);
#endif
		}

		/// <summary>
		/// Finds the shortest walkable path between two points using Weighted A*.
		/// </summary>
		/// <param name="start">Starting grid coordinate.</param>
		/// <param name="end">Destination target grid coordinate.</param>
		/// <param name="path">Receives the reconstructed sequence of grid coordinates if a path is found.</param>
		/// <param name="recorder">Optional editor recorder for step-by-step pathfinding visualization.</param>
		/// <returns>A <see cref="PathFindingResult"/> containing status metadata and execution metrics.</returns>
		public PathFindingResult FindPath(
			Vec2Int start,
			Vec2Int end,
			MovementCapability movementCapability,
			PathfindingRecorder recorder = null) {

			if (!this._graphManager.TryGetNode(start, out _) || !this._graphManager.TryGetNode(end, out _)) {
				return CreateErrorResult(PathFindingResultType.InvalidStartOrEnd, EmptyPath);
			}

			// Reset query state
			this._nodeRecords.Clear();
			this._openSet.Clear();
			this._closedSet.Clear();

#if UNITY_EDITOR
			recorder?.Clear();
#endif

			CostCalculationType costType = this._config.CostCalculationType;
			float greediness = this._config.GreedyNess;
			Func<Vec2Int, Vec2Int, float, int> heuristicFunc = HEURISTIC_FUNCTIONS[costType];

			// Hoisted out of the neighbor loop: this was previously re-evaluated as a float
			// equality check on every single neighbor visit (plus once here for the start node).
			// It's config-constant for the whole search, so it's now resolved once per FindPath call.
			bool useGreediness = greediness != PathFindingConfig.DEFAULT_GREEDINESS;

			// Initialize start node
			int rawInitialH = heuristicFunc(start, end, 1.0f);
			int weightedInitialH = useGreediness
				? Mathf.FloorToInt(rawInitialH * greediness)
				: rawInitialH;

			PathFindingNode startNode = new(start.X, start.Y, 0, weightedInitialH, PathFindingNode.StartPosition);

			this._nodeRecords[start] = startNode;
			this._openSet.EnqueueOrUpdate(startNode, startNode.FCost);

			// totalExpansions gates the loop (actual nodes popped & expanded — this is what
			// _maxIterations was sized against, via the node-count ratios in the constructor).
			// totalEvaluations is a reporting-only metric: every node touched, i.e. the popped
			// node itself plus every neighbor considered off it. These used to be the same
			// variable, which meant the loop's exit condition was being driven by a count that
			// grew ~9x faster than actual expansions (once per neighbor, not once per pop) —
			// so the search could hit the iteration cap and bail out with NoPathFound well
			// before it had actually expanded anywhere near _maxIterations nodes. Splitting them
			// keeps the reported TotalNodeEvaluations metric identical to before, but lets the
			// search actually use its full expansion budget.
			int TotalNodeSearched = 0;
			int totalEvaluations = 0;

			while (this._openSet.Count > 0 && TotalNodeSearched < this._maxIterations) {
				PathFindingNode currentRecord = this._openSet.Dequeue();
				totalEvaluations++;

#if UNITY_EDITOR
				if (recorder != null) {
					this._recorderOpenListCache.Clear();
					foreach (PathFindingNode node in this._openSet.GetElements()) {
						this._recorderOpenListCache.Add(node.Position);
					}
					recorder.RecordStep(currentRecord.Position, this._recorderOpenListCache, this._closedSet);
				}
#endif

				// Destination Reached
				if (currentRecord.Position == end) {
					return new PathFindingResult(
						PathFindingResultType.Success,
						ReconstructPath(currentRecord),
						this._graphManager.TotalNodeCount,
						totalEvaluations,
						TotalNodeSearched,
						costType,
						greediness
					);
				}

				this._closedSet.Add(currentRecord.Position);
				TotalNodeSearched++;

				// Zero-allocation neighbor expansion via Span view. diagonalMask tells us, per
				// returned neighbor, whether the move is diagonal — needed below to price the
				// edge correctly instead of guessing from the selected heuristic type.
				ReadOnlySpan<GridNode> neighbors = this._graphManager.TryGetNeighbors(
					currentRecord.Position,
					this._fetchBuffer,
					this._neighbourBuffer,
					out byte diagonalMask
				);

				for (int i = 0; i < neighbors.Length; i++) {
					// ref readonly avoids copying the GridNode struct (7 fields) on every edge —
					// previously `neighbors[i]` was indexed twice (once into neighborNode, once
					// again for .Position), each a separate struct copy off the span.
					ref readonly GridNode neighborNode = ref neighbors[i];
					Vec2Int neighborPos = neighborNode.Position;

					if (this._closedSet.Contains(neighborPos)) {
						continue;
					}

					// Fused traversability + cost-multiplier lookup: replaces the old
					// IsTraversable(...) + GetCostMultiplier(...) pair, which each recomputed a
					// validModes & movementType-style intersection independently.
					if (!neighborNode.TryGetTraversalCost(movementCapability, out float costMultiplier)) {
						continue;
					}

					// G-COST FIX: step cost between two adjacent grid cells must reflect actual
					// move geometry (cardinal vs. diagonal), not the heuristic distance metric the
					// user picked for goal estimation. The previous code called `heuristicFunc`
					// (Manhattan/Euclidean/Octile — whichever CostCalculationType was configured)
					// to price this edge too, which meant the real traversal cost silently changed
					// with the heuristic setting. E.g. with Manhattan selected, a diagonal step
					// priced out to round(2 * mult) * 10 = 20 instead of the correct
					// GridNode.DIAGONAL_COST of 14 — diagonal movement became artificially
					// expensive purely as a side effect of an unrelated config value, and diagonal
					// vs. cardinal costs were inconsistent across heuristic types. Since adjacency
					// is always known statically here (diagonalMask), we go straight to the
					// grid's own DIRECT_COST/DIAGONAL_COST constants — correct regardless of
					// heuristic choice, and skips a redundant distance calculation (including a
					// sqrt for Euclidean/Octile) per edge.
					bool isDiagonal = (diagonalMask & (1 << i)) != 0;
					int baseStepCost = isDiagonal ? GridNode.DIAGONAL_COST : GridNode.DIRECT_COST;
					int stepCost = Mathf.RoundToInt(baseStepCost * costMultiplier);

					int tentativeGCost = currentRecord.GCost + stepCost;

					if (!this._nodeRecords.TryGetValue(neighborPos, out PathFindingNode existingRecord) ||
						tentativeGCost < existingRecord.GCost) {

						// heuristicFunc is now called exactly once per neighbor (H-cost estimate
						// only) instead of twice (previously also used for step cost above).
						int rawHCost = heuristicFunc(neighborPos, end, 1.0f);
						int weightedHCost = useGreediness
							? Mathf.FloorToInt(rawHCost * greediness)
							: rawHCost;

						PathFindingNode neighborRecord = new(
							neighborPos.X,
							neighborPos.Y,
							tentativeGCost,
							weightedHCost,
							currentRecord.Position
						);

						this._nodeRecords[neighborPos] = neighborRecord;
						this._openSet.EnqueueOrUpdate(neighborRecord, neighborRecord.FCost);
					}

					totalEvaluations++;
				}
			}

			return CreateErrorResult(PathFindingResultType.NoPathFound, EmptyPath);
		}

		private List<Vec2Int> ReconstructPath(PathFindingNode endNode) {
			List<Vec2Int> path = new(16) { endNode.Position };
			PathFindingNode current = endNode;

			while (current.ParentPosition != PathFindingNode.StartPosition) {
				Vec2Int parentPos = current.ParentPosition;
				path.Add(parentPos);

				if (!this._nodeRecords.TryGetValue(parentPos, out current)) {
					break;
				}
			}

			path.Reverse();
			return path;
		}

		private PathFindingResult CreateErrorResult(PathFindingResultType resultType, List<Vec2Int> path) {
			return new PathFindingResult(
				resultType,
				path,
				this._graphManager.TotalNodeCount,
				this._closedSet.Count,
				this._openSet.Count,
				this._config.CostCalculationType,
				this._config.GreedyNess
			);
		}
	}
}