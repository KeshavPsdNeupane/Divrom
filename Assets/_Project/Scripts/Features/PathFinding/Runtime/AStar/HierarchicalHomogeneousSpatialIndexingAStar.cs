using System;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Algorithms;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;

namespace Project.Scripts.Features.PathFinding.Algorithms {

	public class HierarchicalHomogeneousSpatialIndexingAStar {
		private readonly PathfindingGraphManager _graphManager;
		private readonly AStarMicro _microAStar;
		private readonly AStarMacro _macroAStar;

		public HierarchicalHomogeneousSpatialIndexingAStar(
			PathfindingGraphManager graphManager, PathFindingConfig microConfig,
			PathFindingConfig macroConfig
			) {
			this._graphManager = graphManager ?? throw new ArgumentNullException(nameof(graphManager));
			this._microAStar = new AStarMicro(graphManager, microConfig);
			this._macroAStar = new AStarMacro(graphManager, macroConfig);
		}

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

			var macroResultFinal = this._macroAStar.FindPath(start, end, entityMovementCapability, macroRecorder);

			if (macroResultFinal.pathFindingResultType != PathFindingResultType.Success) {
				return new PathFindingResultAggregate(PathFindingErrorPath.MacroPathFinding, macroResultFinal, null);
			}
			//	Debug.Log($"Macro pathfinding successful. Macro path length: {macroResultFinal.Path.Count} | Start: {start} | End: {end}");

			var corridorsTileSet = this._graphManager.GetAllCorridorPositions(macroResultFinal.Path);
			//	Debug.Log($"Corridor size: {corridorsTileSet.Count} | contains start {start}: {corridorsTileSet.Contains(start)} | contains end {end}: {corridorsTileSet.Contains(end)}");

			var microResultFinal = this._microAStar.FindPath(start, end, corridorsTileSet, microRecorder);

			if (microResultFinal.pathFindingResultType != PathFindingResultType.Success) {
				return new PathFindingResultAggregate(PathFindingErrorPath.MicroPathFinding,
				 macroResultFinal, microResultFinal);
			}
			return new PathFindingResultAggregate(PathFindingErrorPath.None, macroResultFinal, microResultFinal);
		}
	}
}