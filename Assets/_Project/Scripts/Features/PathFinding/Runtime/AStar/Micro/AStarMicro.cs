using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;

namespace Kope.Feature.PathFinding.Algorithms {

	public class AStarMicro {
		private static readonly List<Vec2Int> EMPTY_PATH = new();
		private static readonly Vec2Int[] NEIGHBOR_OFFSET = new[] {
			new Vec2Int(0, 1),   // Up
            new Vec2Int(1, 0),   // Right
            new Vec2Int(0, -1),  // Down
            new Vec2Int(-1, 0),  // Left
            new Vec2Int(1, 1),   // Up-Right
            new Vec2Int(1, -1),  // Down-Right
            new Vec2Int(-1, -1), // Down-Left
            new Vec2Int(-1, 1)   // Up-Left
        };

		private static readonly Dictionary<Vec2Int, (Vec2Int, Vec2Int)> NEIGHBORING_RULE_MAP = new() {
			{ new Vec2Int(1, 1),   (new Vec2Int(1, 0), new Vec2Int(0, 1)) },   // Up-Right rule
            { new Vec2Int(1, -1),  (new Vec2Int(1, 0), new Vec2Int(0, -1)) },  // Down-Right rule
            { new Vec2Int(-1, -1), (new Vec2Int(-1, 0), new Vec2Int(0, -1)) }, // Down-Left rule
            { new Vec2Int(-1, 1),  (new Vec2Int(-1, 0), new Vec2Int(0, 1)) }   // Up-Left rule
        };

		private readonly Dictionary<CostCalculationType, Func<Vec2Int, Vec2Int, int>> _costCalculators = new() {
			{ CostCalculationType.Manhattan, MicroGridNode.ManhattanCost },
			{ CostCalculationType.Euclidean, MicroGridNode.EuclideanCost },
			{ CostCalculationType.Octile, MicroGridNode.OctileCost }
		};

		private readonly float _maxIterationsRatio;
		private int _maxIterations = 10;
		private readonly float _greedyNess = 1f;
		private readonly CostCalculationType _costCalculationType = CostCalculationType.Manhattan;

		private readonly PathfindingGraphManager _graphManager;
		private readonly Dictionary<Vec2Int, MicroPathFindingNode> _nodeRecords;
		private readonly HashSet<Vec2Int> _closedSet;
		private readonly QuadPriorityQueue<MicroPathFindingNode, int> _openSet;

		private int _totalNodesCache;

#if UNITY_EDITOR
		/// <summary>
		/// Reusable buffer for editor visualization tools. Stripped out completely in standalone builds.
		/// </summary>
		private readonly List<Vec2Int> _recorderOpenListCache = new(PathFindingConfig.DEFAULT_INITIAL_CAPACITY);
#endif

		public AStarMicro(
			PathfindingGraphManager graphManager,
			PathFindingConfig config = default) {
			this._graphManager = graphManager ?? throw new ArgumentNullException(nameof(graphManager));
			this._nodeRecords = new Dictionary<Vec2Int, MicroPathFindingNode>(config.InitialCapacity);
			this._closedSet = new HashSet<Vec2Int>(config.InitialCapacity);
			this._openSet = new QuadPriorityQueue<MicroPathFindingNode, int>(config.InitialCapacity);

			this._costCalculationType = config.CostCalculationType;
			this._maxIterationsRatio = config.MaxIterationRatio;
			this._greedyNess = config.Greediness;
		}

		public bool PreCheck(Vec2Int start, Vec2Int end, MovementCapability _, out MicroPathFindingResult preCheckResult) {
			this._totalNodesCache = this._graphManager.MicroNodeCount;

			bool TryValidateNode(Vec2Int pos, string pointLabel, out MicroGridNode nodeCache) {
				if (!this._graphManager.TryGetMicroNode(pos, out nodeCache)) {
					Debug.LogWarning($"[{pointLabel}] Position {pos} is not a valid micro node in the graph.");
					return false;
				}

				if (nodeCache.IsStaticObstacle) {
					Debug.LogWarning($"[{pointLabel}] Node at {pos} cannot be used for pathfinding (IsStaticObstacle: {nodeCache.IsStaticObstacle}).");
					return false;
				}

				return true;
			}

			if (!TryValidateNode(start, "Start", out var _) ||
				!TryValidateNode(end, "End", out var _)) {
				preCheckResult = CreateFailureResult(PathFindingResultType.InvalidStartOrEnd, 0, 0);
				return false;
			}

			preCheckResult = default;
			return true;
		}

