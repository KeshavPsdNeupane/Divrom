using System;
using System.Collections.Generic;
using Kope.EntityIdentity;
using Kope.Feature.PathFindingOld.Algorithms;
using Kope.Feature.PathFindingOld.Node;
using Kope.Feature.PathFindingOld.PathFinding;
using Project.Scripts.Features.PathFindingOld.GraphManager;
using UnityEngine;

namespace Project.Scripts.Features.PathFindingOld.Algorithms {

	public class HierarchicalHomogeneousSpatialIndexingAStar {
		/// <summary>
		/// The initial capacity of the corridor tile set. This is a performance optimization to reduce
		/// memory allocations during pathfinding, as corridor tiles are often reused across multiple searches.
		/// Not really big 256*2*8 = 4096 bytes, but it is a good starting point for most scenarios.
		/// so only 4kb of memory is allocated for the HashSet, which is a reasonable 
		/// trade-off between memory usage and performance.
		/// </summary>
		const int INITIAL_SET_CAPACITY = 256;
		private readonly IPathfindingGraphManager _graphManager;
		private readonly AStarMicro _microAStar;
		private readonly AStarMacro _macroAStar;
		private readonly HashSet<Vec2Int> corridorsTileSet;
		public HierarchicalHomogeneousSpatialIndexingAStar(
			IPathfindingGraphManager graphManager, PathFindingConfig microConfig,
			PathFindingConfig macroConfig
			) {
			this._graphManager = graphManager ?? throw new ArgumentNullException(nameof(graphManager));
			this._microAStar = new AStarMicro(graphManager, microConfig);
			this._macroAStar = new AStarMacro(graphManager, macroConfig);
			this.corridorsTileSet = new HashSet<Vec2Int>(INITIAL_SET_CAPACITY);
		}
		readonly System.Diagnostics.Stopwatch stopwatch = new();

		/// <summary>
		/// Runs the full macro -> micro search. This is the single entry point every caller (gizmos
		/// included) should go through — nothing outside this class touches AStarMacro/AStarMicro
		/// directly. <paramref name="macroRecorder"/> and <paramref name="microRecorder"/> are
		/// optional and forwarded straight through to their respective FindPath calls (same
		/// optional-default pattern AStarMacro.FindPath already used before this change) — pass a
		/// gizmo's Recorder to capture step-by-step open/closed-set data for that stage, or leave
		/// both null for a zero-recording-overhead search.
		/// But on Play mode the Recorder is fully ignored, so you can pass a gizmo's Recorder in 
		/// Play mode without worrying about performance.
		/// </summary>

		public PathFindingResultAggregate FindPath(
			Vec2Int start, Vec2Int end, MovementCapability entityMovementCapability,
			MacroPathfindingRecorder macroRecorder = null,
			MicroPathfindingRecorder microRecorder = null) {


			if (!this._macroAStar.PreCheck(start, end, entityMovementCapability, out var macroPreCheckResult)) {
				return new PathFindingResultAggregate(PathFindingErrorPath.MacroPreCheck, macroPreCheckResult, null);
			}

			if (!this._microAStar.PreCheck(start, end, entityMovementCapability, out var microPreCheckResult)) {
				return new PathFindingResultAggregate(PathFindingErrorPath.MicroPreCheck, macroPreCheckResult, microPreCheckResult);
			}


			stopwatch.Restart();

			var macroPathFindingResult = this._macroAStar.FindPath(start, end, entityMovementCapability, macroRecorder);
			stopwatch.Stop();
			Debug.Log($"Macro A* took {stopwatch.ElapsedMilliseconds} ms {stopwatch.ElapsedTicks} Ticks for pathfinding from {start} to {end}.");

			if (macroPathFindingResult.pathFindingResultType != PathFindingResultType.Success) {
				return new PathFindingResultAggregate(PathFindingErrorPath.MacroPathFinding, macroPathFindingResult, null);
			}

			stopwatch.Restart();
			this._graphManager.GetAllCorridorPositions(macroPathFindingResult.Path, this.corridorsTileSet);

			stopwatch.Stop();
			Debug.Log($"Corridor extraction took {stopwatch.ElapsedMilliseconds} ms {stopwatch.ElapsedTicks} for pathfinding from {start} to {end}.");

			stopwatch.Restart();
			var microResultFinal = this._microAStar.FindPath(start, end, this.corridorsTileSet, microRecorder);
			stopwatch.Stop();
			Debug.Log($"Micro A* took {stopwatch.ElapsedMilliseconds} ms {stopwatch.ElapsedTicks} for pathfinding from {start} to {end}.");


			if (microResultFinal.pathFindingResultType != PathFindingResultType.Success) {
				return new PathFindingResultAggregate(PathFindingErrorPath.MicroPathFinding,
				 macroPathFindingResult, microResultFinal);
			}
			return new PathFindingResultAggregate(PathFindingErrorPath.None, macroPathFindingResult, microResultFinal);
		}



		public void InjectMicroConfig(PathFindingConfig config) {
			// This method is intentionally left blank for future micro configuration injection.
		}
		public void InjectMacroConfig(PathFindingConfig config) {
			// This method is intentionally left blank for future macro configuration injection.
		}
	}
}