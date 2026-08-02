using Kope.EntityIdentity;
using Kope.Feature.PathFindingNew.PathFinding;
using Kope.Feature.PathFindingNew.Utility;

namespace Kope.Feature.PathFindingNew.Interface {

	/// <summary>
	/// Common contract for pathfinding strategies operating over the shared
	/// <see cref="Graph.Graphmanager"/> grid topology. Extracted so call sites (and benchmark/debug
	/// tooling) can depend on "a pathfinder" instead of concretely on <see cref="AStar"/>, letting
	/// alternative strategies — e.g. <see cref="BidirectionalAStar"/> — be swapped in without touching
	/// anything downstream.
	/// </summary>
	public interface IPathFinder {

		/// <summary>
		/// Finds the shortest walkable path between two points.
		/// </summary>
		/// <param name="start">Starting grid coordinate.</param>
		/// <param name="end">Destination target grid coordinate.</param>
		/// <param name="movementCapability">Bitmask of movement modes supported by the querying agent.</param>
		/// <param name="recorder">Optional editor recorder for step-by-step pathfinding visualization.</param>
		/// <returns>A <see cref="PathFindingResult"/> containing status metadata and execution metrics.</returns>
		PathFindingResult FindPath(Vec2Int start, Vec2Int end, MovementCapability movementCapability, PathfindingRecorder recorder = null);
	}
}