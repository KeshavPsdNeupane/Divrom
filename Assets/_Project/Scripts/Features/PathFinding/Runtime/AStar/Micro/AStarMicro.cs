using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingOld.Node;
using Kope.Feature.PathFindingOld.PathFinding;
using Project.Scripts.Features.PathFindingOld.GraphManager;
using ThirdParty.PriorityQueeu;
using UnityEngine;

namespace Kope.Feature.PathFindingOld.Algorithms {

	public class AStarMicro {


		private static readonly List<Vec2Int> EMPTY_PATH = new();

		private readonly Dictionary<CostCalculationType, Func<Vec2Int, Vec2Int, int>> _costCalculators = new() {
			{ CostCalculationType.Manhattan, MicroGridNode.ManhattanCost },
			{ CostCalculationType.Euclidean, MicroGridNode.EuclideanCost },
			{ CostCalculationType.Octile, MicroGridNode.OctileCost }
		};

		private int _maxIterations = 10;
		private PathFindingConfig _config;

		private readonly IPathfindingGraphManager _graphManager;
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
			IPathfindingGraphManager graphManager,
			PathFindingConfig config = default) {
			this._graphManager = graphManager;
			this._nodeRecords = new Dictionary<Vec2Int, MicroPathFindingNode>(config.InitialCapacity);
			this._closedSet = new HashSet<Vec2Int>(config.InitialCapacity);
			this._openSet = new QuadPriorityQueue<MicroPathFindingNode, int>(config.InitialCapacity);

			this._config = config;
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

			this._maxIterations = Mathf.CeilToInt(this._config.MaxIterationRatio * this._totalNodesCache);

			var costCalculationType = this._config.CostCalculationType;
			var greedyNess = this._config.GreedyNess;


			Func<Vec2Int, Vec2Int, int> costCalculator = this._costCalculators[costCalculationType];

			int rawInitialH = costCalculator(start, end);
			int weightedInitialH = Mathf.FloorToInt(rawInitialH * greedyNess);


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
						totalExpansion, costCalculationType,
						greedyNess
					);
				}

				this._closedSet.Add(currentRecord.NodePosition);
				totalExpansion++;

				// Delegated neighbor retrieval: handles boundary checks, obstacles, 
				// diagonal corner rules, and closed-set skipping
				foreach (var neighborNode in this._graphManager.GetWalkableMicroNeighboringNodesWithRules(
					currentRecord.NodePosition)) {

					Vec2Int neighborPos = neighborNode.Position;

					if (this._closedSet.Contains(neighborPos)) {
						continue;
					}

					// Restrict neighbor expansion strictly to the corridor set
					if (corridorsTileSet != null && !corridorsTileSet.Contains(neighborPos)) {
						continue;
					}

					int stepCost = costCalculator(currentRecord.NodePosition, neighborPos);
					int tentativeCost = currentRecord.GCost + stepCost;

					// Update node record if unvisited or if a cheaper path is discovered
					if (!this._nodeRecords.TryGetValue(neighborPos, out var existingNeighborRecord)
						|| tentativeCost < existingNeighborRecord.GCost) {

						int rawHCost = costCalculator(neighborPos, end);


						int weightedHCost = (greedyNess == PathFindingConfig.DEFAULT_GREEDINESS)
							? rawHCost
							: Mathf.FloorToInt(rawHCost * greedyNess);

						MicroPathFindingNode neighborRecord = new(
							neighborPos,
							tentativeCost,
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

		private MicroPathFindingResult CreateFailureResult(
			PathFindingResultType resultType, int totalNodeEvaluations, int totalNodeExpansions) {
			return new MicroPathFindingResult(
				resultType,
				EMPTY_PATH, this._totalNodesCache,
				totalNodeEvaluations, totalNodeExpansions,
				this._config.CostCalculationType, this._config.GreedyNess
			);
		}
	}
}