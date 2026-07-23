using System.Collections.Generic;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// DUAL-AXIS GREEDY MESHING PACKING ALGORITHM
	/// =========================================================================================
	/// Implementation of the standard "Greedy Meshing" algorithm used in NavMesh voxelization.
	/// Evaluates both X-dominant (horizontal sweeps) and Y-dominant (vertical sweeps) partitioning
	/// to deterministically guarantee zero splintering and minimal slice counts in O(V) time.
	/// =========================================================================================
	/// </summary>
	public class DualAxisGreedyMeshingAlgorithm : IRectangleRegionSlicer {

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				Vector2Int regionAnchor = kvp.Key;
				var regionTiles = kvp.Value;

				// Evaluate both scanline strategies, passing down the region anchor
				var horizontalSlices = ExecuteMeshingSweep(regionAnchor, regionTiles, maxBoundSize, isXDominant: true);
				var verticalSlices = ExecuteMeshingSweep(regionAnchor, regionTiles, maxBoundSize, isXDominant: false);

				// Adopt the strategy that yielded the fewest total partitions
				var optimalSlices = horizontalSlices.Count <= verticalSlices.Count ? horizontalSlices : verticalSlices;

				foreach (var slice in optimalSlices) {
					finalSlicedRegions[slice.Key] = slice.Value;
				}
			}

			return finalSlicedRegions;
		}

		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> ExecuteMeshingSweep(
			Vector2Int regionAnchor,
			List<Vector2Int> regionTiles,
			Vector2Int maxBoundSize,
			bool isXDominant) {

			var sweepResults = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			var unassignedTiles = new HashSet<Vector2Int>(regionTiles);

			// Establish iteration bounds
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
						var blockAnchor = new Vector2Int(x, y);
						if (!unassignedTiles.Contains(blockAnchor)) continue;

						ExtractOptimalBlock(regionAnchor, blockAnchor, unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			} else {
				for (int x = minX; x <= maxX; x++) {
					for (int y = minY; y <= maxY; y++) {
						var blockAnchor = new Vector2Int(x, y);
						if (!unassignedTiles.Contains(blockAnchor)) continue;

						ExtractOptimalBlock(regionAnchor, blockAnchor, unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			}

			return sweepResults;
		}

		private void ExtractOptimalBlock(
			Vector2Int regionAnchor,
			Vector2Int blockAnchor,
			HashSet<Vector2Int> unassignedTiles,
			Vector2Int maxBound,
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> sweepResults,
			bool isXDominant) {

			int blockWidth = 1;
			int blockHeight = 1;

			if (isXDominant) {
				// Expand horizontally as far as possible
				while (blockWidth < maxBound.x && unassignedTiles.Contains(new Vector2Int(blockAnchor.x + blockWidth, blockAnchor.y))) {
					blockWidth++;
				}
				// Expand vertically using the established width
				bool canExpandHeight = true;
				while (blockHeight < maxBound.y && canExpandHeight) {
					int checkY = blockAnchor.y + blockHeight;
					for (int dx = 0; dx < blockWidth; dx++) {
						if (!unassignedTiles.Contains(new Vector2Int(blockAnchor.x + dx, checkY))) {
							canExpandHeight = false;
							break;
						}
					}
					if (canExpandHeight) blockHeight++;
				}
			} else {
				// Expand vertically as far as possible
				while (blockHeight < maxBound.y && unassignedTiles.Contains(new Vector2Int(blockAnchor.x, blockAnchor.y + blockHeight))) {
					blockHeight++;
				}
				// Expand horizontally using the established height
				bool canExpandWidth = true;
				while (blockWidth < maxBound.x && canExpandWidth) {
					int checkX = blockAnchor.x + blockWidth;
					for (int dy = 0; dy < blockHeight; dy++) {
						if (!unassignedTiles.Contains(new Vector2Int(checkX, blockAnchor.y + dy))) {
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
					var pos = new Vector2Int(blockAnchor.x + dx, blockAnchor.y + dy);
					unassignedTiles.Remove(pos); // Safe removal logic, acting effectively as our Rent/Release for tile ownership
					claimedTiles.Add(pos);
				}
			}

			var boundingBox = new BoundingBox(blockAnchor.x, blockAnchor.y, blockAnchor.x + blockWidth - 1, blockAnchor.y + blockHeight - 1);
			sweepResults[boundingBox] = (regionAnchor, claimedTiles);
		}
	}
}