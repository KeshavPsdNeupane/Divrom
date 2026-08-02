using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Graph;
using Kope.Feature.PathFindingNew.Interface;
using Kope.Feature.PathFindingNew.PathFinding;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.Base {

	/// <summary>
	/// Abstract base class for pathfinding solvers in the Kope framework.
	/// Provides graph reference management, zero-allocation error result formatting,
	/// and fast feasibility pre-checks before executing full path searches.
	/// </summary>
	public abstract class PathFinderBase : IPathFinder {

		/// <summary>
		/// Read-only empty path instance used for failed or invalid path queries to eliminate heap allocations.
		/// </summary>
		public static readonly List<Vec2Int> EMPTY_PATH = new(0);

		protected readonly GraphManager _graphManager;
		protected readonly PathFindingConfig _config;

		/// <summary>
		/// Initializes a new instance of the <see cref="PathFinderBase"/> class.
		/// </summary>
		/// <param name="graphManager">The graph manager providing node lookups and topological data.</param>
		/// <param name="config">Configuration settings governing path heuristic evaluation and weights.</param>
		protected PathFinderBase(GraphManager graphManager, PathFindingConfig config) {
			this._graphManager = graphManager;
			this._config = config;
		}

		/// <summary>
		/// Evaluates whether a path request is valid before running full search routines.
		/// Short-circuits execution via lightweight order-of-checks: point equality, node existence, 
		/// region reachability, and agent movement capability checks.
		/// </summary>
		/// <param name="start">Origin grid coordinate.</param>
		/// <param name="end">Destination grid coordinate.</param>
		/// <param name="movementCapability">Bitmask defining the requesting entity's travel capabilities (e.g. Move, Swim, Fly).</param>
		/// <param name="doReachabilityCheck">
		/// If <c>true</c>, executes an $O(1)$ region ID match check prior to specific capability validation.
		/// </param>
		/// <returns>
		/// A <see cref="PathFindingResult"/> containing <see cref="PathFindingStatus.Success"/> if the query is valid,
		/// or the precise failure status if rejected early.
		/// </returns>
		public PathFindingResult PreCheckFeasibility(
			Vec2Int start,
			Vec2Int end,
			MovementCapability movementCapability,
			bool doReachabilityCheck = true) {

			// 1. Start and End Equality Check
			if (start == end) {
				return CreateErrorResult(PathFindingStatus.StartEqualsEnd);
			}

			// 2. Node Existence Check
			if (!this._graphManager.TryGetNode(start, out var startNode) ||
				!this._graphManager.TryGetNode(end, out var endNode)) {
				return CreateErrorResult(PathFindingStatus.InvalidStartOrEnd);
			}

			// 3. Fast Region Reachability Check
			// Validates whether start and end reside within the same connected region ID.
			if (doReachabilityCheck && !GridNode.IsRegionReachable(startNode, endNode)) {
				return CreateErrorResult(PathFindingStatus.UnReachableTarget);
			}

			// 4. Node Traversability & Capability Check
			// Validates whether the agent has active capabilities (e.g. Move, Swim, Fly) required for these nodes.
			if (!startNode.IsTraversable(movementCapability) ||
				!endNode.IsTraversable(movementCapability)) {
				return CreateErrorResult(PathFindingStatus.InvalidStartOrEnd);
			}

			return CreateErrorResult(PathFindingStatus.Success);
		}

		/// <inheritdoc />
		public abstract PathFindingResult FindPath(
			Vec2Int start,
			Vec2Int end,
			MovementCapability movementCapability,
			PathfindingRecorder recorder = null);

		/// <summary>
		/// Constructs a standard error result object populated with grid dimension and solver configuration metadata.
		/// </summary>
		/// <param name="resultType">The specific failure status code.</param>
		/// <param name="closedSetCount">Number of nodes evaluated in the closed set prior to termination.</param>
		/// <param name="openSetCount">Number of nodes remaining in the open set prior to termination.</param>
		/// <returns>A formatted <see cref="PathFindingResult"/> with an empty path allocation and runtime telemetry.</returns>
		protected PathFindingResult CreateErrorResult(
			PathFindingStatus resultType,
			int closedSetCount = 0,
			int openSetCount = 0) {

			return new PathFindingResult(
				resultType,
				EMPTY_PATH,
				this._graphManager.TotalNodeCount,
				closedSetCount,
				openSetCount,
				this._config.CostCalculationType,
				this._config.GreedyNess
			);
		}
	}
}