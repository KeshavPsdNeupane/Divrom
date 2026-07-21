using System;
using System.Collections.Generic;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// DUAL-AXIS CASCADING REGION SLICER
	/// =========================================================================================
	/// Fixes the "flawed residue" issue by treating the X and Y bounds not as independent limits, 
	/// but as a unified shared budget: (Width + Height <= MaxX + MaxY). 
	/// 
	/// By evaluating two complete sweeps (X-Dominant and Y-Dominant) across the entire region,
	/// we allow the primary expansion axis to seamlessly borrow budget from the secondary axis, 
	/// while strictly preventing the jagged fragmentation that occurs when alternating axes on a 
	/// per-box basis. Returns the sweep that yielded the fewest total partitions.
	/// =========================================================================================
	/// </summary>
	public class DualAxisCascadingSlicer : IRectangleRegionSlicer {

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;

				// Evaluate both scanline strategies using the cascading shared budget
				var horizontalSlices = ExecuteCascadingSweep(regionTiles, maxBoundSize, isXDominant: true);
				var verticalSlices = ExecuteCascadingSweep(regionTiles, maxBoundSize, isXDominant: false);

				// Adopt the strategy that yielded the fewest total partitions
				var optimalSlices = horizontalSlices.Count <= verticalSlices.Count ? horizontalSlices : verticalSlices;

				foreach (var slice in optimalSlices) {
					finalSlicedRegions[slice.Key] = slice.Value;
				}
			}

			return finalSlicedRegions;
		}

		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> ExecuteCascadingSweep(
			List<Vector2Int> regionTiles,
			Vector2Int maxBoundSize,
			bool isXDominant) {

			var sweepResults = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			var unassignedTiles = new HashSet<Vector2Int>(regionTiles);

			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			foreach (var tile in regionTiles) {
				if (tile.x < minX) minX = tile.x;
				if (tile.x > maxX) maxX = tile.x;
				if (tile.y < minY) minY = tile.y;
				if (tile.y > maxY) maxY = tile.y;
			}

			// Sweep logic respects the dominant axis to prevent splintering
			if (isXDominant) {
				for (int y = minY; y <= maxY; y++) {
					for (int x = minX; x <= maxX; x++) {
						var anchorPos = new Vector2Int(x, y);
						if (!unassignedTiles.Contains(anchorPos)) continue;

						ExtractCascadingBlock(anchorPos, unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			} else {
				for (int x = minX; x <= maxX; x++) {
					for (int y = minY; y <= maxY; y++) {
						var anchorPos = new Vector2Int(x, y);
						if (!unassignedTiles.Contains(anchorPos)) continue;

						ExtractCascadingBlock(anchorPos, unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			}

			return sweepResults;
		}

		private void ExtractCascadingBlock(
			Vector2Int anchor,
			HashSet<Vector2Int> unassignedTiles,
			Vector2Int maxBound,
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> sweepResults,
			bool isXDominant) {

			int blockWidth = 1;
			int blockHeight = 1;

			// CASCADING FIX: Unify the budget. The maximum allowed size in one axis 
			// is the total budget minus the size of the other axis.
			int apprarentX = isXDominant ? maxBound.x : (int)(maxBound.x * 0.5f);
			int apparentY = isXDominant ? (int)(maxBound.y * 0.5f) : maxBound.y;

			int sharedBudget = apprarentX + apparentY;

			if (isXDominant) {
				// Expand horizontally as far as possible, bounded only by the shared budget 
				// (leaving at least 1 unit of budget for the height)
				while (blockWidth < sharedBudget - 1 && unassignedTiles.Contains(new Vector2Int(anchor.x + blockWidth, anchor.y))) {
					blockWidth++;
				}

				// Expand vertically using the established width, consuming whatever budget is left
				int remainingHeightBudget = sharedBudget - blockWidth;
				bool canExpandHeight = true;

				while (blockHeight < remainingHeightBudget && canExpandHeight) {
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
				// Expand vertically as far as possible, bounded by the shared budget
				while (blockHeight < sharedBudget - 1 && unassignedTiles.Contains(new Vector2Int(anchor.x, anchor.y + blockHeight))) {
					blockHeight++;
				}

				// Expand horizontally using the established height, consuming whatever budget is left
				int remainingWidthBudget = sharedBudget - blockHeight;
				bool canExpandWidth = true;

				while (blockWidth < remainingWidthBudget && canExpandWidth) {
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

			// Claim the tiles for this block
			var claimedTiles = new List<Vector2Int>(blockWidth * blockHeight);
			for (int dx = 0; dx < blockWidth; dx++) {
				for (int dy = 0; dy < blockHeight; dy++) {
					var pos = new Vector2Int(anchor.x + dx, anchor.y + dy);
					unassignedTiles.Remove(pos);
					claimedTiles.Add(pos);
				}
			}

			var boundingBox = new BoundingBox(anchor.x, anchor.y, anchor.x + blockWidth - 1, anchor.y + blockHeight - 1);
			sweepResults[boundingBox] = (anchor, claimedTiles);
		}
	}
}