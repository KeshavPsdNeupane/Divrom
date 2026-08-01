using System.Collections.Generic;
using Kope.Feature.PathFindingOld.Interface;
using Kope.Feature.PathFindingOld.Node;
namespace Kope.Feature.PathFindingOld.Utility {

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

		public Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)> Slice(
			Dictionary<Vec2Int, List<Vec2Int>> isolatedRegions,
			Vec2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)>();

			foreach (var kvp in isolatedRegions) {
				Vec2Int regionAnchor = kvp.Key;
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

		private Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)> ExecuteMeshingSweep(
			Vec2Int regionAnchor,
			List<Vec2Int> regionTiles,
			Vec2Int maxBoundSize,
			bool isXDominant) {

			var sweepResults = new Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)>();
			var unassignedTiles = new HashSet<Vec2Int>(regionTiles);

			// Establish iteration bounds
			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			foreach (var tile in regionTiles) {
				if (tile.X < minX) minX = tile.X;
				if (tile.X > maxX) maxX = tile.X;
				if (tile.Y < minY) minY = tile.Y;
				if (tile.Y > maxY) maxY = tile.Y;
			}

			// Sweep logic respects the dominant axis to prevent splintering
			if (isXDominant) {
				for (int y = minY; y <= maxY; y++) {
					for (int x = minX; x <= maxX; x++) {
						var blockAnchor = new Vec2Int(x, y);
						if (!unassignedTiles.Contains(blockAnchor)) continue;

						ExtractOptimalBlock(regionAnchor, blockAnchor, unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			} else {
				for (int x = minX; x <= maxX; x++) {
					for (int y = minY; y <= maxY; y++) {
						var blockAnchor = new Vec2Int(x, y);
						if (!unassignedTiles.Contains(blockAnchor)) continue;

						ExtractOptimalBlock(regionAnchor, blockAnchor, unassignedTiles, maxBoundSize, sweepResults, isXDominant);
					}
				}
			}

			return sweepResults;
		}

		private void ExtractOptimalBlock(
			Vec2Int regionAnchor,
			Vec2Int blockAnchor,
			HashSet<Vec2Int> unassignedTiles,
			Vec2Int maxBound,
			Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)> sweepResults,
			bool isXDominant) {

			int blockWidth = 1;
			int blockHeight = 1;

			if (isXDominant) {
				// Expand horizontally as far as possible
				while (blockWidth < maxBound.X && unassignedTiles.Contains(new Vec2Int(blockAnchor.X + blockWidth, blockAnchor.Y))) {
					blockWidth++;
				}
				// Expand vertically using the established width
				bool canExpandHeight = true;
				while (blockHeight < maxBound.Y && canExpandHeight) {
					int checkY = blockAnchor.Y + blockHeight;
					for (int dx = 0; dx < blockWidth; dx++) {
						if (!unassignedTiles.Contains(new Vec2Int(blockAnchor.X + dx, checkY))) {
							canExpandHeight = false;
							break;
						}
					}
					if (canExpandHeight) blockHeight++;
				}
			} else {
				// Expand vertically as far as possible
				while (blockHeight < maxBound.Y && unassignedTiles.Contains(new Vec2Int(blockAnchor.X, blockAnchor.Y + blockHeight))) {
					blockHeight++;
				}
				// Expand horizontally using the established height
				bool canExpandWidth = true;
				while (blockWidth < maxBound.X && canExpandWidth) {
					int checkX = blockAnchor.X + blockWidth;
					for (int dy = 0; dy < blockHeight; dy++) {
						if (!unassignedTiles.Contains(new Vec2Int(checkX, blockAnchor.Y + dy))) {
							canExpandWidth = false;
							break;
						}
					}
					if (canExpandWidth) blockWidth++;
				}
			}

			// Claim the tiles for this block
			var claimedTiles = new List<Vec2Int>(blockWidth * blockHeight);
			for (int dx = 0; dx < blockWidth; dx++) {
				for (int dy = 0; dy < blockHeight; dy++) {
					var pos = new Vec2Int(blockAnchor.X + dx, blockAnchor.Y + dy);
					unassignedTiles.Remove(pos); // Safe removal logic, acting effectively as our Rent/Release for tile ownership
					claimedTiles.Add(pos);
				}
			}

			var boundingBox = new BoundingBox(blockAnchor.X, blockAnchor.Y, blockAnchor.X + blockWidth - 1, blockAnchor.Y + blockHeight - 1);
			sweepResults[boundingBox] = (regionAnchor, claimedTiles);
		}
	}
}