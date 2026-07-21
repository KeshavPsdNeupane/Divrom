using System.Collections.Generic;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// HOMOGENEOUS REGION SLICER (IMPROVED GREEDY MESHING)
	/// =========================================================================================
	/// Phase 1: Protrusion-Aware Meshing handles narrow offshoots via spatial thresholds.
	/// Phase 2: Post-Processing maximal merge and uniform subdivision guarantees even grids.
	/// =========================================================================================
	/// </summary>
	public class HomogeneousRegionSlicer : IRectangleRegionSlicer {

		// Threshold for protrusion detection (e.g., 25% of max bounds)
		private readonly float protrusionThreshold = 0.25f;

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;

				// PHASE 1: Dual-Axis Sweep with Protrusion Handling
				var horizontalSlices = ExecuteMeshingSweep(regionTiles, maxBoundSize, isXDominant: true);
				var verticalSlices = ExecuteMeshingSweep(regionTiles, maxBoundSize, isXDominant: false);

				// Adopt the strategy that yielded the fewest total partitions
				var optimalSlices = horizontalSlices.Count <= verticalSlices.Count ? horizontalSlices : verticalSlices;

				// PHASE 2: Maximal Merging and Uniform Subdivision
				var processedSlices = PostProcessHomogeneousRegions(optimalSlices, maxBoundSize);

				foreach (var slice in processedSlices) {
					finalSlicedRegions[slice.Key] = slice.Value;
				}
			}

			return finalSlicedRegions;
		}

		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> ExecuteMeshingSweep(
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

			// Sweep logic respects the dominant axis
			if (isXDominant) {
				for (int y = minY; y <= maxY; y++) {
					for (int x = minX; x <= maxX; x++) {
						ProcessAnchor(new Vector2Int(x, y), unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			} else {
				for (int x = minX; x <= maxX; x++) {
					for (int y = minY; y <= maxY; y++) {
						ProcessAnchor(new Vector2Int(x, y), unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			}

			return sweepResults;
		}

		private void ProcessAnchor(
			Vector2Int anchor,
			HashSet<Vector2Int> unassignedTiles,
			Vector2Int maxBound,
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> sweepResults,
			bool isXDominant) {

			if (!unassignedTiles.Contains(anchor)) return;

			// 1. Protrusion Check: Verify if surrounding space meets the threshold
			if (IsProtrusion(anchor, unassignedTiles, maxBound)) {
				// If in a narrow offshoot, override dominant axis to encapsulate it locally
				ExtractOptimalBlock(anchor, unassignedTiles, maxBound, sweepResults, !isXDominant);
				return;
			}

			// 2. Standard Greedy Processing
			ExtractOptimalBlock(anchor, unassignedTiles, maxBound, sweepResults, isXDominant);
		}

		private bool IsProtrusion(Vector2Int anchor, HashSet<Vector2Int> unassignedTiles, Vector2Int maxBound) {
			int requiredW = Mathf.CeilToInt(maxBound.x * protrusionThreshold);
			int requiredH = Mathf.CeilToInt(maxBound.y * protrusionThreshold);

			int availableW = 0, availableH = 0;

			while (availableW < requiredW && unassignedTiles.Contains(new Vector2Int(anchor.x + availableW, anchor.y))) {
				availableW++;
			}
			while (availableH < requiredH && unassignedTiles.Contains(new Vector2Int(anchor.x, anchor.y + availableH))) {
				availableH++;
			}

			// If space lacks sufficient bounding area on BOTH axes, it's a protrusion
			return availableW < requiredW && availableH < requiredH;
		}

		private void ExtractOptimalBlock(
			Vector2Int anchor,
			HashSet<Vector2Int> unassignedTiles,
			Vector2Int maxBound,
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> sweepResults,
			bool isXDominant) {

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

			var claimedTiles = new List<Vector2Int>(blockWidth * blockHeight);
			for (int dx = 0; dx < blockWidth; dx++) {
				for (int dy = 0; dy < blockHeight; dy++) {
					var pos = new Vector2Int(anchor.x + dx, anchor.y + dy);
					unassignedTiles.Remove(pos);
					claimedTiles.Add(pos);
				}
			}

			// Assumption: BoundingBox constructor takes (minX, minY, maxX, maxY)
			var boundingBox = new BoundingBox(anchor.x, anchor.y, anchor.x + blockWidth - 1, anchor.y + blockHeight - 1);
			sweepResults[boundingBox] = (anchor, claimedTiles);
		}

		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> PostProcessHomogeneousRegions(
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> initialSlices,
			Vector2Int maxBoundSize) {

			var processedResults = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			var lockedBoxes = new HashSet<BoundingBox>();
			var sliceList = new List<BoundingBox>(initialSlices.Keys);

			for (int i = 0; i < sliceList.Count; i++) {
				var currentBox = sliceList[i];
				if (lockedBoxes.Contains(currentBox)) continue;

				BoundingBox mergedBox = currentBox;
				List<Vector2Int> mergedTiles = new List<Vector2Int>(initialSlices[currentBox].Item2);
				lockedBoxes.Add(currentBox);

				// 1. Maximal Merging (O(N^2) over regions, not tiles)
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
					}
				}

				// 2. Uniform Subdivision
				int totalWidth = (mergedBox.Max.x - mergedBox.Min.x) + 1;
				int totalHeight = (mergedBox.Max.y - mergedBox.Min.y) + 1;

				int cols = Mathf.CeilToInt((float)totalWidth / maxBoundSize.x);
				int rows = Mathf.CeilToInt((float)totalHeight / maxBoundSize.y);

				int uniformW = Mathf.CeilToInt((float)totalWidth / cols);
				int uniformH = Mathf.CeilToInt((float)totalHeight / rows);

				var tileSet = new HashSet<Vector2Int>(mergedTiles);

				for (int r = 0; r < rows; r++) {
					for (int c = 0; c < cols; c++) {
						int sMinX = mergedBox.Min.x + (c * uniformW);
						int sMinY = mergedBox.Min.y + (r * uniformH);
						int sMaxX = Mathf.Min(sMinX + uniformW - 1, mergedBox.Max.x);
						int sMaxY = Mathf.Min(sMinY + uniformH - 1, mergedBox.Max.y);

						var subBox = new BoundingBox(sMinX, sMinY, sMaxX, sMaxY);
						var subTiles = new List<Vector2Int>();

						for (int x = sMinX; x <= sMaxX; x++) {
							for (int y = sMinY; y <= sMaxY; y++) {
								var pos = new Vector2Int(x, y);
								if (tileSet.Contains(pos)) subTiles.Add(pos);
							}
						}

						if (subTiles.Count > 0) {
							processedResults[subBox] = (new Vector2Int(sMinX, sMinY), subTiles);
						}
					}
				}
			}

			return processedResults;
		}

		private bool CanMergePerfectRectangle(BoundingBox a, BoundingBox b) {
			// Boxes can merge if they share an exact dimension length and are flush against each other
			bool verticalAlign = (a.Min.x == b.Min.x && a.Max.x == b.Max.x) && (a.Max.y + 1 == b.Min.y || b.Max.y + 1 == a.Min.y);
			bool horizontalAlign = (a.Min.y == b.Min.y && a.Max.y == b.Max.y) && (a.Max.x + 1 == b.Min.x || b.Max.x + 1 == a.Min.x);
			return verticalAlign || horizontalAlign;
		}
	}
}