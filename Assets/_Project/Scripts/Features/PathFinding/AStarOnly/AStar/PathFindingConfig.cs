
using System.Collections.Generic;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;

namespace Kope.Feature.PathFindingNew.PathFinding {
	public enum AStarType {
		Standard,
		Bidirectional
	}
	public enum CostCalculationType {
		Manhattan,
		Euclidean,
		Octile
	}
	public enum PathFindingStatus {
		Success = 0,
		NoPathFound = 1,
		UnReachableTarget = 20,
		InvalidStartOrEnd = 30,
		ExceededMaxIterations = 31,
		StartEqualsEnd = 50,

	}

	public struct PathFindingConfig {
		/// <summary>
		/// Default cap ratio on max search iterations relative to total nodes in the macro graph.
		/// </summary>
		public const float MAX_NODE_SEARCH_RATIO = 1f;
		public const float MIN_NODE_SEARCH_RATIO = 0.2f;
		public const int MIN_NODE_SEARCH = 32;
		/// <summary>
		/// Default allocation capacity for internal collections to prevent runtime GC re-allocations.
		/// </summary>
		public const int DEFAULT_INITIAL_CAPACITY = 256;

		/// <summary>
		/// Maximum allowed heuristic weighting factor (w) for Weighted A*.
		/// </summary>
		public const float MAX_GREEDINESS = 1.5f;
		public const float DEFAULT_GREEDINESS = 1f;

		public CostCalculationType CostCalculationType;
		public float GreedyNess;
		public float MaxIterationRatio;
		public int InitialCapacity;
		public PathFindingConfig(CostCalculationType costCalculationType = CostCalculationType.Octile,
		int initialCapacity = DEFAULT_INITIAL_CAPACITY,
		float greediness = DEFAULT_GREEDINESS,
		float maxIterationRatio = MAX_NODE_SEARCH_RATIO) {

			CostCalculationType = costCalculationType;

			GreedyNess = Mathf.Clamp(greediness, 1f, MAX_GREEDINESS);

			MaxIterationRatio = Mathf.Clamp(maxIterationRatio, MIN_NODE_SEARCH_RATIO, MAX_NODE_SEARCH_RATIO);

			InitialCapacity = Mathf.Max(initialCapacity, DEFAULT_INITIAL_CAPACITY);
		}
	}



	public class MicroPathfindingRecorder {
		public struct StepSnapshot {
			public Vec2Int Current;
			public List<Vec2Int> OpenSet;
			public HashSet<Vec2Int> ClosedSet;
		}
		public List<StepSnapshot> Steps { get; } = new();
		public void Clear() {
			Steps.Clear();
		}

		public void RecordStep(Vec2Int current, IEnumerable<Vec2Int> openSet, IEnumerable<Vec2Int> closedSet) {
			Steps.Add(new StepSnapshot {
				Current = current,
				OpenSet = new List<Vec2Int>(openSet),
				ClosedSet = new HashSet<Vec2Int>(closedSet)
			});
		}
	}

	public class PathFindingResult {
		public PathFindingStatus Status;
		public List<Vec2Int> Path;
		public int TotalNodes;
		public int TotalNodeEvaluations;
		public int TotalNodeExpansions;
		public CostCalculationType CostCalculationType;
		public float Greediness;
		public PathFindingResult(PathFindingStatus resultType,
		List<Vec2Int> path, int totalNodes, int totalNodeEvaluations,
		int totalNodeExpansions, CostCalculationType costCalculationType,
		 float greediness) {
			Status = resultType;
			Path = path;
			TotalNodes = totalNodes;
			TotalNodeEvaluations = totalNodeEvaluations;
			TotalNodeExpansions = totalNodeExpansions;
			CostCalculationType = costCalculationType;
			Greediness = greediness;
		}

	}


}