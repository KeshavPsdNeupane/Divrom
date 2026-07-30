
using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;

public enum CostCalculationType {
	Manhattan,
	Euclidean,
	Octile
}

public class PathFindingResult<Tlist> {
	public bool Success;
	public List<Tlist> Path;
	public int TotalNodes;
	public int TotalNodeEvaluations;
	public int TotalNodeExpansions;
	public CostCalculationType CostCalculationType;
	public float Greediness;

}


public class MacroPathFindingResult : PathFindingResult<BoundingBox> {
}
public class MicroPathFindingResult : PathFindingResult<Vec2Int> {
}