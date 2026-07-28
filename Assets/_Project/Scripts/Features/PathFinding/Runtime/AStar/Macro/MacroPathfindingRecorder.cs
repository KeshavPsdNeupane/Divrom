using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;

public class MacroPathfindingRecorder {
	public struct StepSnapshot {
		public BoundingBox Current;
		public List<BoundingBox> OpenSet;
		public HashSet<BoundingBox> ClosedSet;
	}
	public List<StepSnapshot> Steps { get; } = new();

	public void Clear() {
		Steps.Clear();
	}

	public void RecordStep(BoundingBox current, IEnumerable<BoundingBox> openSet, IEnumerable<BoundingBox> closedSet) {
		Steps.Add(new StepSnapshot {
			Current = current,
			OpenSet = new List<BoundingBox>(openSet),
			ClosedSet = new HashSet<BoundingBox>(closedSet)
		});
	}
}