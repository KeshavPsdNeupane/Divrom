
using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;

public class PathFindingResult<Tlist> {
	public List<Tlist> Path;
	public int TotalNodeSearches;
	public int TotalNodeEvaluations;
}


public class MacroPathFindingResult : PathFindingResult<BoundingBox> {
}
public class MicroPathFindingResult : PathFindingResult<Vec2Int> {
}