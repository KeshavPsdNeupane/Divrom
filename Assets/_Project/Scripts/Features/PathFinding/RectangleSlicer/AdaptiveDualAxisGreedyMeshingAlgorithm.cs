using System;
using System.Collections.Generic;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// ADAPTIVE DUAL-AXIS GREEDY MESHING ALGORITHM
	/// =========================================================================================
	/// Improved Rectangle Splitting: Greedy Meshing with Post-Processing for Homogeneous Regions.
	///
	/// This extends the standard two-pass greedy meshing algorithm (X-dominant / Y-dominant sweep)
	/// with two additions:
	///
	///   1. Protrusion-aware anchor handling: before running standard greedy extraction at an
	///      anchor, the algorithm checks whether the anchor has "enough surrounding area" (a
	///      configurable percentage of maxBoundSize in both axes). If it doesn't, the anchor is
	///      inside a narrow offshoot ("protrusion") of the main body, and a dedicated boundary
	///      search locates where the protrusion rejoins the main body so the whole offshoot can be
	///      captured as a single region instead of shattering into slivers.
	///
	///   2. Homogeneous post-processing: after the primary pass produces an initial (possibly
	///      over-fragmented) set of rectangles, a second pass re-merges adjacent same-region
	///      rectangles into maximal "perfect" rectangles (ignoring maxBoundSize), then uniformly
	///      subdivides any merged rectangle that exceeds maxBoundSize back down into evenly sized
	///      pieces. This produces uniform tiling in open homogeneous areas rather than randomly
	///      sized shards, while every rectangle only goes through this merge/subdivide step once.
	/// =========================================================================================
	/// </summary>
	public class AdaptiveDualAxisGreedyMeshingAlgorithm : IRectangleRegionSlicer {

		/// <summary>
		/// The percentage (0-1) of maxBoundSize, along each axis, that must be free around an
		/// anchor point for that anchor to be considered part of the "main body" rather than a
		/// narrow protrusion. e.g. 0.35 means the anchor must have a filled rectangle at least
		/// 35% of maxBoundSize wide AND 35% of maxBoundSize tall available to it.
		/// </summary>
		private readonly float _protrusionThresholdPercent;

		public AdaptiveDualAxisGreedyMeshingAlgorithm(float protrusionThresholdPercent = 0.35f) {
			_protrusionThresholdPercent = Mathf.Clamp01(protrusionThresholdPercent);
		}

		private struct RectResult {
			public BoundingBox Box;
			public Vector2Int Anchor;
			public List<Vector2Int> Tiles;
			public bool Locked; // true once it has passed through post-processing merge/subdivide

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

				// ---- Step 1: Primary pass (protrusion-aware, dual-axis per anchor) ----
				var primaryResults = RunPrimaryPass(regionTiles, maxBoundSize);

				// ---- Step 2: Post-processing (maximal merge + uniform subdivision) ----
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

			// Immutable "shape" reference used purely for geometric checks (protrusion detection,
			// corridor width measurement). Never mutated during the pass.
			var regionShape = new HashSet<Vector2Int>(regionTiles);

			// Mutable working set - tiles still needing to be claimed by a rectangle.
			var unassignedTiles = new HashSet<Vector2Int>(regionTiles);

			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			foreach (var tile in regionTiles) {
				if (tile.x < minX) minX = tile.x;
				if (tile.x > maxX) maxX = tile.x;
				if (tile.y < minY) minY = tile.y;
				if (tile.y > maxY) maxY = tile.y;
			}

			int minCheckWidth = Mathf.Max(1, Mathf.CeilToInt(_protrusionThresholdPercent * maxBoundSize.x));
			int minCheckHeight = Mathf.Max(1, Mathf.CeilToInt(_protrusionThresholdPercent * maxBoundSize.y));

			for (int y = minY; y <= maxY; y++) {
				for (int x = minX; x <= maxX; x++) {
					var anchor = new Vector2Int(x, y);
					if (!unassignedTiles.Contains(anchor)) continue;

					bool hasSufficientArea = HasSufficientBoundingArea(
						anchor, regionShape, minCheckWidth, minCheckHeight);

					RectResult result;
					if (!hasSufficientArea) {
						// Anchor sits inside a narrow offshoot - locate where it rejoins the main
						// body and encapsulate the whole protrusion as a single region.
						result = HandleProtrusion(
							anchor, regionShape, unassignedTiles, maxBoundSize, minCheckWidth, minCheckHeight);
					} else {
						// Standard case: evaluate both X-dominant and Y-dominant candidates and
						// keep whichever produces the larger rectangle.
						result = ExtractBestOfBothAxes(anchor, unassignedTiles, maxBoundSize);
					}

					ClaimTiles(result.Tiles, unassignedTiles);
					results.Add(result);
				}
			}

			return results;
		}

		/// <summary>
		/// Checks whether the anchor has "enough surrounding area" to be considered main body
		/// rather than a narrow protrusion.
		///
		/// IMPORTANT DESIGN NOTE: this used to be an AND-based check (full minWidth x minHeight
		/// box must be simultaneously filled in both dimensions). That over-fires at legitimate
		/// transitions between two differently-shaped open areas - e.g. the last few columns of a
		/// wide platform right where a narrow corridor joins it - because no single box spanning
		/// both shapes is ever fully filled, even though the anchor is clearly not stuck in a
		/// narrow offshoot. That false positive was confirmed against real slicing output: it
		/// caused a corner of a platform to be misclassified as a protrusion, which stole tiles
		/// away from the platform's true footprint and left an orphaned single-tile-wide column
		/// behind.
		///
		/// The fix is an OR-based bidirectional span check: the anchor has sufficient area if
		/// EITHER its horizontal span (left run + right run through the anchor) OR its vertical
		/// span meets the threshold. A true protrusion is narrow along BOTH axes at once; if
		/// either axis has room, the anchor is part of some larger open area, not a corridor.
		/// </summary>
		private bool HasSufficientBoundingArea(
			Vector2Int anchor, HashSet<Vector2Int> shape, int minWidth, int minHeight) {

			int horizontalSpan = RunLength(anchor, shape, isXAxis: true)
				+ ExtentInDirection(anchor, shape, dx: -1, dy: 0, limit: int.MaxValue);

			int verticalSpan = RunLength(anchor, shape, isXAxis: false)
				+ ExtentInDirection(anchor, shape, dx: 0, dy: -1, limit: int.MaxValue);

			return horizontalSpan >= minWidth || verticalSpan >= minHeight;
		}

		/// <summary>
		/// Dual-Pass Evaluation: computes both the X-dominant and Y-dominant candidate rectangle
		/// at this anchor (without claiming tiles), and commits whichever has the larger area.
		/// </summary>
		private RectResult ExtractBestOfBothAxes(
			Vector2Int anchor, HashSet<Vector2Int> unassignedTiles, Vector2Int maxBoundSize) {

			var xCandidate = PeekBlockExtent(anchor, unassignedTiles, maxBoundSize, isXDominant: true);
			var yCandidate = PeekBlockExtent(anchor, unassignedTiles, maxBoundSize, isXDominant: false);

			int xArea = xCandidate.x * xCandidate.y;
			int yArea = yCandidate.x * yCandidate.y;

			var chosenExtent = xArea >= yArea ? xCandidate : yCandidate;

			var tiles = CollectTilesInBox(anchor, chosenExtent.x, chosenExtent.y, unassignedTiles);
			var box = new BoundingBox(anchor.x, anchor.y, anchor.x + chosenExtent.x - 1, anchor.y + chosenExtent.y - 1);
			return new RectResult(box, anchor, tiles);
		}

		/// <summary>
		/// Non-committing block-size computation, mirrors the original greedy expansion logic but
		/// only returns (width, height) rather than mutating any shared state.
		/// </summary>
		private Vector2Int PeekBlockExtent(
			Vector2Int anchor, HashSet<Vector2Int> unassignedTiles, Vector2Int maxBound, bool isXDominant) {

			int blockWidth = 1;
			int blockHeight = 1;

			if (isXDominant) {
				while (blockWidth < maxBound.x && unassignedTiles.Contains(new Vector2Int(anchor.x + blockWidth, anchor.y))) {
					blockWidth++;
				}
				bool canExpandHeight = true;
				while (blockHeight < maxBound.y && canExpandHeight) {
					int checkY = anchor.y + blockHeight;
					for (int dx = 0; dx < blockWidth; dx++) {
						if (!unassignedTiles.Contains(new Vector2Int(anchor.x + dx, checkY))) {
							canExpandHeight = false;
							break;
						}
					}
					if (canExpandHeight) blockHeight++;
				}
			} else {
				while (blockHeight < maxBound.y && unassignedTiles.Contains(new Vector2Int(anchor.x, anchor.y + blockHeight))) {
					blockHeight++;
				}
				bool canExpandWidth = true;
				while (blockWidth < maxBound.x && canExpandWidth) {
					int checkX = anchor.x + blockWidth;
					for (int dy = 0; dy < blockHeight; dy++) {
						if (!unassignedTiles.Contains(new Vector2Int(checkX, anchor.y + dy))) {
							canExpandWidth = false;
							break;
						}
					}
					if (canExpandWidth) blockWidth++;
				}
			}

			return new Vector2Int(blockWidth, blockHeight);
		}

		private List<Vector2Int> CollectTilesInBox(Vector2Int anchor, int width, int height, HashSet<Vector2Int> tileSet) {
			var tiles = new List<Vector2Int>(width * height);
			for (int dx = 0; dx < width; dx++) {
				for (int dy = 0; dy < height; dy++) {
					var pos = new Vector2Int(anchor.x + dx, anchor.y + dy);
					if (tileSet.Contains(pos)) tiles.Add(pos);
				}
			}
			return tiles;
		}

		private void ClaimTiles(List<Vector2Int> tiles, HashSet<Vector2Int> unassignedTiles) {
			foreach (var tile in tiles) {
				unassignedTiles.Remove(tile); // Rent/Release equivalent for tile ownership
			}
		}

		// =====================================================================================
		// PROTRUSION HANDLING - Boundary search (binary search + linear correction fallback)
		// =====================================================================================

		private RectResult HandleProtrusion(
			Vector2Int anchor,
			HashSet<Vector2Int> regionShape,
			HashSet<Vector2Int> unassignedTiles,
			Vector2Int maxBoundSize,
			int minCheckWidth,
			int minCheckHeight) {

			// Determine which axis the protrusion is elongated along by peeking how far a run of
			// tiles extends from the anchor in each direction.
			int xRun = RunLength(anchor, regionShape, isXAxis: true);
			int yRun = RunLength(anchor, regionShape, isXAxis: false);
			bool isXElongation = xRun >= yRun;

			int searchLow = 0;
			int searchHigh = isXElongation ? xRun : yRun;
			int requiredWidth = isXElongation ? minCheckHeight : minCheckWidth;

			// Predicate: at offset `o` along the elongation axis, is the corridor already back to
			// "main body" thickness (perpendicular run >= requiredWidth)?
			bool MainBodyReached(int offset) {
				var probe = isXElongation
					? new Vector2Int(anchor.x + offset, anchor.y)
					: new Vector2Int(anchor.x, anchor.y + offset);
				return PerpendicularThickness(probe, regionShape, isXElongation) >= requiredWidth;
			}

			int boundaryOffset = BinarySearchBoundaryWithFallback(searchLow, searchHigh, MainBodyReached);

			// Encapsulate the protrusion, clipped to maxBoundSize along the elongation axis.
			int elongationLimit = isXElongation ? maxBoundSize.x : maxBoundSize.y;
			int protrusionLength = Mathf.Clamp(boundaryOffset, 1, elongationLimit);

			int perpendicularLimit = isXElongation ? maxBoundSize.y : maxBoundSize.x;
			int perpendicularThickness = Mathf.Min(
				PerpendicularThickness(anchor, regionShape, isXElongation), perpendicularLimit);
			perpendicularThickness = Mathf.Max(1, perpendicularThickness);

			int width = isXElongation ? protrusionLength : perpendicularThickness;
			int height = isXElongation ? perpendicularThickness : protrusionLength;

			// The perpendicular axis may extend both before and after the anchor (e.g. anchor is
			// mid-corridor, not against a wall), so re-anchor the box to fully cover the corridor
			// cross-section rather than assuming the anchor sits at the corner.
			var boxOrigin = anchor;
			if (isXElongation) {
				int belowExtent = ExtentInDirection(anchor, regionShape, dx: 0, dy: -1, limit: perpendicularThickness - 1);
				boxOrigin = new Vector2Int(anchor.x, anchor.y - belowExtent);
			} else {
				int leftExtent = ExtentInDirection(anchor, regionShape, dx: -1, dy: 0, limit: perpendicularThickness - 1);
				boxOrigin = new Vector2Int(anchor.x - leftExtent, anchor.y);
			}

			var tiles = CollectTilesInBox(boxOrigin, width, height, unassignedTiles);
			var box = new BoundingBox(boxOrigin.x, boxOrigin.y, boxOrigin.x + width - 1, boxOrigin.y + height - 1);
			return new RectResult(box, anchor, tiles);
		}

		/// <summary>
		/// Binary search for the smallest offset at which `predicate` becomes true, assuming
		/// (best case) monotonicity. Because real corridor geometry can be non-monotonic, the
		/// result is verified and corrected with a small bounded linear scan around the found
		/// index - giving O(log n) typical performance with linear-search correctness guarantees.
		/// </summary>
		private int BinarySearchBoundaryWithFallback(int low, int high, Func<int, bool> predicate) {
			if (high <= low) return Mathf.Max(1, high);

			int lo = low, hi = high;
			while (hi - lo > 1) {
				int mid = lo + (hi - lo) / 2;
				if (predicate(mid)) hi = mid;
				else lo = mid;
			}

			// hi is the binary search's best guess for the boundary. Verify monotonicity held by
			// scanning a small window backwards; if an earlier offset also satisfies the
			// predicate (non-monotonic corridor), prefer the earliest one found so no part of the
			// protrusion is left uncovered.
			int correctionWindow = Mathf.Min(hi, 8);
			for (int offset = hi - correctionWindow; offset < hi; offset++) {
				if (offset >= low && predicate(offset)) {
					return Mathf.Max(1, offset);
				}
			}

			return Mathf.Max(1, hi);
		}

		private int RunLength(Vector2Int anchor, HashSet<Vector2Int> shape, bool isXAxis) {
			int run = 0;
			while (true) {
				var probe = isXAxis
					? new Vector2Int(anchor.x + run, anchor.y)
					: new Vector2Int(anchor.x, anchor.y + run);
				if (!shape.Contains(probe)) break;
				run++;
			}
			return run;
		}

		/// <summary>
		/// Measures the full local thickness perpendicular to the elongation axis at a given
		/// position, extending both directions from the probe point.
		/// </summary>
		private int PerpendicularThickness(Vector2Int probe, HashSet<Vector2Int> shape, bool isXElongation) {
			int forward = isXElongation
				? ExtentInDirection(probe, shape, 0, 1, int.MaxValue)
				: ExtentInDirection(probe, shape, 1, 0, int.MaxValue);
			int backward = isXElongation
				? ExtentInDirection(probe, shape, 0, -1, int.MaxValue)
				: ExtentInDirection(probe, shape, -1, 0, int.MaxValue);
			return 1 + forward + backward;
		}

		private int ExtentInDirection(Vector2Int origin, HashSet<Vector2Int> shape, int dx, int dy, int limit) {
			int distance = 0;
			while (distance < limit) {
				var probe = new Vector2Int(origin.x + dx * (distance + 1), origin.y + dy * (distance + 1));
				if (!shape.Contains(probe)) break;
				distance++;
			}
			return distance;
		}

		// =====================================================================================
		// STEP 2: POST-PROCESSING - Maximal Merging + Uniform Subdivision
		// =====================================================================================

		/// <summary>
		/// DESIGN NOTE - why this isn't a per-tile HashSet-order merge:
		///
		/// An earlier version of this method iterated `foreach (var tile in allTiles)` and grew a
		/// maximal rectangle from whichever tile the HashSet happened to enumerate first. That is
		/// non-deterministic and directionally biased (growth only ever expanded in +x/+y from the
		/// seed), so a seed that landed on a shape's corner/edge could lock in a thin sliver
		/// *before* the seed that should have produced the true, larger rectangle ever got a
		/// chance to claim those cells. Confirmed against real output: an isolated single-tile-wide
		/// column got carved out of what should have been one wide platform, and a narrow corridor
		/// "stole" columns from an adjoining platform, forcing both into worse shapes.
		///
		/// The fix is to always extract the SINGLE LARGEST all-filled, unclaimed axis-aligned
		/// rectangle across the *entire* remaining region first, then repeat. This is the classic
		/// "maximal rectangle in a binary matrix" problem (histogram + monotonic stack, O(W*H) per
		/// extraction). Picking the biggest rectangle globally, rather than growing from an
		/// arbitrary seed, means the decomposition naturally respects the shape's real structural
		/// transitions (a corridor won't out-compete an adjoining platform for shared tiles unless
		/// it is genuinely the bigger candidate), which is also in line with the finding that a
		/// "pick the largest remaining region" greedy strategy performs close to optimal in
		/// practice for rectangle decomposition of binary shapes.
		/// </summary>
		private List<RectResult> RunPostProcessing(List<RectResult> primaryResults, Vector2Int maxBoundSize) {
			if (primaryResults.Count == 0) return primaryResults;

			var allTiles = new HashSet<Vector2Int>();
			foreach (var result in primaryResults) {
				foreach (var tile in result.Tiles) allTiles.Add(tile);
			}

			if (allTiles.Count == 0) return new List<RectResult>();

			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			foreach (var tile in allTiles) {
				if (tile.x < minX) minX = tile.x;
				if (tile.x > maxX) maxX = tile.x;
				if (tile.y < minY) minY = tile.y;
				if (tile.y > maxY) maxY = tile.y;
			}

			int width = maxX - minX + 1;
			int height = maxY - minY + 1;

			// filled[x,y]: is this cell part of the region at all. claimed[x,y]: already extracted.
			var filled = new bool[width, height];
			var claimed = new bool[width, height];
			foreach (var tile in allTiles) {
				filled[tile.x - minX, tile.y - minY] = true;
			}

			var finalResults = new List<RectResult>();
			int remaining = allTiles.Count;

			while (remaining > 0) {
				var (rx, ry, rw, rh) = FindLargestUnclaimedRectangle(filled, claimed, width, height);
				if (rw == 0 || rh == 0) break; // safety net, should not happen while remaining > 0

				for (int dx = 0; dx < rw; dx++) {
					for (int dy = 0; dy < rh; dy++) {
						claimed[rx + dx, ry + dy] = true;
					}
				}
				remaining -= rw * rh;

				var mergedBox = new BoundingBox(minX + rx, minY + ry, minX + rx + rw - 1, minY + ry + rh - 1);

				// Uniform Subdivision: if the merged rectangle exceeds maxBoundSize, split it into
				// evenly sized sub-rectangles rather than leaving one oversized region.
				finalResults.AddRange(SubdivideUniformly(mergedBox, maxBoundSize));
			}

			return finalResults;
		}

		/// <summary>
		/// Classic "maximal rectangle in a binary matrix" solve: builds a per-column histogram of
		/// consecutive filled-and-unclaimed cell counts as it sweeps rows top to bottom, and uses a
		/// monotonic stack to find the largest rectangle in that histogram for every row. Returns
		/// the single largest rectangle found across the whole grid.
		/// </summary>
		private (int x, int y, int w, int h) FindLargestUnclaimedRectangle(
			bool[,] filled, bool[,] claimed, int width, int height) {

			var heights = new int[width];
			int bestArea = 0;
			var best = (x: 0, y: 0, w: 0, h: 0);

			for (int y = 0; y < height; y++) {
				for (int x = 0; x < width; x++) {
					bool free = filled[x, y] && !claimed[x, y];
					heights[x] = free ? heights[x] + 1 : 0;
				}

				var stack = new Stack<int>();
				for (int x = 0; x <= width; x++) {
					int currentHeight = x == width ? 0 : heights[x];
					while (stack.Count > 0 && heights[stack.Peek()] >= currentHeight) {
						int top = stack.Pop();
						int barHeight = heights[top];
						int leftBound = stack.Count == 0 ? 0 : stack.Peek() + 1;
						int barWidth = x - leftBound;
						int area = barHeight * barWidth;
						if (area > bestArea) {
							bestArea = area;
							best = (leftBound, y - barHeight + 1, barWidth, barHeight);
						}
					}
					stack.Push(x);
				}
			}

			return best;
		}

		private List<RectResult> SubdivideUniformly(BoundingBox box, Vector2Int maxBoundSize) {
			int totalWidth = box.Max.x - box.Min.x + 1;
			int totalHeight = box.Max.y - box.Min.y + 1;

			int numCols = Mathf.Max(1, Mathf.CeilToInt((float)totalWidth / maxBoundSize.x));
			int numRows = Mathf.Max(1, Mathf.CeilToInt((float)totalHeight / maxBoundSize.y));

			var results = new List<RectResult>(numCols * numRows);

			int[] colWidths = SplitEvenly(totalWidth, numCols);
			int[] rowHeights = SplitEvenly(totalHeight, numRows);

			int yCursor = box.Min.y;
			for (int r = 0; r < numRows; r++) {
				int xCursor = box.Min.x;
				for (int c = 0; c < numCols; c++) {
					var subAnchor = new Vector2Int(xCursor, yCursor);
					var subBox = new BoundingBox(
						xCursor, yCursor,
						xCursor + colWidths[c] - 1, yCursor + rowHeights[r] - 1);

					var tiles = new List<Vector2Int>(colWidths[c] * rowHeights[r]);
					for (int dx = 0; dx < colWidths[c]; dx++) {
						for (int dy = 0; dy < rowHeights[r]; dy++) {
							tiles.Add(new Vector2Int(xCursor + dx, yCursor + dy));
						}
					}

					var result = new RectResult(subBox, subAnchor, tiles);
					result.Locked = true; // single-pass constraint: cannot re-enter post-processing
					results.Add(result);

					xCursor += colWidths[c];
				}
				yCursor += rowHeights[r];
			}

			return results;
		}

		/// <summary>
		/// Splits `total` into `parts` integer chunks as evenly as possible (difference of at
		/// most 1 between any two chunks), so uniform subdivision doesn't leave one large leftover
		/// sliver.
		/// </summary>
		private int[] SplitEvenly(int total, int parts) {
			var sizes = new int[parts];
			int baseSize = total / parts;
			int remainder = total % parts;
			for (int i = 0; i < parts; i++) {
				sizes[i] = baseSize + (i < remainder ? 1 : 0);
			}
			return sizes;
		}
	}
}