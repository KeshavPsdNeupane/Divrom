using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;

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