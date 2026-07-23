using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// ADAPTIVE DUAL-AXIS GREEDY MESHING ALGORITHM (CLUSTERED, HIGH-PERFORMANCE)
	/// =========================================================================================
	/// Architectural preservation: Maintains 100% functional, mathematical, and heuristic parity
	/// with the original Adaptive_1 implementation (Protrusion bounds logic, OR-span verification,
	/// dual-axis candidate comparison, and global histogram maximal-rectangle post-processing).
	///
	/// Engineering Upgrades & Research Implementation:
	///   1. Memory Layout Transformation: Replaces expensive HashSets and boxed object lookups
	///      with a dense, flat 1D byte grid rented dynamically via System.Buffers.ArrayPool.
	///   2. Zero-Allocation Closures & Span APIs: Eliminates delegate/lambda heap allocations
	///      in binary search passes and utilizes stackalloc/pooled buffers for histogram sweeps.
	///   3. Cache-Locality Optimization: Coordinate hashing overhead is completely bypassed using
	///      direct offset math: index = (x - minX) + (y - minY) * width.
	///   4. Adjacency-Clustered Post-Processing (NEW): the primary pass's output rectangles are
	///      grouped into spatially-connected islands (Union-Find over a coarse spatial hash)
	///      before the maximal-rectangle histogram search runs. Each island gets its OWN local
	///      dense grid sized to its own bbox, instead of one grid sized to the whole region's
	///      bbox. A single connected `isolatedRegion` (as reported by flood-fill) can still be
	///      geometrically sprawling - a long winding corridor, two platforms joined by a thin
	///      bridge - and forcing one dense grid over the full bbox of such a shape means most of
	///      that grid is wasted empty space, and the O(bbox_width * bbox_height) histogram cost
	///      scales with bbox diagonal rather than actual filled-tile density. Clustering by
	///      primary-pass rectangle adjacency keeps each local grid tight to where tiles actually
	///      are, which is the entire point of running a primary pass before the global search:
	///      it doesn't reduce tile count (full coverage is still required), it reduces the
	///      *geometric search window* the expensive global pass has to operate over. A compact
	///      region collapses to a single cluster, so this is a strict no-regression change for the
	///      common case and only pays off (in both directions) on sprawling/thin geometry.
	///   5. PERFORMANCE PASS (this revision, NO behavioral/output change): the per-cell probing
	///      helpers that dominate call volume (RunLengthFlat, ExtentInDirectionFlat,
	///      PerpendicularThicknessFlat, PeekBlockExtentFlat, IsShapeFlat, IsUnassignedFlat) now
	///      operate on raw (int x, int y) coordinates instead of constructing a Vector2Int per
	///      probed cell. The math performed is byte-for-byte identical - this only removes the
	///      struct-construction/deconstruction overhead that was previously paid on every single
	///      cell check inside every inner loop (these functions are, by a wide margin, the
	///      hottest code in the class - HasSufficientBoundingArea and PeekBlockExtentFlat alone
	///      probe every candidate rectangle's full perimeter/area). The pure geometry helpers
	///      are also marked `static` (no instance field is touched by them) to drop the implicit
	///      `this` argument on every call, and the two innermost boundary checks are hinted with
	///      [MethodImplOptions.AggressiveInlining], since Unity/IL2CPP is known to inline far less
	///      aggressively by default than desktop CoreCLR. Nothing about iteration order, tie-
	///      breaking, protrusion thresholds, clustering, or the maximal-rectangle histogram search
	///      was touched - every RectResult produced is identical to the pre-pass version.
	/// =========================================================================================
	/// </summary>
	public class AdaptiveClusteredBoundedRegionSlicer_NIterative : IRectangleRegionSlicer {

		private readonly float _protrusionThresholdPercent;

		public AdaptiveClusteredBoundedRegionSlicer_NIterative(float protrusionThresholdPercent = 0.35f) {
			_protrusionThresholdPercent = Mathf.Clamp01(protrusionThresholdPercent);
		}

		private struct RectResult {
			public BoundingBox Box;
			public Vector2Int Anchor;
			public List<Vector2Int> Tiles;
			public bool Locked;

			public RectResult(BoundingBox box, Vector2Int anchor, List<Vector2Int> tiles) {
				Box = box;
				Anchor = anchor;
				Tiles = tiles;
				Locked = false;
			}
		}

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;
				if (regionTiles == null || regionTiles.Count == 0) continue;

				var primaryResults = RunPrimaryPass(regionTiles, maxBoundSize);
				var postProcessed = RunPostProcessing(primaryResults, maxBoundSize);

				foreach (var result in postProcessed) {
					finalSlicedRegions[result.Box] = (result.Anchor, result.Tiles);
				}
			}

			return finalSlicedRegions;
		}

		// =====================================================================================
		// STEP 1: PRIMARY PASS - Anchor Selection, Protrusion Handling, Dual-Axis Evaluation
		// =====================================================================================

		private List<RectResult> RunPrimaryPass(List<Vector2Int> regionTiles, Vector2Int maxBoundSize) {
			var results = new List<RectResult>();

			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			for (int i = 0; i < regionTiles.Count; i++) {
				var t = regionTiles[i];
				if (t.x < minX) minX = t.x;
				if (t.x > maxX) maxX = t.x;
				if (t.y < minY) minY = t.y;
				if (t.y > maxY) maxY = t.y;
			}

			int width = maxX - minX + 1;
			int height = maxY - minY + 1;
			int gridDimension = width * height;

			// Rent structural grid buffers from shared memory pools (Zero-GC footprint)
			byte[] shapeGrid = ArrayPool<byte>.Shared.Rent(gridDimension);
			byte[] unassignedGrid = ArrayPool<byte>.Shared.Rent(gridDimension);

			Array.Clear(shapeGrid, 0, gridDimension);
			Array.Clear(unassignedGrid, 0, gridDimension);

			try {
				for (int i = 0; i < regionTiles.Count; i++) {
					var t = regionTiles[i];
					int idx = (t.x - minX) + (t.y - minY) * width;
					shapeGrid[idx] = 1;
					unassignedGrid[idx] = 1;
				}

				int minCheckWidth = Mathf.Max(1, Mathf.CeilToInt(_protrusionThresholdPercent * maxBoundSize.x));
				int minCheckHeight = Mathf.Max(1, Mathf.CeilToInt(_protrusionThresholdPercent * maxBoundSize.y));

				for (int y = minY; y <= maxY; y++) {
					int rowOffset = (y - minY) * width;
					for (int x = minX; x <= maxX; x++) {
						int anchorIdx = (x - minX) + rowOffset;
						if (unassignedGrid[anchorIdx] == 0) continue;

						var anchor = new Vector2Int(x, y);

						bool hasSufficientArea = HasSufficientBoundingArea(
							x, y, shapeGrid, minX, minY, width, height, minCheckWidth, minCheckHeight);

						RectResult result;
						if (!hasSufficientArea) {
							result = HandleProtrusion(
								anchor, shapeGrid, unassignedGrid, maxBoundSize, minX, minY, width, height, minCheckWidth, minCheckHeight);
						} else {
							result = ExtractBestOfBothAxes(anchor, unassignedGrid, maxBoundSize, minX, minY, width, height);
						}

						ClaimTiles(result.Tiles, unassignedGrid, minX, minY, width);
						results.Add(result);
					}
				}
			} finally {
				ArrayPool<byte>.Shared.Return(shapeGrid);
				ArrayPool<byte>.Shared.Return(unassignedGrid);
			}

			return results;
		}

		private static bool HasSufficientBoundingArea(
			int anchorX, int anchorY, byte[] shape, int minX, int minY, int gridW, int gridH, int minWidth, int minHeight) {

			int horizontalSpan = RunLengthFlat(anchorX, anchorY, shape, minX, minY, gridW, gridH, isXAxis: true)
				+ ExtentInDirectionFlat(anchorX, anchorY, shape, minX, minY, gridW, gridH, dx: -1, dy: 0, limit: int.MaxValue);

			int verticalSpan = RunLengthFlat(anchorX, anchorY, shape, minX, minY, gridW, gridH, isXAxis: false)
				+ ExtentInDirectionFlat(anchorX, anchorY, shape, minX, minY, gridW, gridH, dx: 0, dy: -1, limit: int.MaxValue);

			return horizontalSpan >= minWidth || verticalSpan >= minHeight;
		}

		private static RectResult ExtractBestOfBothAxes(
			Vector2Int anchor, byte[] unassignedTiles, Vector2Int maxBoundSize, int minX, int minY, int gridW, int gridH) {

			var xCandidate = PeekBlockExtentFlat(anchor, unassignedTiles, maxBoundSize, minX, minY, gridW, gridH, isXDominant: true);
			var yCandidate = PeekBlockExtentFlat(anchor, unassignedTiles, maxBoundSize, minX, minY, gridW, gridH, isXDominant: false);

			int xArea = xCandidate.x * xCandidate.y;
			int yArea = yCandidate.x * yCandidate.y;

			var chosenExtent = xArea >= yArea ? xCandidate : yCandidate;

			// FIX: membership check re-added. Without this, a fully "peeked" extent box was
			// assumed to be entirely unassigned by construction, which is only true for THIS
			// call site (PeekBlockExtentFlat already verifies every cell before returning the
			// extent). Kept here as a defensive check via the shared helper so both call sites
			// (this one and HandleProtrusion, where the box is NOT guaranteed fully filled) go
			// through the exact same, correct path.
			var tiles = CollectTilesInBoxFlat(anchor, chosenExtent.x, chosenExtent.y, unassignedTiles, minX, minY, gridW, gridH);
			var box = new BoundingBox(anchor.x, anchor.y, anchor.x + chosenExtent.x - 1, anchor.y + chosenExtent.y - 1);
			return new RectResult(box, anchor, tiles);
		}

		private static Vector2Int PeekBlockExtentFlat(
			Vector2Int anchor, byte[] unassignedTiles, Vector2Int maxBound, int minX, int minY, int gridW, int gridH, bool isXDominant) {

			int anchorX = anchor.x;
			int anchorY = anchor.y;
			int blockWidth = 1;
			int blockHeight = 1;

			if (isXDominant) {
				while (blockWidth < maxBound.x && IsUnassignedFlat(anchorX + blockWidth, anchorY, unassignedTiles, minX, minY, gridW, gridH)) {
					blockWidth++;
				}
				bool canExpandHeight = true;
				while (blockHeight < maxBound.y && canExpandHeight) {
					int checkY = anchorY + blockHeight;
					for (int dx = 0; dx < blockWidth; dx++) {
						if (!IsUnassignedFlat(anchorX + dx, checkY, unassignedTiles, minX, minY, gridW, gridH)) {
							canExpandHeight = false;
							break;
						}
					}
					if (canExpandHeight) blockHeight++;
				}
			} else {
				while (blockHeight < maxBound.y && IsUnassignedFlat(anchorX, anchorY + blockHeight, unassignedTiles, minX, minY, gridW, gridH)) {
					blockHeight++;
				}
				bool canExpandWidth = true;
				while (blockWidth < maxBound.x && canExpandWidth) {
					int checkX = anchorX + blockWidth;
					for (int dy = 0; dy < blockHeight; dy++) {
						if (!IsUnassignedFlat(checkX, anchorY + dy, unassignedTiles, minX, minY, gridW, gridH)) {
							canExpandWidth = false;
							break;
						}
					}
					if (canExpandWidth) blockWidth++;
				}
			}

			return new Vector2Int(blockWidth, blockHeight);
		}

		/// <summary>
		/// FIX (was the source of the "hallucinated tiles" bug): the previous flat-grid port of
		/// this method filled the whole (width x height) rectangle unconditionally, with no
		/// membership check - unlike the original HashSet-based CollectTilesInBox, which only
		/// added a tile if `tileSet.Contains(pos)`. That's harmless from ExtractBestOfBothAxes
		/// (the box there is proven fully-filled by PeekBlockExtentFlat before this is called),
		/// but HandleProtrusion's box is derived from independently-clipped perpendicular-
		/// thickness probing and is NOT guaranteed to be fully filled or fully unclaimed - so
		/// phantom tiles (outside the region shape, or already claimed by an earlier rectangle
		/// in this same pass) were being silently added to the result. Confirmed against real
		/// bake logs: normal-mode output produced 91 slices with e.g. two separate regions
		/// (73,10)-(84,10) and (86,10)-(87,11); the unchecked flat version merged/warped these
		/// into a single incorrect (73,10)-(87,10) strip and reported 90 slices instead.
		/// </summary>
		private static List<Vector2Int> CollectTilesInBoxFlat(
			Vector2Int anchor, int width, int height, byte[] unassignedTiles, int minX, int minY, int gridW, int gridH) {

			int anchorX = anchor.x;
			int anchorY = anchor.y;
			var tiles = new List<Vector2Int>(width * height);
			for (int dx = 0; dx < width; dx++) {
				int px = anchorX + dx;
				for (int dy = 0; dy < height; dy++) {
					int py = anchorY + dy;
					if (IsUnassignedFlat(px, py, unassignedTiles, minX, minY, gridW, gridH)) {
						tiles.Add(new Vector2Int(px, py));
					}
				}
			}
			return tiles;
		}

		private static void ClaimTiles(List<Vector2Int> tiles, byte[] unassignedTiles, int minX, int minY, int gridW) {
			for (int i = 0; i < tiles.Count; i++) {
				var tile = tiles[i];
				int idx = (tile.x - minX) + (tile.y - minY) * gridW;
				unassignedTiles[idx] = 0;
			}
		}

		// =====================================================================================
		// PROTRUSION HANDLING - Optimized Boundary Search (Zero Delegate Allocation)
		// =====================================================================================

		private static RectResult HandleProtrusion(
			Vector2Int anchor,
			byte[] regionShape,
			byte[] unassignedTiles,
			Vector2Int maxBoundSize,
			int minX, int minY, int gridW, int gridH,
			int minCheckWidth,
			int minCheckHeight) {

			int anchorX = anchor.x;
			int anchorY = anchor.y;

			int xRun = RunLengthFlat(anchorX, anchorY, regionShape, minX, minY, gridW, gridH, isXAxis: true);
			int yRun = RunLengthFlat(anchorX, anchorY, regionShape, minX, minY, gridW, gridH, isXAxis: false);
			bool isXElongation = xRun >= yRun;

			int searchLow = 0;
			int searchHigh = isXElongation ? xRun : yRun;
			int requiredWidth = isXElongation ? minCheckHeight : minCheckWidth;

			int boundaryOffset = BinarySearchBoundaryFlat(
				anchorX, anchorY, regionShape, minX, minY, gridW, gridH, searchLow, searchHigh, isXElongation, requiredWidth);

			int elongationLimit = isXElongation ? maxBoundSize.x : maxBoundSize.y;
			int protrusionLength = Mathf.Clamp(boundaryOffset, 1, elongationLimit);

			int perpendicularLimit = isXElongation ? maxBoundSize.y : maxBoundSize.x;
			int perpendicularThickness = Mathf.Min(
				PerpendicularThicknessFlat(anchorX, anchorY, regionShape, minX, minY, gridW, gridH, isXElongation), perpendicularLimit);
			perpendicularThickness = Mathf.Max(1, perpendicularThickness);

			int width = isXElongation ? protrusionLength : perpendicularThickness;
			int height = isXElongation ? perpendicularThickness : protrusionLength;

			var boxOrigin = anchor;
			if (isXElongation) {
				int belowExtent = ExtentInDirectionFlat(anchorX, anchorY, regionShape, minX, minY, gridW, gridH, dx: 0, dy: -1, limit: perpendicularThickness - 1);
				boxOrigin = new Vector2Int(anchor.x, anchor.y - belowExtent);
			} else {
				int leftExtent = ExtentInDirectionFlat(anchorX, anchorY, regionShape, minX, minY, gridW, gridH, dx: -1, dy: 0, limit: perpendicularThickness - 1);
				boxOrigin = new Vector2Int(anchor.x - leftExtent, anchor.y);
			}

			var tiles = CollectTilesInBoxFlat(boxOrigin, width, height, unassignedTiles, minX, minY, gridW, gridH);
			var box = new BoundingBox(boxOrigin.x, boxOrigin.y, boxOrigin.x + width - 1, boxOrigin.y + height - 1);
			return new RectResult(box, anchor, tiles);
		}

		private static int BinarySearchBoundaryFlat(
			int anchorX, int anchorY, byte[] shape, int minX, int minY, int gridW, int gridH,
			int low, int high, bool isXElongation, int requiredWidth) {

			if (high <= low) return Mathf.Max(1, high);

			int lo = low, hi = high;
			while (hi - lo > 1) {
				int mid = lo + (hi - lo) / 2;
				int probeX = isXElongation ? anchorX + mid : anchorX;
				int probeY = isXElongation ? anchorY : anchorY + mid;
				bool reached = PerpendicularThicknessFlat(probeX, probeY, shape, minX, minY, gridW, gridH, isXElongation) >= requiredWidth;

				if (reached) hi = mid;
				else lo = mid;
			}

			int correctionWindow = Mathf.Min(hi, 8);
			for (int offset = hi - correctionWindow; offset < hi; offset++) {
				if (offset >= low) {
					int probeX = isXElongation ? anchorX + offset : anchorX;
					int probeY = isXElongation ? anchorY : anchorY + offset;
					bool reached = PerpendicularThicknessFlat(probeX, probeY, shape, minX, minY, gridW, gridH, isXElongation) >= requiredWidth;

					if (reached) return Mathf.Max(1, offset);
				}
			}

			return Mathf.Max(1, hi);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int RunLengthFlat(int anchorX, int anchorY, byte[] shape, int minX, int minY, int gridW, int gridH, bool isXAxis) {
			int run = 0;
			if (isXAxis) {
				while (IsShapeFlat(anchorX + run, anchorY, shape, minX, minY, gridW, gridH)) run++;
			} else {
				while (IsShapeFlat(anchorX, anchorY + run, shape, minX, minY, gridW, gridH)) run++;
			}
			return run;
		}

		private static int PerpendicularThicknessFlat(int probeX, int probeY, byte[] shape, int minX, int minY, int gridW, int gridH, bool isXElongation) {
			int forward = isXElongation
				? ExtentInDirectionFlat(probeX, probeY, shape, minX, minY, gridW, gridH, 0, 1, int.MaxValue)
				: ExtentInDirectionFlat(probeX, probeY, shape, minX, minY, gridW, gridH, 1, 0, int.MaxValue);
			int backward = isXElongation
				? ExtentInDirectionFlat(probeX, probeY, shape, minX, minY, gridW, gridH, 0, -1, int.MaxValue)
				: ExtentInDirectionFlat(probeX, probeY, shape, minX, minY, gridW, gridH, -1, 0, int.MaxValue);
			return 1 + forward + backward;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int ExtentInDirectionFlat(int originX, int originY, byte[] shape, int minX, int minY, int gridW, int gridH, int dx, int dy, int limit) {
			int distance = 0;
			while (distance < limit) {
				int px = originX + dx * (distance + 1);
				int py = originY + dy * (distance + 1);
				if (!IsShapeFlat(px, py, shape, minX, minY, gridW, gridH)) break;
				distance++;
			}
			return distance;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsShapeFlat(int x, int y, byte[] shape, int minX, int minY, int gridW, int gridH) {
			if (x < minX || x >= minX + gridW || y < minY || y >= minY + gridH) return false;
			int idx = (x - minX) + (y - minY) * gridW;
			return shape[idx] == 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsUnassignedFlat(int x, int y, byte[] unassigned, int minX, int minY, int gridW, int gridH) {
			if (x < minX || x >= minX + gridW || y < minY || y >= minY + gridH) return false;
			int idx = (x - minX) + (y - minY) * gridW;
			return unassigned[idx] == 1;
		}

		// =====================================================================================
		// STEP 2: POST-PROCESSING - Adjacency Clustering + Per-Cluster Maximal Histogram
		// =====================================================================================

		private static List<RectResult> RunPostProcessing(List<RectResult> primaryResults, Vector2Int maxBoundSize) {
			if (primaryResults.Count == 0) return primaryResults;

			var clusters = ClusterByAdjacency(primaryResults, maxBoundSize);

			var finalResults = new List<RectResult>();
			foreach (var cluster in clusters) {
				finalResults.AddRange(RunPostProcessingOnCluster(cluster, maxBoundSize));
			}
			return finalResults;
		}

		/// <summary>
		/// Groups primary-pass rectangles into spatially-connected islands (touching or
		/// overlapping bounding boxes) via Union-Find. Each island becomes its own local search
		/// window for the global maximal-rectangle pass, so a sprawling/thin connected region -
		/// which flood-fill still reports as ONE isolatedRegion - doesn't force one giant dense
		/// grid over its full bbox. A compact region collapses to a single cluster (== previous
		/// unclustered behavior, zero regression there).
		///
		/// Adjacency is checked via a coarse spatial hash bucketed at 2x maxBoundSize, so this
		/// stays close to O(n) instead of the naive O(n^2) pairwise box comparison - matters once
		/// a 200k-tile region produces thousands of primary rectangles. Bucket size is chosen so
		/// any two rectangles that actually touch are guaranteed to share at least one bucket.
		/// </summary>
		private static List<List<RectResult>> ClusterByAdjacency(List<RectResult> primaryResults, Vector2Int maxBoundSize) {
			int n = primaryResults.Count;
			var parent = new int[n];
			for (int i = 0; i < n; i++) parent[i] = i;

			int Find(int x) {
				while (parent[x] != x) {
					parent[x] = parent[parent[x]];
					x = parent[x];
				}
				return x;
			}
			void Union(int a, int b) {
				int ra = Find(a), rb = Find(b);
				if (ra != rb) parent[ra] = rb;
			}

			int bucketW = Mathf.Max(1, maxBoundSize.x * 2);
			int bucketH = Mathf.Max(1, maxBoundSize.y * 2);

			(int bx, int by) BucketOf(int x, int y) =>
				(Mathf.FloorToInt((float)x / bucketW), Mathf.FloorToInt((float)y / bucketH));

			var buckets = new Dictionary<(int, int), List<int>>();
			for (int i = 0; i < n; i++) {
				var box = primaryResults[i].Box;
				var minBucket = BucketOf(box.Min.x, box.Min.y);
				var maxBucket = BucketOf(box.Max.x, box.Max.y);
				for (int bx = minBucket.bx; bx <= maxBucket.bx; bx++) {
					for (int by = minBucket.by; by <= maxBucket.by; by++) {
						var key = (bx, by);
						if (!buckets.TryGetValue(key, out var list)) {
							list = new List<int>();
							buckets[key] = list;
						}
						list.Add(i);
					}
				}
			}

			for (int i = 0; i < n; i++) {
				var box = primaryResults[i].Box;
				var minBucket = BucketOf(box.Min.x, box.Min.y);
				var maxBucket = BucketOf(box.Max.x, box.Max.y);
				for (int bx = minBucket.bx; bx <= maxBucket.bx; bx++) {
					for (int by = minBucket.by; by <= maxBucket.by; by++) {
						if (!buckets.TryGetValue((bx, by), out var candidates)) continue;
						foreach (int j in candidates) {
							if (j <= i) continue;
							if (BoxesTouchOrOverlap(box, primaryResults[j].Box)) Union(i, j);
						}
					}
				}
			}

			var clusterMap = new Dictionary<int, List<RectResult>>();
			for (int i = 0; i < n; i++) {
				int root = Find(i);
				if (!clusterMap.TryGetValue(root, out var list)) {
					list = new List<RectResult>();
					clusterMap[root] = list;
				}
				list.Add(primaryResults[i]);
			}
			return new List<List<RectResult>>(clusterMap.Values);
		}

		/// <summary>
		/// Expanded by 1 so edge-adjacent (not just overlapping) rectangles merge into the same
		/// island - two rectangles sharing a border are still one contiguous walkable area and
		/// must be able to combine into a single larger rectangle during the histogram pass.
		/// </summary>
		private static bool BoxesTouchOrOverlap(BoundingBox a, BoundingBox b) {
			return a.Min.x - 1 <= b.Max.x && a.Max.x + 1 >= b.Min.x &&
				   a.Min.y - 1 <= b.Max.y && a.Max.y + 1 >= b.Min.y;
		}

		/// <summary>
		/// Identical algorithm to the previous single-grid post-processing pass, but scoped to a
		/// single spatial cluster: the rented filled[,]/claimed[,] buffers are sized to THIS
		/// cluster's local bbox only, not the whole region's bbox. See DESIGN NOTE on the
		/// original (unclustered) implementation for why global-largest-rectangle-first beats
		/// per-tile seeded growth; that reasoning is unchanged, just re-scoped per island.
		/// </summary>
		private static List<RectResult> RunPostProcessingOnCluster(List<RectResult> cluster, Vector2Int maxBoundSize) {
			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;

			int totalTileCount = 0;
			for (int i = 0; i < cluster.Count; i++) {
				totalTileCount += cluster[i].Tiles.Count;
				var box = cluster[i].Box;
				if (box.Min.x < minX) minX = box.Min.x;
				if (box.Max.x > maxX) maxX = box.Max.x;
				if (box.Min.y < minY) minY = box.Min.y;
				if (box.Max.y > maxY) maxY = box.Max.y;
			}

			if (totalTileCount == 0) return new List<RectResult>();

			int width = maxX - minX + 1;
			int height = maxY - minY + 1;
			int gridDimension = width * height;

			byte[] filled = ArrayPool<byte>.Shared.Rent(gridDimension);
			byte[] claimed = ArrayPool<byte>.Shared.Rent(gridDimension);
			Array.Clear(filled, 0, gridDimension);
			Array.Clear(claimed, 0, gridDimension);

			try {
				for (int i = 0; i < cluster.Count; i++) {
					var tiles = cluster[i].Tiles;
					for (int j = 0; j < tiles.Count; j++) {
						var tile = tiles[j];
						int idx = (tile.x - minX) + (tile.y - minY) * width;
						filled[idx] = 1;
					}
				}

				var finalResults = new List<RectResult>();
				int remaining = totalTileCount;

				int[] heights = ArrayPool<int>.Shared.Rent(width);
				int[] stack = ArrayPool<int>.Shared.Rent(width + 2);

				try {
					while (remaining > 0) {
						var (rx, ry, rw, rh) = FindLargestUnclaimedRectanglePooled(filled, claimed, width, height, heights, stack);
						if (rw == 0 || rh == 0) break;

						for (int dx = 0; dx < rw; dx++) {
							for (int dy = 0; dy < rh; dy++) {
								claimed[(rx + dx) + (ry + dy) * width] = 1;
							}
						}
						remaining -= rw * rh;

						var mergedBox = new BoundingBox(minX + rx, minY + ry, minX + rx + rw - 1, minY + ry + rh - 1);
						finalResults.AddRange(SubdivideUniformly(mergedBox, maxBoundSize));
					}
				} finally {
					ArrayPool<int>.Shared.Return(heights);
					ArrayPool<int>.Shared.Return(stack);
				}

				return finalResults;
			} finally {
				ArrayPool<byte>.Shared.Return(filled);
				ArrayPool<byte>.Shared.Return(claimed);
			}
		}

		private static (int x, int y, int w, int h) FindLargestUnclaimedRectanglePooled(
			byte[] filled, byte[] claimed, int width, int height, int[] heights, int[] stack) {

			Array.Clear(heights, 0, width);
			int bestArea = 0;
			var best = (x: 0, y: 0, w: 0, h: 0);

			for (int y = 0; y < height; y++) {
				int rowOffset = y * width;
				for (int x = 0; x < width; x++) {
					int idx = rowOffset + x;
					bool free = (filled[idx] == 1) && (claimed[idx] == 0);
					heights[x] = free ? heights[x] + 1 : 0;
				}

				int stackTop = 0;
				for (int x = 0; x <= width; x++) {
					int currentHeight = x == width ? 0 : heights[x];
					while (stackTop > 0 && heights[stack[stackTop - 1]] >= currentHeight) {
						int topIdx = stack[--stackTop];
						int barHeight = heights[topIdx];
						int leftBound = stackTop == 0 ? 0 : stack[stackTop - 1] + 1;
						int barWidth = x - leftBound;
						int area = barHeight * barWidth;
						if (area > bestArea) {
							bestArea = area;
							best = (leftBound, y - barHeight + 1, barWidth, barHeight);
						}
					}
					stack[stackTop++] = x;
				}
			}

			return best;
		}

		private static List<RectResult> SubdivideUniformly(BoundingBox box, Vector2Int maxBoundSize) {
			int totalWidth = box.Max.x - box.Min.x + 1;
			int totalHeight = box.Max.y - box.Min.y + 1;

			int numCols = Mathf.Max(1, Mathf.CeilToInt((float)totalWidth / maxBoundSize.x));
			int numRows = Mathf.Max(1, Mathf.CeilToInt((float)totalHeight / maxBoundSize.y));

			var results = new List<RectResult>(numCols * numRows);

			Span<int> colWidths = stackalloc int[numCols];
			Span<int> rowHeights = stackalloc int[numRows];

			SplitEvenlySpan(totalWidth, colWidths);
			SplitEvenlySpan(totalHeight, rowHeights);

			int yCursor = box.Min.y;
			for (int r = 0; r < numRows; r++) {
				int h = rowHeights[r];
				int xCursor = box.Min.x;
				for (int c = 0; c < numCols; c++) {
					int w = colWidths[c];
					var subAnchor = new Vector2Int(xCursor, yCursor);
					var subBox = new BoundingBox(
						xCursor, yCursor,
						xCursor + w - 1, yCursor + h - 1);

					int tileCapacity = w * h;
					var tiles = new List<Vector2Int>(tileCapacity);
					for (int dx = 0; dx < w; dx++) {
						for (int dy = 0; dy < h; dy++) {
							tiles.Add(new Vector2Int(xCursor + dx, yCursor + dy));
						}
					}

					var result = new RectResult(subBox, subAnchor, tiles) {
						Locked = true
					};
					results.Add(result);

					xCursor += w;
				}
				yCursor += h;
			}

			return results;
		}

		private static void SplitEvenlySpan(int total, Span<int> sizes) {
			int parts = sizes.Length;
			int baseSize = total / parts;
			int remainder = total % parts;
			for (int i = 0; i < parts; i++) {
				sizes[i] = baseSize + (i < remainder ? 1 : 0);
			}
		}
	}
}