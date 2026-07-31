
using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

public enum CostCalculationType {
	Manhattan,
	Euclidean,
	Octile
}
public enum PathFindingResultType {
	Success = 0,
	NoPathFound = 1,
	InvalidStartOrEnd = 2,
	ExceededMaxIterations = 3,
}
public enum PathFindingErrorPath {
	None,
	MicroPreCheck,
	MacroPreCheck,
	MacroPathFinding,
	MicroPathFinding
}

public struct PathFindingConfig {
	/// <summary>
	/// Default cap ratio on max search iterations relative to total nodes in the macro graph.
	/// </summary>
	public const float MAX_ITERATIONS_RATIO = 1f;
	public const float MIN_ITERATIONS_RATIO = 0.2f;
	/// <summary>
	/// Default allocation capacity for internal collections to prevent runtime GC re-allocations.
	/// </summary>
	public const int DEFAULT_INITIAL_CAPACITY = 32;

	/// <summary>
	/// Maximum allowed heuristic weighting factor (w) for Weighted A*.
	/// </summary>
	public const float MAX_GREEDINESS = 1.5f;
	public const float DEFAULT_GREEDINESS = 1f;

	public CostCalculationType CostCalculationType;
	public float Greediness;
	public float MaxIterationRatio;
	public int InitialCapacity;
	public PathFindingConfig(CostCalculationType costCalculationType = CostCalculationType.Octile,
	int initialCapacity = DEFAULT_INITIAL_CAPACITY,
	float greediness = DEFAULT_GREEDINESS,
	float maxIterationRatio = MAX_ITERATIONS_RATIO) {

		CostCalculationType = costCalculationType;

		Greediness = Mathf.Clamp(greediness, 1f, MAX_GREEDINESS);

		MaxIterationRatio = Mathf.Clamp(maxIterationRatio, MIN_ITERATIONS_RATIO, MAX_ITERATIONS_RATIO);

		InitialCapacity = Mathf.Max(initialCapacity, DEFAULT_INITIAL_CAPACITY);
	}
}


public class PathFindingResult<Tlist> {
	public PathFindingResultType pathFindingResultType;
	public List<Tlist> Path;
	public int TotalNodes;
	public int TotalNodeEvaluations;
	public int TotalNodeExpansions;
	public CostCalculationType CostCalculationType;
	public float Greediness;
	public PathFindingResult(PathFindingResultType resultType, List<Tlist> path, int totalNodes, int totalNodeEvaluations, int totalNodeExpansions, CostCalculationType costCalculationType, float greediness) {
		pathFindingResultType = resultType;
		Path = path;
		TotalNodes = totalNodes;
		TotalNodeEvaluations = totalNodeEvaluations;
		TotalNodeExpansions = totalNodeExpansions;
		CostCalculationType = costCalculationType;
		Greediness = greediness;
	}

}


public class MacroPathFindingResult : PathFindingResult<BoundingBox> {
	public MacroPathFindingResult(PathFindingResultType resultType, List<BoundingBox> path, int totalNodes, int totalNodeEvaluations, int totalNodeExpansions, CostCalculationType costCalculationType, float greediness) :
	base(resultType, path, totalNodes, totalNodeEvaluations, totalNodeExpansions, costCalculationType, greediness) {
	}
}
public class MicroPathFindingResult : PathFindingResult<Vec2Int> {
	public MicroPathFindingResult(PathFindingResultType resultType, List<Vec2Int> path, int totalNodes, int totalNodeEvaluations, int totalNodeExpansions, CostCalculationType costCalculationType, float greediness) :
	base(resultType, path, totalNodes, totalNodeEvaluations, totalNodeExpansions, costCalculationType, greediness) {
	}
}



public struct PathFindingResultAggregate {
	public PathFindingErrorPath ErrorPath;
	public MacroPathFindingResult MacroResult;
	public MicroPathFindingResult MicroResult;
	public PathFindingResultAggregate(PathFindingErrorPath errorPath,
	MacroPathFindingResult macroResult, MicroPathFindingResult microResult) {
		this.ErrorPath = errorPath;
		MacroResult = macroResult;
		MicroResult = microResult;
	}
}