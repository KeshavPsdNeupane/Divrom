
namespace Kope.Feature.PathFindingOld.Utility {
	using System.Collections.Generic;
	using UnityEngine;
	using System;
	using Kope.Feature.PathFindingOld.Interface;
	using Kope.Feature.PathFindingOld.Node;

	[Obsolete("Deprecated: Diagonal neighbor detection on 2D tile grids is impractical" +
	" to implement and maintain—corner overlaps share a single zero-length vertex" +
	" rather than a flat edge, causing awkward seam math, corner-clipping physics," +
	" and invalid pathfinding edges. Use 4-way cardinal neighbor finding instead. Kept" +
	" for legacy support and will be removed in future versions.")]
	public class MacroNeighbourFinderAllDirection : IMacroNeighbourFinder {

		// Every adjacency has exactly one "lower/left" box and one "upper/right" box, so
		// searching only these two directions from every box finds each edge exactly once.
		// A Left/Down pass would just re-find the same pairs from the other side and need
		// a dedup set — this way we don't need one.
		internal enum EdgeSearchDirection { Right, Up }

		// Only 2 distinct diagonal-touch topologies exist for any pair of boxes (not 4 —
		// "A top-right touches B bottom-left" and "A bottom-left touches B top-right" are
		// the same physical relationship). Searching UpRight/UpLeft from every box covers
		// both exactly once; DownRight/DownLeft get found when the *other* box searches up.
		internal enum CornerSearchDirection { UpRight, UpLeft }

		private const int MinConnectionCapacity = 4; // typical branching factor for packed regions
		private const int MinBucketCapacity = 2;      // usually only a couple boxes share an edge coordinate

		public Dictionary<BoundingBox, List<BoundingBox>> FindNeighbours(
			Dictionary<(int x, int y), BoundingBox> microToMacro,
			BoundingBox[] boundingBoxesArray) {

			int totalBoundingBoxes = boundingBoxesArray.Length;
			var connections = new Dictionary<BoundingBox, List<BoundingBox>>(totalBoundingBoxes);

			// Keyed by Min.x / Min.y rather than by box, since several boxes can start on
			// the same column/row (e.g. a stack of boxes against one vertical seam).
			var boxesByMinX = new Dictionary<int, List<BoundingBox>>(totalBoundingBoxes);
			var boxesByMinY = new Dictionary<int, List<BoundingBox>>(totalBoundingBoxes);

			// Exact-corner lookups for the two diagonal-only topologies.
			var byBottomLeftCorner = new Dictionary<(int x, int y), BoundingBox>(totalBoundingBoxes);
			var byBottomRightCorner = new Dictionary<(int x, int y), BoundingBox>(totalBoundingBoxes);

			foreach (var box in boundingBoxesArray) {
				AddToBucket(boxesByMinX, box.Min.X, box);
				AddToBucket(boxesByMinY, box.Min.Y, box);

				RegisterCorner(byBottomLeftCorner, (box.Min.X, box.Min.Y), box);
				RegisterCorner(byBottomRightCorner, (box.Max.X, box.Min.Y), box);
			}

			foreach (var box in boundingBoxesArray) {
				// Right — horizontal edge share
				if (boxesByMinX.TryGetValue(box.Max.X + 1, out var rightCandidates)) {
					foreach (var candidate in rightCandidates) {
						if (RangesOverlap(box.Min.Y, box.Max.Y, candidate.Min.Y, candidate.Max.Y)) {
							AddConnection(connections, box, candidate);
						}
					}
				}

				// Up — vertical edge share
				if (boxesByMinY.TryGetValue(box.Max.Y + 1, out var upCandidates)) {
					foreach (var candidate in upCandidates) {
						if (RangesOverlap(box.Min.X, box.Max.X, candidate.Min.X, candidate.Max.X)) {
							AddConnection(connections, box, candidate);
						}
					}
				}

				// UpRight — this box's top-right corner touches a neighbor's bottom-left corner
				if (byBottomLeftCorner.TryGetValue((box.Max.X + 1, box.Max.Y + 1), out var upRightNeighbor)) {
					AddConnection(connections, box, upRightNeighbor);
				}

				// UpLeft — this box's top-left corner touches a neighbor's bottom-right corner
				if (byBottomRightCorner.TryGetValue((box.Min.X - 1, box.Max.Y + 1), out var upLeftNeighbor)) {
					AddConnection(connections, box, upLeftNeighbor);
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

		private static void RegisterCorner(Dictionary<(int, int), BoundingBox> corners, (int, int) key, BoundingBox box) {
			if (corners.ContainsKey(key)) {
				// Two boxes claiming the same exact corner means the packer produced
				// overlapping rectangles — that's an upstream bug, surface it instead of
				// silently picking one.
				Debug.LogError($"ConnectionFinder: duplicate corner {key} across packed boxes — check the rectangle packer output.");
				return;
			}
			corners[key] = box;
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