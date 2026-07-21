using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// Top-level meshing constants shared across region-slicing strategies (kept outside the
	/// algorithm class itself, per spec, since aspect-ratio classification is a concept other
	/// slicers/consumers may also want to reference).
	/// </summary>
	public static class MeshingConstants {

		/// <summary>
		/// A block qualifies as a narrow structural "strip" (rather than a bulk open region)
		/// once its long side is at least this many times its short side.
		/// </summary>
		public const float STRIP_ASPECT_RATIO_THRESHOLD = 3.0f;
	}

	/// <summary>
	/// =========================================================================================
	/// AREA-BOUNDED GREEDY MESHING ALGORITHM
	/// (V3: BINARY GRID & MEMORY STRIDE OPTIMIZED  +  V4: TWO-TIER SLICING & GRAND FINALE)
	/// =========================================================================================
	/// Retains the single-pass cascading and squarified sequencing logic, and the contiguous 1D
	/// Binary Grid (bool[]) from V3, which replaces pointer-chasing HashSet lookups with
	/// sequential/strided memory reads (`index = y * width + x`).
	///
	/// Correctness Note (grid bounds, carried over from V3): HashSet.Contains() returns false
	/// for free on any coordinate outside the region. A flat array doesn't get that for free —
	/// IsColumnClear/IsRowClear explicitly bounds-check the target coordinate against
	/// [minX,maxX]/[minY,maxY] before indexing, so a probe one column past the region's real
	/// edge reports "blocked" instead of silently wrapping into an unrelated row.
	///
	/// -----------------------------------------------------------------------------------------
	/// V4: TWO-TIER SLICING SCHEDULE (erosion core-mask anchoring)
	/// -----------------------------------------------------------------------------------------
	/// Classification happens during the sweep, not after it. An earlier version of this pass
	/// compared each finished rectangle's size against maxBoundSize post-hoc — that's a weak
	/// signal, since a block can be legitimately smaller than maxBoundSize just because it hit a
	/// real wall, not because it's a corridor. Misclassifying those as strips fed them into the
	/// Grand Finale, which could then merge them across sections they had no business touching.
	///
	/// Instead, ExecuteMeshingSweep computes a morphological erosion "core mask" per region — a
	/// tile is core only if its full 3x3 neighborhood is inside the region — and runs two raster
	/// passes:
	///
	/// Phase A (core anchors only): the classic greedy-meshing failure mode is anchoring into a
	/// thin corridor before the sweep ever reaches the larger open area beside it, fragmenting
	/// what should be one big rectangle. Restricting anchors to core tiles means the bulk area
	/// claims its territory first; corridors narrower than 3 tiles fail the core test everywhere
	/// along their length and simply never get to anchor. Blocks from this phase go straight into
	/// the primary/"bedrock" container (Phase 1) and are never revisited, merged, or split again —
	/// modulo one defensive re-check against STRIP_ASPECT_RATIO_THRESHOLD, in case cascading
	/// growth stretched one into a strip shape anyway.
	///
	/// Phase B (unrestricted mop-up): whatever Phase A left unclaimed — the protrusions and
	/// corridors it skipped as anchors — is swept normally and deferred straight into the
	/// secondary strip/blob container (Phase 2) for the Grand Finale.
	///
	/// -----------------------------------------------------------------------------------------
	/// V4: GRAND FINALE (iterative merge + balanced split)
	/// -----------------------------------------------------------------------------------------
	/// Runs only against the strip/blob container, in up to GRAND_FINALE_MAX_ITERATIONS sweep
	/// passes:
	///
	///  - Each pass scans for adjacent blocks that share a full contact edge with matching
	///    perpendicular dimension, and share the same major axis/orientation (a roughly-square
	///    "blob" is orientation-neutral and can merge with either).
	///  - A merge is only accepted if the resulting dimension along the merge axis stays within
	///    GRAND_FINALE_MERGE_SIZE_FACTOR * maxBoundSize — intentional slack, not a hard ceiling,
	///    since this is an area-based algorithm rather than a rigid fixed-grid bound system.
	///  - Because bedrock never enters this container, "never merge into bedrock" is enforced
	///    structurally rather than by an explicit runtime check.
	///  - If an accepted merge still exceeds the *raw* maxBoundSize on its long axis (i.e. it
	///    only fit because of the tolerance slack above), it is immediately re-split down the
	///    middle into two balanced halves rather than left as one oversized block paired with an
	///    awkward splinter.
	///  - The loop stops early once a full pass produces zero merges.
	/// =========================================================================================
	/// </summary>
	public class AreaBoundedGreedyMeshingAlgorithm : IRectangleRegionSlicer {

		// ===== Grand Finale tuning constants (class-level, per spec) =====
		private const float GRAND_FINALE_MERGE_SIZE_FACTOR = 1.75f;
		private const int GRAND_FINALE_MAX_ITERATIONS = 8;

		private enum Orientation { Horizontal, Vertical, Neutral }

		/// <summary>
		/// Intermediate representation used between the raster sweep, the bedrock/strip
		/// classification pass, and the Grand Finale merger. Deliberately carries its own
		/// min/max/tiles rather than relying on BoundingBox's (unknown-to-us) property surface —
		/// it's only converted into a BoundingBox at the very end, once geometry is final.
		/// </summary>
		private class SliceBlock {
			public int MinX, MinY, MaxX, MaxY;
			public Vector2Int Anchor;
			public List<Vector2Int> Tiles;

			public int Width => MaxX - MinX + 1;
			public int Height => MaxY - MinY + 1;

			public Orientation Orient {
				get {
					if (Width > Height) return Orientation.Horizontal;
					if (Height > Width) return Orientation.Vertical;
					return Orientation.Neutral;
				}
			}

			public bool IsStrip =>
				Mathf.Max(Width, Height) / (float)Mathf.Min(Width, Height) >= MeshingConstants.STRIP_ASPECT_RATIO_THRESHOLD;
		}

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var bedrock = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			var pendingStripsAndBlobs = new List<SliceBlock>();

			// ----- Phase 1 (commit) + Phase 2 (defer) -----
			// Classification happens by construction now, not by comparing finished rectangle
			// sizes against maxBoundSize after the fact — see ExecuteMeshingSweep's core-anchor
			// pass below. That's what stops the bulk area from being fragmented whenever it's
			// legitimately smaller than maxBoundSize (it hit a real wall) rather than actually
			// being a corridor.
			foreach (var kvp in isolatedRegions) {
				var (coreBlocks, fallbackBlocks) = ExecuteMeshingSweep(kvp.Value, maxBoundSize);

				foreach (var block in coreBlocks) {
					// Defensive re-check only: a core-anchored block is bulky by construction,
					// but if cascading growth still stretched it into a strip shape, route it to
					// Phase 2 rather than trust the anchor phase blindly.
					if (block.IsStrip) {
						pendingStripsAndBlobs.Add(block);
					} else {
						var box = new BoundingBox(block.MinX, block.MinY, block.MaxX, block.MaxY);
						bedrock[box] = (block.Anchor, block.Tiles);
					}
				}

				// Fallback-phase blocks are exactly the protrusions/corridors Phase A skipped as
				// anchors — always deferred, no size check needed.
				pendingStripsAndBlobs.AddRange(fallbackBlocks);
			}

			// ----- Grand Finale: iterative merge + balanced split -----
			var finalizedStripsAndBlobs = RunGrandFinale(pendingStripsAndBlobs, maxBoundSize);

			// ----- Combine: bedrock is untouched, strips/blobs are the post-merge result -----
			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>(bedrock);
			foreach (var block in finalizedStripsAndBlobs) {
				var box = new BoundingBox(block.MinX, block.MinY, block.MaxX, block.MaxY);
				finalSlicedRegions[box] = (block.Anchor, block.Tiles);
			}

			return finalSlicedRegions;
		}

		// =====================================================================================
		// GRAND FINALE
		// =====================================================================================

		private List<SliceBlock> RunGrandFinale(List<SliceBlock> blocks, Vector2Int maxBoundSize) {
			int iterations = 0;

			while (iterations < GRAND_FINALE_MAX_ITERATIONS) {
				var consumed = new HashSet<int>();
				var nextPass = new List<SliceBlock>(blocks.Count);
				bool mergedAnything = false;

				for (int i = 0; i < blocks.Count; i++) {
					if (consumed.Contains(i)) continue;

					var current = blocks[i];
					bool foundPartner = false;

					for (int j = i + 1; j < blocks.Count; j++) {
						if (consumed.Contains(j)) continue;

						var merged = TryMergeAdjacent(current, blocks[j], maxBoundSize);
						if (merged == null) continue;

						consumed.Add(j);
						nextPass.AddRange(ApplyBalancedSplitIfOversized(merged, maxBoundSize));
						mergedAnything = true;
						foundPartner = true;
						break;
					}

					if (!foundPartner) nextPass.Add(current);
				}

				blocks = nextPass;
				iterations++;
				if (!mergedAnything) break;
			}

			return blocks;
		}

		// Neighbor & dimension matching: adjacent along one axis, full contact edge with matching
		// perpendicular dimension, compatible orientation. Returns the merge candidate only if it
		// also clears the GRAND_FINALE_MERGE_SIZE_FACTOR slack check; otherwise null (no merge).
		private SliceBlock TryMergeAdjacent(SliceBlock a, SliceBlock b, Vector2Int maxBoundSize) {
			if (!OrientationsCompatible(a.Orient, b.Orient)) return null;

			// Horizontal neighbor (side by side along X): contact edge runs the full height.
			if (a.MinY == b.MinY && a.MaxY == b.MaxY) {
				if (a.MaxX + 1 == b.MinX) return BuildMergeCandidate(a, b, mergeAxisIsX: true, maxBoundSize);
				if (b.MaxX + 1 == a.MinX) return BuildMergeCandidate(b, a, mergeAxisIsX: true, maxBoundSize);
			}

			// Vertical neighbor (stacked along Y): contact edge runs the full width.
			if (a.MinX == b.MinX && a.MaxX == b.MaxX) {
				if (a.MaxY + 1 == b.MinY) return BuildMergeCandidate(a, b, mergeAxisIsX: false, maxBoundSize);
				if (b.MaxY + 1 == a.MinY) return BuildMergeCandidate(b, a, mergeAxisIsX: false, maxBoundSize);
			}

			return null;
		}

		private bool OrientationsCompatible(Orientation a, Orientation b) {
			// A roughly-square blob (Neutral) has no strong major axis, so it's free to merge
			// with a strip of either orientation. Two oriented strips must match.
			if (a == Orientation.Neutral || b == Orientation.Neutral) return true;
			return a == b;
		}

		// `first` is the lower-coordinate block along the merge axis, `second` the higher one.
		private SliceBlock BuildMergeCandidate(SliceBlock first, SliceBlock second, bool mergeAxisIsX, Vector2Int maxBoundSize) {
			int minX = Mathf.Min(first.MinX, second.MinX);
			int minY = Mathf.Min(first.MinY, second.MinY);
			int maxX = Mathf.Max(first.MaxX, second.MaxX);
			int maxY = Mathf.Max(first.MaxY, second.MaxY);

			int mergedDimension = mergeAxisIsX ? (maxX - minX + 1) : (maxY - minY + 1);
			int budget = mergeAxisIsX ? maxBoundSize.x : maxBoundSize.y;

			// Merge-eligibility slack, not a hard ceiling: lets otherwise-good merges through
			// even when they overshoot maxBoundSize a bit, trusting ApplyBalancedSplitIfOversized
			// below to bring the result back into line afterward.
			if (mergedDimension > budget * GRAND_FINALE_MERGE_SIZE_FACTOR) return null;

			var tiles = new List<Vector2Int>(first.Tiles.Count + second.Tiles.Count);
			tiles.AddRange(first.Tiles);
			tiles.AddRange(second.Tiles);

			return new SliceBlock {
				MinX = minX,
				MinY = minY,
				MaxX = maxX,
				MaxY = maxY,
				Anchor = new Vector2Int(minX, minY),
				Tiles = tiles
			};
		}

		// Balanced 50/50 splitting: a merge that cleared the tolerance check above but still
		// exceeds the *raw* maxBoundSize on its long axis gets cut evenly down the middle rather
		// than left as one oversized block. Both halves are guaranteed solid rectangles, since
		// the merged block is itself the union of two adjacent solid rectangles with no gaps.
		private List<SliceBlock> ApplyBalancedSplitIfOversized(SliceBlock block, Vector2Int maxBoundSize) {
			bool overWidth = block.Width > maxBoundSize.x;
			bool overHeight = block.Height > maxBoundSize.y;

			if (!overWidth && !overHeight) return new List<SliceBlock> { block };

			// Split along whichever axis is actually over budget; if somehow both are, split
			// along the longer one.
			bool splitAlongX = overWidth && (!overHeight || block.Width >= block.Height);

			if (splitAlongX) {
				int leftWidth = block.Width / 2;
				int splitX = block.MinX + leftWidth - 1;

				var left = BuildRectBlock(block.MinX, block.MinY, splitX, block.MaxY);
				var right = BuildRectBlock(splitX + 1, block.MinY, block.MaxX, block.MaxY);
				return new List<SliceBlock> { left, right };
			} else {
				int bottomHeight = block.Height / 2;
				int splitY = block.MinY + bottomHeight - 1;

				var bottom = BuildRectBlock(block.MinX, block.MinY, block.MaxX, splitY);
				var top = BuildRectBlock(block.MinX, splitY + 1, block.MaxX, block.MaxY);
				return new List<SliceBlock> { bottom, top };
			}
		}

		private SliceBlock BuildRectBlock(int minX, int minY, int maxX, int maxY) {
			var tiles = new List<Vector2Int>((maxX - minX + 1) * (maxY - minY + 1));
			for (int y = minY; y <= maxY; y++)
				for (int x = minX; x <= maxX; x++)
					tiles.Add(new Vector2Int(x, y));

			return new SliceBlock {
				MinX = minX,
				MinY = minY,
				MaxX = maxX,
				MaxY = maxY,
				Anchor = new Vector2Int(minX, minY),
				Tiles = tiles
			};
		}

		// =====================================================================================
		// RASTER SWEEP (V3 logic, unchanged — now emits SliceBlock instead of writing straight
		// into a BoundingBox-keyed dictionary, since classification happens one layer up in
		// Slice())
		// =====================================================================================

		// Erosion core mask: a tile is "core" only if the full 3x3 neighborhood around it is
		// inside the region (in-bounds and unclaimed). This is the standard morphological
		// erosion test — anything with a missing neighbor, including every tile in a corridor
		// narrower than 3 tiles wide, fails it and is left non-core. Anchoring Phase A exclusively
		// on core tiles means the sweep can't lock onto a thin corridor before it ever reaches the
		// bulk area sitting next to it — the classic greedy-meshing failure mode this two-phase
		// split exists to avoid.
		private bool[] ComputeCoreMask(bool[] unassignedGrid, int gridWidth, int gridHeight) {
			var core = new bool[gridWidth * gridHeight];

			for (int y = 0; y < gridHeight; y++) {
				for (int x = 0; x < gridWidth; x++) {
					int idx = y * gridWidth + x;
					if (!unassignedGrid[idx]) continue;

					bool isCore = true;
					for (int dy = -1; dy <= 1 && isCore; dy++) {
						for (int dx = -1; dx <= 1; dx++) {
							int nx = x + dx, ny = y + dy;
							if (nx < 0 || nx >= gridWidth || ny < 0 || ny >= gridHeight || !unassignedGrid[ny * gridWidth + nx]) {
								isCore = false;
								break;
							}
						}
					}
					core[idx] = isCore;
				}
			}

			return core;
		}

		// Two-phase raster sweep, replacing the single-pass V3/early-V4 version.
		// Phase A anchors only on core (bulk-body) tiles, letting the big open area claim its
		// territory first via the same cascading squarified expansion as before. Phase B then
		// mops up whatever's left unclaimed — exactly the protrusions/corridors Phase A skipped
		// as anchors, plus any sliver the erosion test ate entirely. Both phases share the same
		// grid, so Phase B only ever sees tiles Phase A didn't already claim.
		private (List<SliceBlock> coreBlocks, List<SliceBlock> fallbackBlocks) ExecuteMeshingSweep(
			List<Vector2Int> regionTiles,
			Vector2Int maxBoundSize) {

			var coreBlocks = new List<SliceBlock>();
			var fallbackBlocks = new List<SliceBlock>();

			// Establish iteration bounds to frame our local 2D space
			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			foreach (var tile in regionTiles) {
				if (tile.x < minX) minX = tile.x;
				if (tile.x > maxX) maxX = tile.x;
				if (tile.y < minY) minY = tile.y;
				if (tile.y > maxY) maxY = tile.y;
			}

			// Flatten the 2D local space into a 1D contiguous array.
			int gridWidth = (maxX - minX) + 1;
			int gridHeight = (maxY - minY) + 1;
			bool[] unassignedGrid = new bool[gridWidth * gridHeight];

			foreach (var tile in regionTiles) {
				unassignedGrid[(tile.y - minY) * gridWidth + (tile.x - minX)] = true;
			}

			bool[] coreMask = ComputeCoreMask(unassignedGrid, gridWidth, gridHeight);

			// Phase A: anchor only on core tiles (Bottom-to-Top, Left-to-Right, same order as before).
			for (int y = minY; y <= maxY; y++) {
				for (int x = minX; x <= maxX; x++) {
					int gridIdx = (y - minY) * gridWidth + (x - minX);
					if (!unassignedGrid[gridIdx] || !coreMask[gridIdx]) continue;

					var anchorPos = new Vector2Int(x, y);
					ExtractOptimalCascadingBlock(
						anchorPos, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, maxBoundSize, coreBlocks);
				}
			}

			// Phase B: unrestricted mop-up over whatever Phase A left unclaimed.
			for (int y = minY; y <= maxY; y++) {
				for (int x = minX; x <= maxX; x++) {
					int gridIdx = (y - minY) * gridWidth + (x - minX);
					if (!unassignedGrid[gridIdx]) continue;

					var anchorPos = new Vector2Int(x, y);
					ExtractOptimalCascadingBlock(
						anchorPos, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, maxBoundSize, fallbackBlocks);
				}
			}

			return (coreBlocks, fallbackBlocks);
		}

		private void ExtractOptimalCascadingBlock(
			Vector2Int anchor,
			bool[] unassignedGrid,
			int gridWidth,
			int gridHeight,
			int minX,
			int minY,
			int maxX,
			int maxY,
			Vector2Int baseBound,
			List<SliceBlock> sweepResults) {

			int blockWidth = 1;
			int blockHeight = 1;

			int currentMaxWidth = baseBound.x;
			int currentMaxHeight = baseBound.y;

			bool isActivelyExpanding = true;
			bool xBlockedByWall = false;
			bool yBlockedByWall = false;

			while (isActivelyExpanding) {
				isActivelyExpanding = false;

				bool xGoesFirst = ShouldExpandXFirst(
					anchor, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY,
					blockWidth, blockHeight, currentMaxWidth, currentMaxHeight,
					xBlockedByWall, yBlockedByWall);

				if (xGoesFirst) {
					if (TryExpandX(anchor, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, ref blockWidth, blockHeight, currentMaxWidth, ref xBlockedByWall))
						isActivelyExpanding = true;

					int xResidue = Mathf.Max(0, currentMaxWidth - blockWidth);
					currentMaxHeight = baseBound.y + xResidue;

					if (TryExpandY(anchor, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, blockWidth, ref blockHeight, currentMaxHeight, ref yBlockedByWall))
						isActivelyExpanding = true;

					int yResidue = Mathf.Max(0, currentMaxHeight - blockHeight);
					currentMaxWidth = baseBound.x + yResidue;
				} else {
					if (TryExpandY(anchor, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, blockWidth, ref blockHeight, currentMaxHeight, ref yBlockedByWall))
						isActivelyExpanding = true;

					int yResidue = Mathf.Max(0, currentMaxHeight - blockHeight);
					currentMaxWidth = baseBound.x + yResidue;

					if (TryExpandX(anchor, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, ref blockWidth, blockHeight, currentMaxWidth, ref xBlockedByWall))
						isActivelyExpanding = true;

					int xResidue = Mathf.Max(0, currentMaxWidth - blockWidth);
					currentMaxHeight = baseBound.y + xResidue;
				}
			}

			// Final Box Lock & Hand-off (now into a SliceBlock, not straight into a BoundingBox
			// dictionary — bedrock/strip classification happens one layer up in Slice())
			var claimedTiles = new List<Vector2Int>(blockWidth * blockHeight);
			for (int dy = 0; dy < blockHeight; dy++) {
				int rowStartIdx = (anchor.y + dy - minY) * gridWidth + (anchor.x - minX);

				for (int dx = 0; dx < blockWidth; dx++) {
					unassignedGrid[rowStartIdx + dx] = false; // Fast Rent/Release mapping
					claimedTiles.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
				}
			}

			sweepResults.Add(new SliceBlock {
				MinX = anchor.x,
				MinY = anchor.y,
				MaxX = anchor.x + blockWidth - 1,
				MaxY = anchor.y + blockHeight - 1,
				Anchor = anchor,
				Tiles = claimedTiles
			});
		}

		private bool ShouldExpandXFirst(
			Vector2Int anchor, bool[] unassignedGrid, int gridWidth, int gridHeight, int minX, int minY, int maxX, int maxY,
			int blockWidth, int blockHeight, int currentMaxWidth, int currentMaxHeight,
			bool xBlockedByWall, bool yBlockedByWall) {

			bool xCanStep = !xBlockedByWall && blockWidth < currentMaxWidth &&
				IsColumnClear(unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, anchor.x + blockWidth, anchor.y, blockHeight);

			bool yCanStep = !yBlockedByWall && blockHeight < currentMaxHeight &&
				IsRowClear(unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, anchor.x, anchor.y + blockHeight, blockWidth);

			if (!xCanStep || !yCanStep) return true;

			float scoreIfXFirst = SquarenessScore(blockWidth + 1, blockHeight);
			float scoreIfYFirst = SquarenessScore(blockWidth, blockHeight + 1);

			return scoreIfXFirst <= scoreIfYFirst;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float SquarenessScore(int width, int height) {
			return width >= height ? (float)width / height : (float)height / width;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsColumnClear(bool[] grid, int gridWidth, int gridHeight, int minX, int minY, int maxX, int maxY, int targetX, int startY, int height) {
			if (targetX < minX || targetX > maxX) return false;
			int localStartY = startY - minY;
			if (localStartY < 0 || localStartY + height - 1 > maxY - minY) return false;

			int localX = targetX - minX;
			int startIdx = localStartY * gridWidth + localX;
			for (int dy = 0; dy < height; dy++) {
				if (!grid[startIdx + (dy * gridWidth)]) return false; // Strided memory jump
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsRowClear(bool[] grid, int gridWidth, int gridHeight, int minX, int minY, int maxX, int maxY, int startX, int targetY, int width) {
			if (targetY < minY || targetY > maxY) return false;
			int localStartX = startX - minX;
			if (localStartX < 0 || localStartX + width - 1 > maxX - minX) return false;

			int startIdx = (targetY - minY) * gridWidth + localStartX;
			for (int dx = 0; dx < width; dx++) {
				if (!grid[startIdx + dx]) return false; // Pure sequential memory read
			}
			return true;
		}

		private bool TryExpandX(
			Vector2Int anchor, bool[] unassignedGrid, int gridWidth, int gridHeight, int minX, int minY, int maxX, int maxY,
			ref int blockWidth, int blockHeight, int maxBudgetW, ref bool xBlockedByWall) {

			if (xBlockedByWall || blockWidth >= maxBudgetW) return false;

			bool expanded = false;

			while (blockWidth < maxBudgetW) {
				if (IsColumnClear(unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, anchor.x + blockWidth, anchor.y, blockHeight)) {
					blockWidth++;
					expanded = true;
				} else {
					xBlockedByWall = true;
					break;
				}
			}
			return expanded;
		}

		private bool TryExpandY(
			Vector2Int anchor, bool[] unassignedGrid, int gridWidth, int gridHeight, int minX, int minY, int maxX, int maxY,
			int blockWidth, ref int blockHeight, int maxBudgetH, ref bool yBlockedByWall) {

			if (yBlockedByWall || blockHeight >= maxBudgetH) return false;

			bool expanded = false;

			while (blockHeight < maxBudgetH) {
				if (IsRowClear(unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, anchor.x, anchor.y + blockHeight, blockWidth)) {
					blockHeight++;
					expanded = true;
				} else {
					yBlockedByWall = true;
					break;
				}
			}
			return expanded;
		}
	}
}