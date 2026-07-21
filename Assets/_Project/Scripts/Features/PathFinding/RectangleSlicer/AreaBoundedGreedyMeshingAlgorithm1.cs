using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// AREA-BOUNDED GREEDY MESHING ALGORITHM (V4.2: LOCAL SPAN STRIP DETECTION)
	/// =========================================================================================
	/// Fixes the perimeter-chopping bug by replacing rigid 3x3 erosion with a local span 
	/// thickness check. Large rooms (even their outer edge tiles) have large spans on at least 
	/// one axis and are processed in Phase 1. Only genuinely narrow corridors (thickness <= 2) 
	/// are deferred to Phase 2 and cleaned up by the Grand Finale.
	/// =========================================================================================
	/// </summary>
	public class AreaBoundedGreedyMeshingAlgorithm1 : IRectangleRegionSlicer {

		private const float SPLIT_TOLERANCE_FACTOR = 1.25f;
		private const float STRIP_ASPECT_RATIO_THRESHOLD = 3f;
		private const int GRAND_FINALE_MAX_ITERATIONS = 100;
		private const int STRIP_THICKNESS_THRESHOLD = 2; // 1-wide or 2-wide corridors are deferred

		private struct MeshBlock {
			public Vector2Int Anchor;
			public int MinX, MinY, MaxX, MaxY;
			public List<Vector2Int> Tiles;
			public bool IsStrip;

			public int Width => MaxX - MinX + 1;
			public int Height => MaxY - MinY + 1;
		}

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;
				var optimalSlices = ExecuteMeshingSweep(regionTiles, maxBoundSize);

				foreach (var slice in optimalSlices) {
					finalSlicedRegions[slice.Key] = slice.Value;
				}
			}

			return finalSlicedRegions;
		}

		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> ExecuteMeshingSweep(
			List<Vector2Int> regionTiles,
			Vector2Int maxBoundSize) {

			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			foreach (var tile in regionTiles) {
				if (tile.x < minX) minX = tile.x;
				if (tile.x > maxX) maxX = tile.x;
				if (tile.y < minY) minY = tile.y;
				if (tile.y > maxY) maxY = tile.y;
			}

			int gridWidth = (maxX - minX) + 1;
			int gridHeight = (maxY - minY) + 1;

			var regionMask = new bool[gridWidth * gridHeight];
			foreach (var tile in regionTiles) {
				regionMask[(tile.y - minY) * gridWidth + (tile.x - minX)] = true;
			}

			var unassignedGrid = new bool[gridWidth * gridHeight];
			regionMask.CopyTo(unassignedGrid, 0);

			// Identify thin strips using local span checking instead of total perimeter erosion
			bool[] stripMask = ComputeStripMask(regionMask, gridWidth, gridHeight);

			var primaryBlocks = new List<MeshBlock>();
			var stripBlocks = new List<MeshBlock>();
			var deferredAnchors = new List<Vector2Int>();

			// ---- Phase 1: Primary volume slicing (Bulk open areas & room interiors/edges) ----
			for (int y = minY; y <= maxY; y++) {
				for (int x = minX; x <= maxX; x++) {
					int gridIdx = (y - minY) * gridWidth + (x - minX);
					if (!unassignedGrid[gridIdx]) continue;

					if (!stripMask[gridIdx]) {
						var anchorPos = new Vector2Int(x, y);
						ExtractOptimalCascadingBlock(
							anchorPos, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY,
							maxBoundSize, isStrip: false, primaryBlocks);
					} else {
						deferredAnchors.Add(new Vector2Int(x, y));
					}
				}
			}

			// ---- Phase 2: Deferred narrow strip slicing ----
			foreach (var anchor in deferredAnchors) {
				int gridIdx = (anchor.y - minY) * gridWidth + (anchor.x - minX);
				if (!unassignedGrid[gridIdx]) continue;

				ExtractOptimalCascadingBlock(
					anchor, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY,
					maxBoundSize, isStrip: true, stripBlocks);
			}

			// ---- Grand Finale: Iterative merge & split exclusively on isolated strip blocks ----
			GrandFinaleMergeStrips(stripBlocks, maxBoundSize);

			int totalBlockCount = primaryBlocks.Count + stripBlocks.Count;
			var sweepResults = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>(totalBlockCount);

			foreach (var block in primaryBlocks) {
				sweepResults[new BoundingBox(block.MinX, block.MinY, block.MaxX, block.MaxY)] = (block.Anchor, block.Tiles);
			}
			foreach (var block in stripBlocks) {
				sweepResults[new BoundingBox(block.MinX, block.MinY, block.MaxX, block.MaxY)] = (block.Anchor, block.Tiles);
			}

			return sweepResults;
		}

		/// <summary>
		/// Computes whether a tile belongs to a narrow corridor/strip by measuring its local spans.
		/// A tile is a strip tile only if its minimum transverse span is <= STRIP_THICKNESS_THRESHOLD.
		/// Edge tiles of large rooms have large spans on at least one axis and are correctly kept out of this mask.
		/// </summary>
		private static bool[] ComputeStripMask(bool[] regionMask, int gridWidth, int gridHeight) {
			var stripMask = new bool[regionMask.Length];

			for (int y = 0; y < gridHeight; y++) {
				for (int x = 0; x < gridWidth; x++) {
					int idx = y * gridWidth + x;
					if (!regionMask[idx]) continue;

					// Measure horizontal span
					int left = 0;
					while (x - left - 1 >= 0 && regionMask[y * gridWidth + (x - left - 1)]) left++;
					int right = 0;
					while (x + right + 1 < gridWidth && regionMask[y * gridWidth + (x + right + 1)]) right++;
					int horizontalSpan = left + 1 + right;

					// Measure vertical span
					int down = 0;
					while (y - down - 1 >= 0 && regionMask[(y - down - 1) * gridWidth + x]) down++;
					int up = 0;
					while (y + up + 1 < gridHeight && regionMask[(y + up + 1) * gridWidth + x]) up++;
					int verticalSpan = down + 1 + up;

					int minSpan = Mathf.Min(horizontalSpan, verticalSpan);
					if (minSpan <= STRIP_THICKNESS_THRESHOLD) {
						stripMask[idx] = true;
					}
				}
			}

			return stripMask;
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
			bool isStrip,
			List<MeshBlock> targetContainer) {

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

			var claimedTiles = new List<Vector2Int>(blockWidth * blockHeight);
			for (int dy = 0; dy < blockHeight; dy++) {
				int rowStartIdx = (anchor.y + dy - minY) * gridWidth + (anchor.x - minX);

				for (int dx = 0; dx < blockWidth; dx++) {
					unassignedGrid[rowStartIdx + dx] = false;
					claimedTiles.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
				}
			}

			targetContainer.Add(new MeshBlock {
				Anchor = anchor,
				MinX = anchor.x,
				MinY = anchor.y,
				MaxX = anchor.x + blockWidth - 1,
				MaxY = anchor.y + blockHeight - 1,
				Tiles = claimedTiles,
				IsStrip = isStrip
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
				if (!grid[startIdx + (dy * gridWidth)]) return false;
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
				if (!grid[startIdx + dx]) return false;
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

		// =====================================================================================
		// GRAND FINALE
		// =====================================================================================

		private void GrandFinaleMergeStrips(List<MeshBlock> stripBlocks, Vector2Int maxBoundSize) {
			for (int iteration = 0; iteration < GRAND_FINALE_MAX_ITERATIONS; iteration++) {
				bool anyMergeHappened = RunGrandFinalePass(stripBlocks, maxBoundSize);
				if (!anyMergeHappened) break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsLongStrip(MeshBlock block) {
			int longSide = Mathf.Max(block.Width, block.Height);
			int shortSide = Mathf.Min(block.Width, block.Height);
			return longSide >= shortSide * STRIP_ASPECT_RATIO_THRESHOLD;
		}

		private bool RunGrandFinalePass(List<MeshBlock> blocks, Vector2Int maxBoundSize) {
			if (blocks.Count < 2) return false;

			var indicesToRemove = new HashSet<int>();
			var newBlocks = new List<MeshBlock>();

			var horizontalGroups = new Dictionary<(int minY, int maxY), List<int>>();
			var verticalGroups = new Dictionary<(int minX, int maxX), List<int>>();

			for (int i = 0; i < blocks.Count; i++) {
				var block = blocks[i];
				bool isStrip = IsLongStrip(block);

				bool goesHorizontal = !isStrip || (block.Width >= block.Height);
				bool goesVertical = !isStrip || (block.Height >= block.Width);

				if (goesHorizontal) {
					var hKey = (block.MinY, block.MaxY);
					if (!horizontalGroups.TryGetValue(hKey, out var hList)) {
						hList = new List<int>();
						horizontalGroups[hKey] = hList;
					}
					hList.Add(i);
				}

				if (goesVertical) {
					var vKey = (block.MinX, block.MaxX);
					if (!verticalGroups.TryGetValue(vKey, out var vList)) {
						vList = new List<int>();
						verticalGroups[vKey] = vList;
					}
					vList.Add(i);
				}
			}

			foreach (var group in horizontalGroups.Values) {
				if (group.Count < 2) continue;
				group.Sort((a, b) => blocks[a].MinX.CompareTo(blocks[b].MinX));
				MergeContiguousRuns(blocks, group, maxBoundSize, isHorizontal: true, indicesToRemove, newBlocks);
			}

			foreach (var group in verticalGroups.Values) {
				group.RemoveAll(idx => indicesToRemove.Contains(idx));

				if (group.Count < 2) continue;
				group.Sort((a, b) => blocks[a].MinY.CompareTo(blocks[b].MinY));
				MergeContiguousRuns(blocks, group, maxBoundSize, isHorizontal: false, indicesToRemove, newBlocks);
			}

			if (indicesToRemove.Count == 0) return false;

			var survivors = new List<MeshBlock>(blocks.Count - indicesToRemove.Count + newBlocks.Count);
			for (int i = 0; i < blocks.Count; i++) {
				if (!indicesToRemove.Contains(i)) survivors.Add(blocks[i]);
			}
			survivors.AddRange(newBlocks);

			blocks.Clear();
			blocks.AddRange(survivors);
			return true;
		}

		private void MergeContiguousRuns(
			List<MeshBlock> blocks,
			List<int> sortedGroup,
			Vector2Int maxBoundSize,
			bool isHorizontal,
			HashSet<int> indicesToRemove,
			List<MeshBlock> newBlocks) {

			int runStart = 0;
			for (int k = 1; k <= sortedGroup.Count; k++) {
				bool contiguous = k < sortedGroup.Count && (isHorizontal
					? blocks[sortedGroup[k]].MinX == blocks[sortedGroup[k - 1]].MaxX + 1
					: blocks[sortedGroup[k]].MinY == blocks[sortedGroup[k - 1]].MaxY + 1);

				if (contiguous) continue;

				int runLength = k - runStart;
				if (runLength >= 2 && RunHasGenuineStripAnchor(blocks, sortedGroup, runStart, k - 1)) {
					FinalizeRun(blocks, sortedGroup, runStart, k - 1, maxBoundSize, isHorizontal, indicesToRemove, newBlocks);
				}
				runStart = k;
			}
		}

		private static bool RunHasGenuineStripAnchor(List<MeshBlock> blocks, List<int> sortedGroup, int runStart, int runEnd) {
			for (int idx = runStart; idx <= runEnd; idx++) {
				if (IsLongStrip(blocks[sortedGroup[idx]])) return true;
			}
			return false;
		}

		private void FinalizeRun(
			List<MeshBlock> blocks,
			List<int> sortedGroup,
			int runStart,
			int runEnd,
			Vector2Int maxBoundSize,
			bool isHorizontal,
			HashSet<int> indicesToRemove,
			List<MeshBlock> newBlocks) {

			var first = blocks[sortedGroup[runStart]];
			var last = blocks[sortedGroup[runEnd]];

			var proposedBlocks = new List<MeshBlock>();

			if (isHorizontal) {
				int mergedMinX = first.MinX;
				int mergedMaxX = last.MaxX;
				int minY = first.MinY;
				int maxY = first.MaxY;
				int mergedWidth = mergedMaxX - mergedMinX + 1;

				if (mergedWidth > maxBoundSize.x * SPLIT_TOLERANCE_FACTOR) {
					int leftWidth = mergedWidth / 2;
					int splitX = mergedMinX + leftWidth;
					proposedBlocks.Add(BuildRectBlock(mergedMinX, minY, splitX - 1, maxY));
					proposedBlocks.Add(BuildRectBlock(splitX, minY, mergedMaxX, maxY));
				} else {
					proposedBlocks.Add(BuildRectBlock(mergedMinX, minY, mergedMaxX, maxY));
				}
			} else {
				int mergedMinY = first.MinY;
				int mergedMaxY = last.MaxY;
				int minX = first.MinX;
				int maxX = first.MaxX;
				int mergedHeight = mergedMaxY - mergedMinY + 1;

				if (mergedHeight > maxBoundSize.y * SPLIT_TOLERANCE_FACTOR) {
					int bottomHeight = mergedHeight / 2;
					int splitY = mergedMinY + bottomHeight;
					proposedBlocks.Add(BuildRectBlock(minX, mergedMinY, maxX, splitY - 1));
					proposedBlocks.Add(BuildRectBlock(minX, splitY, maxX, mergedMaxY));
				} else {
					proposedBlocks.Add(BuildRectBlock(minX, mergedMinY, maxX, mergedMaxY));
				}
			}

			bool isMeaningfulChange = proposedBlocks.Count != (runEnd - runStart + 1);

			if (!isMeaningfulChange) {
				isMeaningfulChange = false;
				for (int i = 0; i < proposedBlocks.Count; i++) {
					var orig = blocks[sortedGroup[runStart + i]];
					var prop = proposedBlocks[i];
					if (orig.MinX != prop.MinX || orig.MaxX != prop.MaxX ||
						orig.MinY != prop.MinY || orig.MaxY != prop.MaxY) {
						isMeaningfulChange = true;
						break;
					}
				}
			}

			if (isMeaningfulChange) {
				for (int idx = runStart; idx <= runEnd; idx++) {
					indicesToRemove.Add(sortedGroup[idx]);
				}
				newBlocks.AddRange(proposedBlocks);
			}
		}

		private static MeshBlock BuildRectBlock(int minX, int minY, int maxX, int maxY) {
			var tiles = new List<Vector2Int>((maxX - minX + 1) * (maxY - minY + 1));
			for (int y = minY; y <= maxY; y++) {
				for (int x = minX; x <= maxX; x++) {
					tiles.Add(new Vector2Int(x, y));
				}
			}

			return new MeshBlock {
				Anchor = new Vector2Int(minX, minY),
				MinX = minX,
				MinY = minY,
				MaxX = maxX,
				MaxY = maxY,
				Tiles = tiles,
				IsStrip = true
			};
		}
	}
}