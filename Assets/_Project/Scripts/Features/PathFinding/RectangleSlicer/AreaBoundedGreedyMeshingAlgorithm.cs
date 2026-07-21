using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// AREA-BOUNDED GREEDY MESHING ALGORITHM (V3: BINARY GRID & MEMORY STRIDE OPTIMIZED)
	/// =========================================================================================
	/// Retains the single-pass cascading and squarified sequencing logic, but replaces the 
	/// pointer-chasing HashSet with a contiguous 1D Binary Grid (bool[]).
	/// 
	/// Spatial checks bypass heap allocations and hash lookups entirely. By projecting coordinates
	/// into a local 1D array (`index = y * width + x`), the CPU can aggressively cache memory 
	/// strides. Expanding along the X axis becomes a sequential memory read (`startIdx + dx`), 
	/// executing meaningfully faster than hash-based lookups on grid-dense data.
	///
	/// Correctness Note (grid bounds): HashSet.Contains() returns false for free on any
	/// coordinate outside the region — nothing outside the set needs special handling. A flat
	/// array doesn't have that property automatically: index = (y-minY)*gridWidth + (x-minX) is
	/// only meaningful while x and y stay within the region's actual bounding box. Before, none
	/// of the read paths checked that, so stepping one column past the region's real right edge
	/// (x = maxX + 1) computed an index that silently landed on the *next row's* first cell
	/// instead of failing — the flat array has no "out of bounds" for coordinates that wrap back
	/// into range, only for ones that overflow the whole buffer. If that unrelated cell happened
	/// to be unclaimed, the probe reported "clear" for a position outside the region entirely,
	/// the block kept growing into unvalidated space, and the final claim loop marked that
	/// unrelated tile (belonging to a different row, never part of this rectangle) as consumed —
	/// silently dropping it from every future scan and from the final output. This happens
	/// whenever a region's real bounding box is narrower/shorter than maxBoundSize plus residue,
	/// which is the common case for any non-square region, not an edge case. Now, IsColumnClear
	/// and IsRowClear explicitly check the target coordinate against [minX,maxX]/[minY,maxY]
	/// before indexing, restoring the same "outside the region reads as blocked" behavior
	/// HashSet gave for free — at the cost of a couple of extra integer comparisons per check,
	/// which is negligible next to the array-vs-hash lookup savings this version is built around.
	/// =========================================================================================
	/// </summary>
	public class AreaBoundedGreedyMeshingAlgorithm : IRectangleRegionSlicer {

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

			var sweepResults = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

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
			// Note: sized to the region's own bounding box, not to maxBoundSize — a sparse or
			// elongated-diagonal region can have a bounding box much larger than its actual tile
			// count, which is the memory tradeoff for dropping the HashSet's O(tileCount) footprint
			// in favor of this array's O(boundingBoxArea) one. Fine for typical compact HHSI
			// regions; worth knowing if a region ever comes out very sparse/scattered.
			int gridWidth = (maxX - minX) + 1;
			int gridHeight = (maxY - minY) + 1;
			bool[] unassignedGrid = new bool[gridWidth * gridHeight];

			foreach (var tile in regionTiles) {
				unassignedGrid[(tile.y - minY) * gridWidth + (tile.x - minX)] = true;
			}

			// Single standard raster scan (Bottom-to-Top, Left-to-Right)
			for (int y = minY; y <= maxY; y++) {
				for (int x = minX; x <= maxX; x++) {
					int gridIdx = (y - minY) * gridWidth + (x - minX);

					if (!unassignedGrid[gridIdx]) continue;

					var anchorPos = new Vector2Int(x, y);
					ExtractOptimalCascadingBlock(
						anchorPos, unassignedGrid, gridWidth, gridHeight, minX, minY, maxX, maxY, maxBoundSize, sweepResults);
				}
			}

			return sweepResults;
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
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> sweepResults) {

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

			// Final Box Lock & Hand-off
			var claimedTiles = new List<Vector2Int>(blockWidth * blockHeight);
			for (int dy = 0; dy < blockHeight; dy++) {
				// Calculate the start of the row once to save math cycles
				int rowStartIdx = (anchor.y + dy - minY) * gridWidth + (anchor.x - minX);

				for (int dx = 0; dx < blockWidth; dx++) {
					unassignedGrid[rowStartIdx + dx] = false; // Fast Rent/Release mapping
					claimedTiles.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
				}
			}

			var boundingBox = new BoundingBox(anchor.x, anchor.y, anchor.x + blockWidth - 1, anchor.y + blockHeight - 1);
			sweepResults[boundingBox] = (anchor, claimedTiles);
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

		// Correctness fix: bounds-checks targetX (and the full [startY, startY+height) span)
		// against the region's actual grid extent before touching the array. Before, an
		// out-of-extent targetX (e.g. maxX + 1) still produced an in-range flat index that
		// pointed at a different, unrelated row — this now reports "not clear" for that case
		// the same way HashSet.Contains would have, instead of silently reading into it.
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

		// Correctness fix: same reasoning as IsColumnClear, mirrored for the horizontal case —
		// bounds-checks targetY and the full [startX, startX+width) span before indexing.
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