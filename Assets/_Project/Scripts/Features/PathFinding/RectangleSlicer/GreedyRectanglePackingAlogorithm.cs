using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Kope.Feature.PathFinding;
using Kope.Feature.PathFinding.Interface;
using Kope.Feature.PathFinding.Utility;
using UnityEngine;

public class GreedyRectanglePackingAlogorithm : IRectangleRegionSlicer {
	// =========================================================================
	// HHSI-SPECIFIC GREEDY RECTANGLE PACKING ALGORITHM
	// =========================================================================
	// Note: Since this implementation is custom-tailored specifically for the HHSI 
	// pathfinding framework, it intentionally departs from a general-purpose, bin-packing 
	// optimization routine. Instead of trying to achieve mathematically minimal texture atlases 
	// or absolute space efficiency (NP-hard bin packing), this uses a fast, deterministic, 
	// greedy raster scan. It evaluates contiguous blocks of matching terrain data, movement 
	// rules, and narrative accessibility flags row-by-row, locking in the largest valid bounding 
	// box it can immediately capture before moving forward. While this can occasionally 
	// produce suboptimal macro-node splitting compared to global optimization solvers, it 
	// executes instantly at bake-time and provides a predictable, stable zoning topology 
	// optimized for hierarchical pathfinding.
	public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions, Vector2Int maxBoundSize) {
		if (isolatedRegions == null || isolatedRegions.Count == 0) {
			Debug.LogError("GreedyRectanglePackingAlgorithm: No regions provided for slicing.");
			return null;
		}

		// pre allocating atleast 2x size of isolatedRegions to avoid rehashing and resizing of dictionary
		// if more needed, it will resize automatically but this is a good starting point
		var slicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>(isolatedRegions.Count * 2);

		foreach (var kvp in isolatedRegions) {
			Vector2Int anchor = kvp.Key;
			List<Vector2Int> singleRegionTiles = kvp.Value;

			// Pass the master dictionary reference and max constraint down to be mutated in-place
			SliceTheRegionsAlgorithm(anchor, singleRegionTiles, maxBoundSize, slicedRegions);
		}

		return slicedRegions;
	}

	// Optimization Note (seed selection): Before, picking the next rectangle's seed tile called
	// unvisitedTiles.AsValueEnumerable().First(), which rescans the HashSet from its first bucket
	// every time it's called. Since tiles get removed from unvisitedTiles in raster/greedy order
	// rather than bucket order, a region that fragments into many small rectangles could trigger
	// an O(n) rescan per rectangle — O(n * rectangleCount) overall in the worst case. Now,
	// singleRegionTiles is walked once with a forward-only searchIndex, skipping tiles that have
	// already been claimed, making seed selection amortized O(n) for the whole region.
	//
	// Optimization Note (hashing): unvisitedTiles now uses Vector2IntComparer.Instance instead of
	// the default Vector2Int comparer. Before, the default hash collided on grid-adjacent
	// coordinates, so every TryExpand* call below — which is itself an inner loop of Contains()
	// checks — paid for chained bucket lookups instead of near-direct ones. Now, lookups spread
	// across the hash table evenly, keeping each Contains() check close to O(1) even on dense,
	// large regions.
	private void SliceTheRegionsAlgorithm(
		Vector2Int anchor,
		List<Vector2Int> singleRegionTiles,
		Vector2Int maxBoundSize,
		Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> destinationDict) {

		var unvisitedTiles = new HashSet<Vector2Int>(singleRegionTiles, Vector2IntComparer.Instance);
		bool usedFirstAnchor = false;
		int searchIndex = 0;

		while (unvisitedTiles.Count > 0) {
			// Grab the starting anchor point for this specific rectangle box
			Vector2Int anchorPos;
			if (!usedFirstAnchor) {
				anchorPos = anchor;
				usedFirstAnchor = true;
			} else {
				while (!unvisitedTiles.Contains(singleRegionTiles[searchIndex])) {
					searchIndex++;
				}
				anchorPos = singleRegionTiles[searchIndex];
			}

			// A rectangle starting from anchorPos must be completely solid and unvisited 
			// across its entire span. We grow it greedily while respecting maxBoundSize.
			int minX = anchorPos.x;
			int maxX = anchorPos.x;
			int minY = anchorPos.y;
			int maxY = anchorPos.y;

			bool isExpandible = true;
			while (isExpandible) {
				isExpandible = false;

				// Try expanding Right (increase maxX) if it doesn't exceed width constraint and full column is unvisited
				if ((maxX - minX + 1) < maxBoundSize.x && TryExpandRight(unvisitedTiles, minX, maxX, minY, maxY)) {
					maxX++;
					isExpandible = true;
				}
				// Try expanding Up (increase maxY) if it doesn't exceed height constraint and full row is unvisited
				else if ((maxY - minY + 1) < maxBoundSize.y && TryExpandUp(unvisitedTiles, minX, maxX, minY, maxY)) {
					maxY++;
					isExpandible = true;
				}
				// Try expanding Left (decrease minX) if it doesn't exceed width constraint and full column is unvisited
				else if ((maxX - minX + 1) < maxBoundSize.x && TryExpandLeft(unvisitedTiles, minX, maxX, minY, maxY)) {
					minX--;
					isExpandible = true;
				}
				// Try expanding Down (decrease minY) if it doesn't exceed height constraint and full row is unvisited
				else if ((maxY - minY + 1) < maxBoundSize.y && TryExpandDown(unvisitedTiles, minX, maxX, minY, maxY)) {
					minY--;
					isExpandible = true;
				}
			}

			// Optimization Note (box collection): Before, tiles were gathered in one pass with a
			// redundant "if (unvisited.Contains(pos))" guard, then removed in a second pass. Every
			// expansion step above only commits after verifying the entire new row/column is
			// present in unvisitedTiles, so by induction the whole locked-in
			// [minX,maxX] x [minY,maxY] box is guaranteed fully unvisited by the time we get here —
			// making that guard always true. Now, tiles are collected and removed in a single pass,
			// and the list is pre-sized to the exact known box area so it never has to grow/reallocate.
			int boxWidth = maxX - minX + 1;
			int boxHeight = maxY - minY + 1;
			var currentBoxTiles = new List<Vector2Int>(boxWidth * boxHeight);
			for (int x = minX; x <= maxX; x++) {
				for (int y = minY; y <= maxY; y++) {
					var pos = new Vector2Int(x, y);
					currentBoxTiles.Add(pos);
					unvisitedTiles.Remove(pos);
				}
			}

			// Construct final BoundingBox
			var boundingBox = new BoundingBox(minX, minY, maxX, maxY);

			// Commit to the destination dictionary:
			// Maps the BoundingBox to a tuple containing the region's origin anchor point (from args)
			// and the list of grid positions contained inside this slice.
			destinationDict[boundingBox] = (anchor, currentBoxTiles);
		}
	}

	// Optimization Note: These four expand checks sit in the innermost loop of the packing
	// algorithm, called repeatedly per rectangle as it grows one row/column at a time. Before,
	// each was a normal (non-inlined) method call. Now, AggressiveInlining hints the JIT to fold
	// their bodies directly into the caller, removing call/return overhead on a path that can run
	// many thousands of times per bake for large maps. Logic is untouched — same loop, same
	// early-out on the first missing tile.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryExpandRight(HashSet<Vector2Int> unvisited, int minX, int maxX, int minY, int maxY) {
		int nextX = maxX + 1;
		for (int y = minY; y <= maxY; y++) {
			if (!unvisited.Contains(new Vector2Int(nextX, y))) return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryExpandUp(HashSet<Vector2Int> unvisited, int minX, int maxX, int minY, int maxY) {
		int nextY = maxY + 1;
		for (int x = minX; x <= maxX; x++) {
			if (!unvisited.Contains(new Vector2Int(x, nextY))) return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryExpandLeft(HashSet<Vector2Int> unvisited, int minX, int maxX, int minY, int maxY) {
		int nextX = minX - 1;
		for (int y = minY; y <= maxY; y++) {
			if (!unvisited.Contains(new Vector2Int(nextX, y))) return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryExpandDown(HashSet<Vector2Int> unvisited, int minX, int maxX, int minY, int maxY) {
		int nextY = minY - 1;
		for (int x = minX; x <= maxX; x++) {
			if (!unvisited.Contains(new Vector2Int(x, nextY))) return false;
		}
		return true;
	}
}