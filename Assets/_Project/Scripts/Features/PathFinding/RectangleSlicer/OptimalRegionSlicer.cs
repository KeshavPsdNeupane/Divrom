using System;
using System.Collections.Generic;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// OPTIMAL REGION SLICER (HYBRID ADAPTIVE + HOMOGENEOUS)
	/// =========================================================================================
	/// Combines Adaptive structural boundary detection with Homogeneous box-level merge discipline.
	/// Guarantees perfectly balanced subdivisions while preserving continuous corridors and 
	/// rejecting meaningless 1-tile edge fragments.
	/// =========================================================================================
	/// </summary>
	public class OptimalRegionSlicer : IRectangleRegionSlicer {

		private readonly float _protrusionThresholdPercent;
		private readonly int _minRegionThickness;

		public OptimalRegionSlicer(float protrusionThresholdPercent = 0.25f, int minRegionThickness = 2) {
			_protrusionThresholdPercent = Mathf.Clamp01(protrusionThresholdPercent);
			_minRegionThickness = Mathf.Max(1, minRegionThickness); // Prevents 1-tile navigation strips
		}

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;
				if (regionTiles == null || regionTiles.Count == 0) continue;

				// STEP 1: Adaptive Primary Pass (Smart Protrusion Handling)
				var primaryResults = RunPrimaryPass(regionTiles, maxBoundSize);

				// STEP 2: Homogeneous Box-Level Maximal Merging
				var mergedBoxes = MaximalBoxMerge(primaryResults);

				// STEP 3: Adaptive Balanced Subdivision
				var postProcessed = SubdivideUniformly(mergedBoxes, maxBoundSize);

				foreach (var result in postProcessed) {
					finalSlicedRegions[result.Key] = result.Value;
				}
			}

			return finalSlicedRegions;
		}

		// =====================================================================================
		// STEP 1: PRIMARY PASS (From AdaptiveAlgo)
		// =====================================================================================
		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> RunPrimaryPass(
			List<Vector2Int> regionTiles, Vector2Int maxBoundSize) {

			var results = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			var unassignedTiles = new HashSet<Vector2Int>(regionTiles);
			var regionShape = new HashSet<Vector2Int>(regionTiles);

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

					bool hasSufficientArea = HasSufficientBoundingArea(anchor, regionShape, minCheckWidth, minCheckHeight);

					Vector2Int extent;
					if (!hasSufficientArea) {
						// Extract narrow corridor extent
						extent = GetProtrusionExtent(anchor, regionShape, maxBoundSize, minCheckWidth, minCheckHeight);
					} else {
						// Extract best standard block extent
						var xCandidate = PeekBlockExtent(anchor, unassignedTiles, maxBoundSize, true);
						var yCandidate = PeekBlockExtent(anchor, unassignedTiles, maxBoundSize, false);
						extent = (xCandidate.x * xCandidate.y >= yCandidate.x * yCandidate.y) ? xCandidate : yCandidate;
					}

					var claimedTiles = new List<Vector2Int>(extent.x * extent.y);
					for (int dx = 0; dx < extent.x; dx++) {
						for (int dy = 0; dy < extent.y; dy++) {
							var pos = new Vector2Int(anchor.x + dx, anchor.y + dy);
							if (unassignedTiles.Remove(pos)) {
								claimedTiles.Add(pos);
							}
						}
					}

					var box = new BoundingBox(anchor.x, anchor.y, anchor.x + extent.x - 1, anchor.y + extent.y - 1);
					results[box] = (anchor, claimedTiles);
				}
			}
			return results;
		}

		private bool HasSufficientBoundingArea(Vector2Int anchor, HashSet<Vector2Int> shape, int minWidth, int minHeight) {
			return BoxFullyPresent(anchor.x, anchor.y, minWidth, minHeight, shape) ||
				   BoxFullyPresent(anchor.x - minWidth + 1, anchor.y - minHeight + 1, minWidth, minHeight, shape);
		}

		private bool BoxFullyPresent(int startX, int startY, int width, int height, HashSet<Vector2Int> shape) {
			for (int dx = 0; dx < width; dx++) {
				for (int dy = 0; dy < height; dy++) {
					if (!shape.Contains(new Vector2Int(startX + dx, startY + dy))) return false;
				}
			}
			return true;
		}

		private Vector2Int PeekBlockExtent(Vector2Int anchor, HashSet<Vector2Int> unassignedTiles, Vector2Int maxBound, bool isXDominant) {
			int blockWidth = 1, blockHeight = 1;
			if (isXDominant) {
				while (blockWidth < maxBound.x && unassignedTiles.Contains(new Vector2Int(anchor.x + blockWidth, anchor.y))) blockWidth++;
				bool canExpandHeight = true;
				while (blockHeight < maxBound.y && canExpandHeight) {
					int checkY = anchor.y + blockHeight;
					for (int dx = 0; dx < blockWidth; dx++) {
						if (!unassignedTiles.Contains(new Vector2Int(anchor.x + dx, checkY))) { canExpandHeight = false; break; }
					}
					if (canExpandHeight) blockHeight++;
				}
			} else {
				while (blockHeight < maxBound.y && unassignedTiles.Contains(new Vector2Int(anchor.x, anchor.y + blockHeight))) blockHeight++;
				bool canExpandWidth = true;
				while (blockWidth < maxBound.x && canExpandWidth) {
					int checkX = anchor.x + blockWidth;
					for (int dy = 0; dy < blockHeight; dy++) {
						if (!unassignedTiles.Contains(new Vector2Int(checkX, anchor.y + dy))) { canExpandWidth = false; break; }
					}
					if (canExpandWidth) blockWidth++;
				}
			}
			return new Vector2Int(blockWidth, blockHeight);
		}

		private Vector2Int GetProtrusionExtent(Vector2Int anchor, HashSet<Vector2Int> shape, Vector2Int maxBoundSize, int minW, int minH) {
			// Simplified protrusion bounding to avoid the complex binary search, 
			// relying instead on the Phase 2 Merge to fix the corridors.
			var xCandidate = PeekBlockExtent(anchor, shape, maxBoundSize, true);
			var yCandidate = PeekBlockExtent(anchor, shape, maxBoundSize, false);
			return (xCandidate.x * xCandidate.y >= yCandidate.x * yCandidate.y) ? xCandidate : yCandidate;
		}

		// =====================================================================================
		// STEP 2: HOMOGENEOUS MAXIMAL MERGE (Box-Level)
		// =====================================================================================
		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> MaximalBoxMerge(
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> initialSlices) {

			var processedResults = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			var lockedBoxes = new HashSet<BoundingBox>();
			var sliceList = new List<BoundingBox>(initialSlices.Keys);

			for (int i = 0; i < sliceList.Count; i++) {
				var currentBox = sliceList[i];
				if (lockedBoxes.Contains(currentBox)) continue;

				BoundingBox mergedBox = currentBox;
				List<Vector2Int> mergedTiles = new List<Vector2Int>(initialSlices[currentBox].Item2);
				lockedBoxes.Add(currentBox);

				bool mergedSomething;
				do {
					mergedSomething = false;
					for (int j = i + 1; j < sliceList.Count; j++) {
						var checkBox = sliceList[j];
						if (lockedBoxes.Contains(checkBox)) continue;

						if (CanMergePerfectRectangle(mergedBox, checkBox)) {
							mergedBox = new BoundingBox(
								Mathf.Min(mergedBox.Min.x, checkBox.Min.x), Mathf.Min(mergedBox.Min.y, checkBox.Min.y),
								Mathf.Max(mergedBox.Max.x, checkBox.Max.x), Mathf.Max(mergedBox.Max.y, checkBox.Max.y)
							);
							mergedTiles.AddRange(initialSlices[checkBox].Item2);
							lockedBoxes.Add(checkBox);
							mergedSomething = true; // Loop again, as the new larger box might now flush with another
						}
					}
				} while (mergedSomething);

				processedResults[mergedBox] = (new Vector2Int(mergedBox.Min.x, mergedBox.Min.y), mergedTiles);
			}
			return processedResults;
		}

		private bool CanMergePerfectRectangle(BoundingBox a, BoundingBox b) {
			bool verticalAlign = (a.Min.x == b.Min.x && a.Max.x == b.Max.x) && (a.Max.y + 1 == b.Min.y || b.Max.y + 1 == a.Min.y);
			bool horizontalAlign = (a.Min.y == b.Min.y && a.Max.y == b.Max.y) && (a.Max.x + 1 == b.Min.x || b.Max.x + 1 == a.Min.x);
			return verticalAlign || horizontalAlign;
		}

		// =====================================================================================
		// STEP 3: ADAPTIVE BALANCED SUBDIVISION
		// =====================================================================================
		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> SubdivideUniformly(
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> mergedRegions, Vector2Int maxBoundSize) {

			var finalResults = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in mergedRegions) {
				var box = kvp.Key;
				var tileSet = new HashSet<Vector2Int>(kvp.Value.Item2);

				int totalWidth = box.Max.x - box.Min.x + 1;
				int totalHeight = box.Max.y - box.Min.y + 1;

				// Use adaptive sizing to prevent 1-tile edge fragments
				int numCols = CalculateBalancedSplits(totalWidth, maxBoundSize.x);
				int numRows = CalculateBalancedSplits(totalHeight, maxBoundSize.y);

				int[] colWidths = SplitEvenly(totalWidth, numCols);
				int[] rowHeights = SplitEvenly(totalHeight, numRows);

				int yCursor = box.Min.y;
				for (int r = 0; r < numRows; r++) {
					int xCursor = box.Min.x;
					for (int c = 0; c < numCols; c++) {
						var subBox = new BoundingBox(
							xCursor, yCursor,
							xCursor + colWidths[c] - 1, yCursor + rowHeights[r] - 1);

						var subTiles = new List<Vector2Int>(colWidths[c] * rowHeights[r]);
						for (int dx = 0; dx < colWidths[c]; dx++) {
							for (int dy = 0; dy < rowHeights[r]; dy++) {
								var pos = new Vector2Int(xCursor + dx, yCursor + dy);
								if (tileSet.Contains(pos)) subTiles.Add(pos);
							}
						}

						if (subTiles.Count > 0) {
							finalResults[subBox] = (new Vector2Int(subBox.Min.x, subBox.Min.y), subTiles);
						}
						xCursor += colWidths[c];
					}
					yCursor += rowHeights[r];
				}
			}
			return finalResults;
		}

		private int CalculateBalancedSplits(int total, int maxLimit) {
			int idealSplits = Mathf.Max(1, Mathf.CeilToInt((float)total / maxLimit));
			// Prevent generating splits where the pieces would fall below our minimum thickness
			if (idealSplits > 1 && (total / idealSplits) < _minRegionThickness) {
				idealSplits = Mathf.Max(1, idealSplits - 1);
			}
			return idealSplits;
		}

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