		/// <summary>
		/// Finds a path from the specified start position to the end position using Weighted A*, restricted to the corridor tile set.
		/// </summary>
		public MicroPathFindingResult FindPath(
			Vec2Int start, Vec2Int end, HashSet<Vec2Int> corridorsTileSet,
			MicroPathfindingRecorder recorder = null) {
			// Clear previous state
			this._nodeRecords.Clear();
			this._closedSet.Clear();
			this._openSet.Clear();
#if UNITY_EDITOR
			recorder?.Clear();
#endif

			this._maxIterations = Mathf.CeilToInt(this._maxIterationsRatio * this._totalNodesCache);

			Func<Vec2Int, Vec2Int, int> costCalculator = this._costCalculators[this._costCalculationType];

			int rawInitialH = costCalculator(start, end);
			int weightedInitialH = Mathf.FloorToInt(rawInitialH * this._greedyNess);


			MicroPathFindingNode startNode = new(start, 0, weightedInitialH, null);

			this._openSet.EnqueueOrUpdate(startNode);
			this._nodeRecords[start] = startNode;

			int totalExpansion = 0;
			int totalNodeEvaluation = 0;

			while (this._openSet.Count > 0 && totalNodeEvaluation < this._maxIterations) {
				totalNodeEvaluation++;
				MicroPathFindingNode currentRecord = this._openSet.Dequeue();

#if UNITY_EDITOR
				if (recorder != null) {
					this._recorderOpenListCache.Clear();
					foreach (var node in this._openSet.GetElements()) {
						this._recorderOpenListCache.Add(node.NodePosition);
					}
					recorder.RecordStep(currentRecord.NodePosition, this._recorderOpenListCache, this._closedSet);
				}
#endif

				if (currentRecord.NodePosition == end) {
					List<Vec2Int> path = ReconstructPath(currentRecord);
					return new MicroPathFindingResult(
						PathFindingResultType.Success, path,
						this._totalNodesCache, totalNodeEvaluation,
						totalExpansion, this._costCalculationType,
						this._greedyNess
					);
				}

				this._closedSet.Add(currentRecord.NodePosition);
				totalExpansion++;

				// Delegated neighbor retrieval: handles boundary checks, obstacles, 
				// diagonal corner rules, and closed-set skipping
				foreach (var neighborNode in this._graphManager.GetWalkableMicroNeighboringNodesWithRules(
					currentRecord.NodePosition,
					NEIGHBOR_OFFSET,
					NEIGHBORING_RULE_MAP,
					this._closedSet)) {

					Vec2Int neighborPos = neighborNode.Position;

					// Restrict neighbor expansion strictly to the corridor set
					if (corridorsTileSet != null && !corridorsTileSet.Contains(neighborPos)) {
						continue;
					}

					int stepCost = costCalculator(currentRecord.NodePosition, neighborPos);
					int tentativeGCost = currentRecord.GCost + stepCost;

					// Update node record if unvisited or if a cheaper path is discovered
					if (!this._nodeRecords.TryGetValue(neighborPos, out var existingNeighborRecord)
						|| tentativeGCost < existingNeighborRecord.GCost) {

						int rawHCost = costCalculator(neighborPos, end);


						int weightedHCost = (this._greedyNess == PathFindingConfig.DEFAULT_GREEDINESS)
							? rawHCost
							: Mathf.FloorToInt(rawHCost * this._greedyNess);

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

			return CreateFailureResult(PathFindingResultType.NoPathFound, totalNodeEvaluation, totalExpansion);
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

		private MicroPathFindingResult CreateFailureResult(PathFindingResultType resultType, int totalNodeEvaluations, int totalNodeExpansions) {
			return new MicroPathFindingResult(
				resultType,
				EMPTY_PATH, this._totalNodesCache,
				totalNodeEvaluations, totalNodeExpansions,
				this._costCalculationType, this._greedyNess
			);
		}
	}
}