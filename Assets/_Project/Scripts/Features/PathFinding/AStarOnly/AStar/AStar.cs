using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Base;
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
	/// via <see cref="GraphManager"/> to eliminate Garbage Collection overhead during search loops.
	/// </para>
	/// </summary>
	public class AStar : PathFinderBase {

		// NOTE: this table is exclusively for estimating H-cost (distance-to-goal). It must never be
		// used to price an actual G-cost edge step — see the rationale comment in the neighbor loop
		// below for why that distinction matters and previously wasn't respected.
		private static readonly Dictionary<CostCalculationType, Func<Vec2Int, Vec2Int, float, int>>
		HEURISTIC_FUNCTIONS = new() {
			{ CostCalculationType.Manhattan, GridNode.ManhattanDistanceTo },
			{ CostCalculationType.Euclidean, GridNode.EuclideanDistanceTo },
			{ CostCalculationType.Octile, GridNode.OctileDistanceTo }
		};

		private readonly int _maxIterations;

		// Node-record storage: a single position-keyed dictionary holding every record created
		// this search. This is the non-index variant — PathFindingNode stores its parent as a
		// Vec2Int (ParentPosition) rather than a slot index, so both "does a record already exist
		// for this grid cell" (neighbor revisit checks) and "find my parent record" (path
		// reconstruction) go through the same Vec2Int-hashed lookup. Simpler than an indexed
		// variant, at the cost of one dictionary lookup per backtrace step in ReconstructPath
		// instead of plain array indexing.
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
		public AStar(GraphManager graphManager, PathFindingConfig config)
		: base(graphManager, config) {
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
		/// <param name="movementCapability">Movement modes supported by the querying agent.</param>
		/// <param name="recorder">Optional editor recorder for step-by-step pathfinding visualization.</param>
		/// <returns>A <see cref="PathFindingResult"/> containing status metadata and execution metrics.</returns>
		public override PathFindingResult FindPath(
			Vec2Int start,
			Vec2Int end,
			MovementCapability movementCapability,
			PathfindingRecorder recorder = null) {

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

			PathFindingNode startNode = new(start.X, start.Y, 0, weightedInitialH, PathFindingNode.NoParent);

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
						PathFindingStatus.Success,
						ReconstructPath(currentRecord, movementCapability),
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
					movementCapability,
					this._fetchBuffer,
					this._neighbourBuffer,
					out byte diagonalMask
				);

				for (int i = 0; i < neighbors.Length; i++) {
					ref readonly GridNode neighborNode = ref neighbors[i];
					Vec2Int neighborPos = neighborNode.Position;

					if (this._closedSet.Contains(neighborPos)) {
						continue;
					}

					if (!neighborNode.TryGetTraversalCost(movementCapability, out float costMultiplier)) {
						continue;
					}

					// G-COST: step cost between two adjacent grid cells reflects actual move
					// geometry (cardinal vs. diagonal) via GridNode.DIRECT_COST/DIAGONAL_COST,
					// never the heuristic distance metric — see AStar's H-cost usage below, which
					// is the only place HEURISTIC_FUNCTIONS may be called.
					bool isDiagonal = (diagonalMask & (1 << i)) != 0;
					int baseStepCost = isDiagonal ? GridNode.DIAGONAL_COST : GridNode.DIRECT_COST;
					int stepCost = Mathf.RoundToInt(baseStepCost * costMultiplier);

					int tentativeGCost = currentRecord.GCost + stepCost;

					bool hasExisting = this._nodeRecords.TryGetValue(neighborPos, out PathFindingNode existingRecord);

					if (!hasExisting || tentativeGCost < existingRecord.GCost) {
						// Pure goal-distance estimate — unit cost multiplier. H-cost approximates
						// remaining distance to the goal; it isn't scaled by any single tile's
						// terrain multiplier along the way.
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

			return CreateErrorResult(PathFindingStatus.NoPathFound);
		}

		/// <summary>
		/// Walks <see cref="PathFindingNode.ParentPosition"/> back to the root via <see cref="_nodeRecords"/>
		/// lookups, one dictionary hit per step. Safe without an existence guard because the invariant is
		/// structural: a record's ParentPosition is only ever set to a position that was already inserted
		/// into <see cref="_nodeRecords"/> before the child that references it.
		/// </summary>
		private List<Vec2Int> ReconstructPath(PathFindingNode endNode, MovementCapability movementCapability) {
			List<Vec2Int> path = new(16) { endNode.Position };
			PathFindingNode current = endNode;

			while (current.ParentPosition != PathFindingNode.NoParent) {
				current = this._nodeRecords[current.ParentPosition];
				path.Add(current.Position);
			}

			path.Reverse();
			return path;
		}
	}
}