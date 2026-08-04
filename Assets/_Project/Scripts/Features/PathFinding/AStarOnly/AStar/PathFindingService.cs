using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Graph;
using Kope.Feature.PathFindingNew.Interface;
using Kope.Feature.PathFindingNew.PathFinding;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;



public class PathFindingService {
	private readonly GraphManager _graphManager;
	private readonly IPathFinder _pathFinder;


	public PathFindingService(AStarType type, GraphManager graphManager, PathFindingConfig config) {
		this._graphManager = graphManager;
		this._pathFinder = GeneratePathFinder(type, graphManager, config);
	}
	/// <summary>
	/// Finds a path from the start position to the end position using the specified movement capability.
	/// If ignoreReachabilityCheck is true, the reachability check will be skipped.
	/// </summary>
	/// <param name="start"></param>
	/// <param name="end"></param>
	/// <param name="movementCapability"></param>
	/// <param name="doReachabilityCheck"></param>
	/// <param name="recorder"></param>
	/// <returns></returns>
	public PathFindingResult FindPath(Vec2Int start, Vec2Int end, MovementCapability movementCapability,
	bool doReachabilityCheck = false, bool stringPulling = true,
	 PathfindingRecorder recorder = null) {

		var preCheckResult = this._pathFinder.PreCheckFeasibility(start, end, movementCapability, doReachabilityCheck);
		if (preCheckResult.Status != PathFindingStatus.Success) {
			return preCheckResult;
		}
		var pathResult = this._pathFinder.FindPath(start, end, movementCapability, recorder);
		// // Decoupled from the recorder on purpose: FinalPath is set straight from the result, so
		// FinalPathOnly (and the end-of-animation reveal) works even with recording off.
		var finalPath = stringPulling ? PathSmoother.StringPull(
				pathResult.Path,
				(fromPoint, toPoint) => PathSmoother.HasLineOfSight(
					fromPoint, toPoint,
					pos => this._graphManager.IsWalkable(pos, movementCapability)
			)) : pathResult.Path;

		return pathResult.CopyWithNewPath(finalPath);
	}

	private IPathFinder GeneratePathFinder(AStarType type, GraphManager graphManager, PathFindingConfig config) {
		return type switch {
			AStarType.Standard => new AStar(graphManager, config),
			AStarType.Bidirectional => new BidirectionalAStar(graphManager, config),
			_ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
		};
	}
}
