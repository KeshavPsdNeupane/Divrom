using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.Graph;
using Kope.Feature.PathFindingNew.Interface;
using Kope.Feature.PathFindingNew.PathFinding;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;



public class PathFindingService {
	private readonly IPathFinder _pathFinder;


	public PathFindingService(AStarType type, GraphManager graphManager, PathFindingConfig config) {
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
	bool doReachabilityCheck = false,
	 PathfindingRecorder recorder = null) {

		var preCheckResult = this._pathFinder.PreCheckFeasibility(start, end, movementCapability, doReachabilityCheck);
		if (preCheckResult.Status != PathFindingStatus.Success) {
			return preCheckResult;
		}

		return this._pathFinder.FindPath(start, end, movementCapability, recorder);
	}

	private IPathFinder GeneratePathFinder(AStarType type, GraphManager graphManager, PathFindingConfig config) {
		return type switch {
			AStarType.Standard => new AStar(graphManager, config),
			AStarType.Bidirectional => new BidirectionalAStar(graphManager, config),
			_ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null),
		};
	}
}
