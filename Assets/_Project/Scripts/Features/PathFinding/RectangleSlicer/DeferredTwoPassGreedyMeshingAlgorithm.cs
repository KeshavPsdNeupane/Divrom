using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// DEFERRED TWO-PASS GREEDY MESHING ALGORITHM
	/// =========================================================================================
	/// Pass 1: Primary slicing via robust max-area discovery. Identifies and defers protruding strips 
	///         to preserve the structural integrity of macro regions.
	/// Pass 2: Secondary slicing processing deferred narrow corridors (image-detection style pruning).
	/// Pass 3 (Post): Merges aligned isolated strips and enforces symmetrical 50/50 splitting to 
	///                avoid disproportionate residual slivers.
	/// =========================================================================================
	/// </summary>
	public class DeferredTwoPassGreedyMeshingAlgorithm : IRectangleRegionSlicer {

		// Thresholds based on the specification
		private const float STRIP_ASPECT_RATIO = 3.0f;

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;
				var processedSlices = ExecuteDeferredTwoPassSlicing(regionTiles, maxBoundSize);

				foreach (var slice in processedSlices) {
					finalSlicedRegions[slice.Key] = slice.Value;
				}
			}

			return finalSlicedRegions;
		}

		private Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> ExecuteDeferredTwoPassSlicing(
			List<Vector2Int> regionTiles,
			Vector2Int maxBoundSize) {

			var unassignedTiles = new HashSet<Vector2Int>(regionTiles);
			var deferredTiles = new HashSet<Vector2Int>();

			var macroRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			var isolatedStrips = new List<BoundingBox>();

			// Establish deterministic iteration bounds
			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			foreach (var tile in regionTiles) {
				if (tile.x < minX) minX = tile.x;
				if (tile.x > maxX) maxX = tile.x;
				if (tile.y < minY) minY = tile.y;
				if (tile.y > maxY) maxY = tile.y;
			}

			// =======================================================================
			// PASS 1: Primary Volume Slicing & Protruding Strip Deferral
			// =======================================================================
			for (int y = minY; y <= maxY; y++) {
				for (int x = minX; x <= maxX; x++) {
					var anchor = new Vector2Int(x, y);
					if (!unassignedTiles.Contains(anchor)) continue;

					ExtractPrimaryBlock(anchor, unassignedTiles, deferredTiles, maxBoundSize, macroRegions, isolatedStrips);
				}
			}

			// =======================================================================
			// PASS 2: Deferred Strip Slicing (Pruned Region Processing)
			// =======================================================================
			var unassignedDeferred = new HashSet<Vector2Int>(deferredTiles);
			var deferredList = deferredTiles.OrderBy(t => t.y).ThenBy(t => t.x).ToList();

			foreach (var anchor in deferredList) {
				if (!unassignedDeferred.Contains(anchor)) continue;

				ExtractDeferredBlock(anchor, unassignedDeferred, maxBoundSize, isolatedStrips);
			}

			// =======================================================================
			// GRAND FINALE: Strip Merging & Balanced 50/50 Splitting
			// =======================================================================
			var finalizedStrips = MergeAndSplitStrips(isolatedStrips, maxBoundSize);

			// Assemble final collection combining Macro Regions and Processed Strips
			var results = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>(macroRegions);

			foreach (var strip in finalizedStrips) {
				var tiles = GetTilesInBox(strip);
				results[strip] = (new Vector2Int(strip.Min.x, strip.Min.y), tiles);
			}

			return results;
		}

		private void ExtractPrimaryBlock(
			Vector2Int anchor,
			HashSet<Vector2Int> unassignedTiles,
			HashSet<Vector2Int> deferredTiles,
			Vector2Int maxBoundSize,
			Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> macroRegions,
			List<BoundingBox> isolatedStrips) {
			// Determine maximum width for initial scanline
			int maxW = 0;
			while (maxW < maxBoundSize.x && unassignedTiles.Contains(new Vector2Int(anchor.x + maxW, anchor.y))) {
				maxW++;
			}

			int bestW = 1, bestH = 1;
			int maxArea = 1;
			bool blockWasHalted = false;

			// Iterate permutations natively to discover maximum bounding area without redundant sweeps
			for (int w = 1; w <= maxW; w++) {
				int h = 1;
				bool haltedForProtrusion = false;

				while (h < maxBoundSize.y) {
					int checkY = anchor.y + h;
					bool rowValid = true;

					for (int dx = 0; dx < w; dx++) {
						if (!unassignedTiles.Contains(new Vector2Int(anchor.x + dx, checkY))) {
							rowValid = false;
							break;
						}
					}

					if (rowValid) {
						// PROTRUDING STRIP DEFERRAL: Check if a narrow strip opens into a macro space
						bool leftOpen = unassignedTiles.Contains(new Vector2Int(anchor.x - 1, checkY));
						bool rightOpen = unassignedTiles.Contains(new Vector2Int(anchor.x + w, checkY));

						// If space opens up, and we are currently operating as a narrow strip (w <= 2)
						if ((leftOpen || rightOpen) && (w <= 2 || (float)h / w >= 2.0f)) {
							haltedForProtrusion = true;
							break; // Halt expansion to prevent macro-region fragmentation
						}

						h++;
					} else {
						break;
					}
				}

				int area = w * h;
				if (area > maxArea) {
					maxArea = area;
					bestW = w;
					bestH = h;
					blockWasHalted = haltedForProtrusion;
				}
			}

			// Reclaim tiles for finalized block dimensions
			var claimedTiles = new List<Vector2Int>(maxArea);
			for (int dx = 0; dx < bestW; dx++) {
				for (int dy = 0; dy < bestH; dy++) {
					var pos = new Vector2Int(anchor.x + dx, anchor.y + dy);
					unassignedTiles.Remove(pos);
					claimedTiles.Add(pos);
				}
			}

			var boundingBox = new BoundingBox(anchor.x, anchor.y, anchor.x + bestW - 1, anchor.y + bestH - 1);
			float currentAspectRatio = Mathf.Max((float)bestW / bestH, (float)bestH / bestW);

			if (blockWasHalted) {
				// Record anchor/tiles into deferred queue for Pass 2 processing
				foreach (var t in claimedTiles) {
					deferredTiles.Add(t);
				}
			} else if (currentAspectRatio >= STRIP_ASPECT_RATIO) {
				// Elongated component generated and isolated
				isolatedStrips.Add(boundingBox);
			} else {
				// Stabilized macro block assigned
				macroRegions[boundingBox] = (anchor, claimedTiles);
			}
		}

		private void ExtractDeferredBlock(
			Vector2Int anchor,
			HashSet<Vector2Int> unassignedDeferred,
			Vector2Int maxBoundSize,
			List<BoundingBox> isolatedStrips) {
			// Simplified standard greedy mesh for pruned pass 2 strips
			int maxW = 0;
			while (maxW < maxBoundSize.x && unassignedDeferred.Contains(new Vector2Int(anchor.x + maxW, anchor.y))) {
				maxW++;
			}

			int bestW = 1, bestH = 1;
			int maxArea = 1;

			for (int w = 1; w <= maxW; w++) {
				int h = 1;
				while (h < maxBoundSize.y) {
					int checkY = anchor.y + h;
					bool rowValid = true;
					for (int dx = 0; dx < w; dx++) {
						if (!unassignedDeferred.Contains(new Vector2Int(anchor.x + dx, checkY))) {
							rowValid = false;
							break;
						}
					}
					if (rowValid) h++;
					else break;
				}
				int area = w * h;
				if (area > maxArea) {
					maxArea = area;
					bestW = w;
					bestH = h;
				}
			}

			for (int dx = 0; dx < bestW; dx++) {
				for (int dy = 0; dy < bestH; dy++) {
					unassignedDeferred.Remove(new Vector2Int(anchor.x + dx, anchor.y + dy));
				}
			}

			isolatedStrips.Add(new BoundingBox(anchor.x, anchor.y, anchor.x + bestW - 1, anchor.y + bestH - 1));
		}
		private List<BoundingBox> MergeAndSplitStrips(List<BoundingBox> strips, Vector2Int maxBoundSize) {
			bool merged = true;

			// 1. Orientation Matching & Controlled Merging
			while (merged) {
				merged = false;
				for (int i = 0; i < strips.Count; i++) {
					for (int j = i + 1; j < strips.Count; j++) {
						var s1 = strips[i];
						var s2 = strips[j];

						bool isHorizontal1 = (s1.Max.x - s1.Min.x) >= (s1.Max.y - s1.Min.y);
						bool isHorizontal2 = (s2.Max.x - s2.Min.x) >= (s2.Max.y - s2.Min.y);

						if (isHorizontal1 && isHorizontal2) {
							if (s1.Min.y == s2.Min.y && s1.Max.y == s2.Max.y) {
								if (s1.Max.x + 1 == s2.Min.x) {
									strips[i] = new BoundingBox(s1.Min.x, s1.Min.y, s2.Max.x, s1.Max.y);
									strips.RemoveAt(j);
									merged = true; break;
								} else if (s2.Max.x + 1 == s1.Min.x) {
									strips[i] = new BoundingBox(s2.Min.x, s1.Min.y, s1.Max.x, s1.Max.y);
									strips.RemoveAt(j);
									merged = true; break;
								}
							}
						}

						bool isVertical1 = (s1.Max.y - s1.Min.y) >= (s1.Max.x - s1.Min.x);
						bool isVertical2 = (s2.Max.y - s2.Min.y) >= (s2.Max.x - s2.Min.x);

						if (!merged && isVertical1 && isVertical2) {
							if (s1.Min.x == s2.Min.x && s1.Max.x == s2.Max.x) {
								if (s1.Max.y + 1 == s2.Min.y) {
									strips[i] = new BoundingBox(s1.Min.x, s1.Min.y, s1.Max.x, s2.Max.y);
									strips.RemoveAt(j);
									merged = true; break;
								} else if (s2.Max.y + 1 == s1.Min.y) {
									strips[i] = new BoundingBox(s1.Min.x, s2.Min.y, s1.Max.x, s1.Max.y);
									strips.RemoveAt(j);
									merged = true; break;
								}
							}
						}
					}
					if (merged) break; // Restart iteration to check new combinations
				}
			}

			// 2. Balanced 50/50 Splitting
			var balancedStrips = new List<BoundingBox>();
			foreach (var strip in strips) {
				SplitStripBalanced(strip, maxBoundSize, balancedStrips);
			}

			return balancedStrips;
		}

		private void SplitStripBalanced(BoundingBox strip, Vector2Int maxBound, List<BoundingBox> result) {
			int w = strip.Max.x - strip.Min.x + 1;
			int h = strip.Max.y - strip.Min.y + 1;

			// Strip is within bounds
			if (w <= maxBound.x && h <= maxBound.y) {
				result.Add(strip);
				return;
			}

			// Exceeds bounds: Recursive Symmetrical 50/50 Division
			if (w > maxBound.x && (w >= h || h <= maxBound.y)) {
				int halfW = w / 2;
				var left = new BoundingBox(strip.Min.x, strip.Min.y, strip.Min.x + halfW - 1, strip.Max.y);
				var right = new BoundingBox(strip.Min.x + halfW, strip.Min.y, strip.Max.x, strip.Max.y);

				SplitStripBalanced(left, maxBound, result);
				SplitStripBalanced(right, maxBound, result);
			} else if (h > maxBound.y) {
				int halfH = h / 2;
				var bottom = new BoundingBox(strip.Min.x, strip.Min.y, strip.Max.x, strip.Min.y + halfH - 1);
				var top = new BoundingBox(strip.Min.x, strip.Min.y + halfH, strip.Max.x, strip.Max.y);

				SplitStripBalanced(bottom, maxBound, result);
				SplitStripBalanced(top, maxBound, result);
			}
		}

		private List<Vector2Int> GetTilesInBox(BoundingBox box) {
			int w = box.Max.x - box.Min.x + 1;
			int h = box.Max.y - box.Min.y + 1;
			var tiles = new List<Vector2Int>(w * h);

			for (int x = box.Min.x; x <= box.Max.x; x++) {
				for (int y = box.Min.y; y <= box.Max.y; y++) {
					tiles.Add(new Vector2Int(x, y));
				}
			}
			return tiles;
		}
	}
}