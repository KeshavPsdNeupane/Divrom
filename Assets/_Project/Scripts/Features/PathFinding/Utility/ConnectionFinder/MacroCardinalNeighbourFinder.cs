using System.Collections.Generic;

namespace Kope.Feature.PathFinding.Utility {
	using Kope.Feature.PathFinding.Interface;

	public class MacroCardinalNeighbourFinder : IMacroNeighbourFinder {

		// Every adjacency has exactly one "lower/left" box and one "upper/right" box, so
		// searching only these two directions from every box finds each edge exactly once.
		// A Left/Down pass would just re-find the same pairs from the other side and need
		// a dedup set — this way we don't need one.
		internal enum EdgeSearchDirection { Right, Up }

		private const int MinConnectionCapacity = 4;
		private const int MinBucketCapacity = 2;

		public Dictionary<BoundingBox, List<BoundingBox>> FindNeighbours(
			Dictionary<(int x, int y), BoundingBox> microToMacro,
			BoundingBox[] boundingBoxesArray) {

			// microToMacro is unused here — cardinal adjacency is derived purely from box
			// geometry. Kept in the signature to satisfy IMacroNeighbourFinder.

			int totalBoundingBoxes = boundingBoxesArray.Length;
			var connections = new Dictionary<BoundingBox, List<BoundingBox>>(totalBoundingBoxes);

			// Keyed by Min.x / Min.y rather than by box, since several boxes can start on
			// the same column/row (e.g. a stack of boxes against one vertical seam).
			var boxesByMinX = new Dictionary<int, List<BoundingBox>>(totalBoundingBoxes);
			var boxesByMinY = new Dictionary<int, List<BoundingBox>>(totalBoundingBoxes);

			foreach (var box in boundingBoxesArray) {
				AddToBucket(boxesByMinX, box.Min.x, box);
				AddToBucket(boxesByMinY, box.Min.y, box);
			}

			foreach (var box in boundingBoxesArray) {
				// Right — horizontal edge share
				if (boxesByMinX.TryGetValue(box.Max.x + 1, out var rightCandidates)) {
					foreach (var candidate in rightCandidates) {
						if (RangesOverlap(box.Min.y, box.Max.y, candidate.Min.y, candidate.Max.y)) {
							AddConnection(connections, box, candidate);
						}
					}
				}

				// Up — vertical edge share
				if (boxesByMinY.TryGetValue(box.Max.y + 1, out var upCandidates)) {
					foreach (var candidate in upCandidates) {
						if (RangesOverlap(box.Min.x, box.Max.x, candidate.Min.x, candidate.Max.x)) {
							AddConnection(connections, box, candidate);
						}
					}
				}
			}

			return connections;
		}

		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		private static bool RangesOverlap(int aMin, int aMax, int bMin, int bMax) => aMin <= bMax && bMin <= aMax;

		private static void AddToBucket(Dictionary<int, List<BoundingBox>> buckets, int key, BoundingBox box) {
			if (!buckets.TryGetValue(key, out var list)) {
				list = new List<BoundingBox>(MinBucketCapacity);
				buckets[key] = list;
			}
			list.Add(box);
		}

		private static void AddConnection(Dictionary<BoundingBox, List<BoundingBox>> connections, BoundingBox a, BoundingBox b) {
			if (!connections.TryGetValue(a, out var aList)) {
				aList = new List<BoundingBox>(MinConnectionCapacity);
				connections[a] = aList;
			}
			if (!aList.Contains(b)) aList.Add(b);

			if (!connections.TryGetValue(b, out var bList)) {
				bList = new List<BoundingBox>(MinConnectionCapacity);
				connections[b] = bList;
			}
			if (!bList.Contains(a)) bList.Add(a);
		}
	}
}