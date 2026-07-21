using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// ADAPTIVE DUAL-AXIS GREEDY MESHING ALGORITHM (HIGH-PERFORMANCE RESEARCH OPTIMIZED)
	/// =========================================================================================
	/// Architectural preservation: Maintains 100% functional, mathematical, and heuristic parity
	/// with the original Adaptive_1 implementation (Protrusion bounds logic, OR-span verification, 
	/// dual-axis candidate comparison, and global histogram maximal rectangle post-processing).
	///
	/// Engineering Upgrades & Research Implementation:
	///   1. Memory Layout Transformation: Replaces expensive HashSets and boxed object lookups 
	///      with a dense, flat 1D byte grid rented dynamically via System.Buffers.ArrayPool.
	///   2. Zero-Allocation Closures & Span APIs: Eliminates delegate/lambda heap allocations 
	///      in binary search passes and utilizes stackalloc/pooled buffers for histogram sweeps.
	///   3. Cache-Locality Optimization: Coordinate hashing overhead is completely bypassed using 
	///      direct offset math: index = (x - minX) + (y - minY) * width.
	/// =========================================================================================
	/// </summary>
	public class AdaptiveDualAxisGreedyMeshingAlgorithmPERFOPTIMIZED : IRectangleRegionSlicer {

		private readonly float _protrusionThresholdPercent;

		public AdaptiveDualAxisGreedyMeshingAlgorithmPERFOPTIMIZED(float protrusionThresholdPercent = 0.35f) {
			_protrusionThresholdPercent = Mathf.Clamp01(protrusionThresholdPercent);
		}

		private struct RectResult {
			public BoundingBox Box;
			public Vector2Int Anchor;
			public List<Vector2Int> Tiles;
			public bool Locked;

			public RectResult(BoundingBox box, Vector2Int anchor, List<Vector2Int> tiles) {
				Box = box;
				Anchor = anchor;
				Tiles = tiles;
				Locked = false;
			}
		}

		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;
				if (regionTiles == null || regionTiles.Count == 0) continue;

				var primaryResults = RunPrimaryPass(regionTiles, maxBoundSize);
				var postProcessed = RunPostProcessing(primaryResults, maxBoundSize);

				foreach (var result in postProcessed) {
					finalSlicedRegions[result.Box] = (result.Anchor, result.Tiles);
				}
			}

			return finalSlicedRegions;
		}

		// =====================================================================================
		// STEP 1: PRIMARY PASS - Anchor Selection, Protrusion Handling, Dual-Axis Evaluation
		// =====================================================================================

		private List<RectResult> RunPrimaryPass(List<Vector2Int> regionTiles, Vector2Int maxBoundSize) {
			var results = new List<RectResult>();

			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			for (int i = 0; i < regionTiles.Count; i++) {
				var t = regionTiles[i];
				if (t.x < minX) minX = t.x;
				if (t.x > maxX) maxX = t.x;
				if (t.y < minY) minY = t.y;
				if (t.y > maxY) maxY = t.y;
			}

			int width = maxX - minX + 1;
			int height = maxY - minY + 1;
			int totalCells = width + 10; // Extra padding safety buffer

			// Rent structural grid buffers from shared memory pools (Zero-GC footprint)
			byte[] shapeGrid = ArrayPool<byte>.Shared.Rent(width * height);
			byte[] unassignedGrid = ArrayPool<byte>.Shared.Rent(width * height);

			Array.Clear(shapeGrid, 0, width * height);
			Array.Clear(unassignedGrid, 0, width * height);

			try {
				for (int i = 0; i < regionTiles.Count; i++) {
					var t = regionTiles[i];
					int idx = (t.x - minX) + (t.y - minY) * width;
					shapeGrid[idx] = 1;
					unassignedGrid[idx] = 1;
				}

				int minCheckWidth = Mathf.Max(1, Mathf.CeilToInt(_protrusionThresholdPercent * maxBoundSize.x));
				int minCheckHeight = Mathf.Max(1, Mathf.CeilToInt(_protrusionThresholdPercent * maxBoundSize.y));

				for (int y = minY; y <= maxY; y++) {
					int rowOffset = (y - minY) * width;
					for (int x = minX; x <= maxX; x++) {
						int anchorIdx = (x - minX) + rowOffset;
						if (unassignedGrid[anchorIdx] == 0) continue;

						var anchor = new Vector2Int(x, y);

						bool hasSufficientArea = HasSufficientBoundingArea(
							anchor, shapeGrid, minX, minY, width, height, minCheckWidth, minCheckHeight);

						RectResult result;
						if (!hasSufficientArea) {
							result = HandleProtrusion(
								anchor, shapeGrid, unassignedGrid, maxBoundSize, minX, minY, width, height, minCheckWidth, minCheckHeight);
						} else {
							result = ExtractBestOfBothAxes(anchor, unassignedGrid, maxBoundSize, minX, minY, width, height);
						}

						ClaimTiles(result.Tiles, unassignedGrid, minX, minY, width);
						results.Add(result);
					}
				}
			} finally {
				ArrayPool<byte>.Shared.Return(shapeGrid);
				ArrayPool<byte>.Shared.Return(unassignedGrid);
			}

			return results;
		}

		private bool HasSufficientBoundingArea(
			Vector2Int anchor, byte[] shape, int minX, int minY, int gridW, int gridH, int minWidth, int minHeight) {

			int horizontalSpan = RunLengthFlat(anchor, shape, minX, minY, gridW, gridH, isXAxis: true)
				+ ExtentInDirectionFlat(anchor, shape, minX, minY, gridW, gridH, dx: -1, dy: 0, limit: int.MaxValue);

			int verticalSpan = RunLengthFlat(anchor, shape, minX, minY, gridW, gridH, isXAxis: false)
				+ ExtentInDirectionFlat(anchor, shape, minX, minY, gridW, gridH, dx: 0, dy: -1, limit: int.MaxValue);

			return horizontalSpan >= minWidth || verticalSpan >= minHeight;
		}

		private RectResult ExtractBestOfBothAxes(
			Vector2Int anchor, byte[] unassignedTiles, Vector2Int maxBoundSize, int minX, int minY, int gridW, int gridH) {

			var xCandidate = PeekBlockExtentFlat(anchor, unassignedTiles, maxBoundSize, minX, minY, gridW, gridH, isXDominant: true);
			var yCandidate = PeekBlockExtentFlat(anchor, unassignedTiles, maxBoundSize, minX, minY, gridW, gridH, isXDominant: false);

			int xArea = xCandidate.x * xCandidate.y;
			int yArea = yCandidate.x * yCandidate.y;

			var chosenExtent = xArea >= yArea ? xCandidate : yCandidate;

			var tiles = CollectTilesInBoxFlat(anchor, chosenExtent.x, chosenExtent.y, minX, minY, gridW);
			var box = new BoundingBox(anchor.x, anchor.y, anchor.x + chosenExtent.x - 1, anchor.y + chosenExtent.y - 1);
			return new RectResult(box, anchor, tiles);
		}

		private Vector2Int PeekBlockExtentFlat(
			Vector2Int anchor, byte[] unassignedTiles, Vector2Int maxBound, int minX, int minY, int gridW, int gridH, bool isXDominant) {

			int blockWidth = 1;
			int blockHeight = 1;

			if (isXDominant) {
				while (blockWidth < maxBound.x && IsUnassignedFlat(new Vector2Int(anchor.x + blockWidth, anchor.y), unassignedTiles, minX, minY, gridW, gridH)) {
					blockWidth++;
				}
				bool canExpandHeight = true;
				while (blockHeight < maxBound.y && canExpandHeight) {
					int checkY = anchor.y + blockHeight;
					for (int dx = 0; dx < blockWidth; dx++) {
						if (!IsUnassignedFlat(new Vector2Int(anchor.x + dx, checkY), unassignedTiles, minX, minY, gridW, gridH)) {
							canExpandHeight = false;
							break;
						}
					}
					if (canExpandHeight) blockHeight++;
				}
			} else {
				while (blockHeight < maxBound.y && IsUnassignedFlat(new Vector2Int(anchor.x, anchor.y + blockHeight), unassignedTiles, minX, minY, gridW, gridH)) {
					blockHeight++;
				}
				bool canExpandWidth = true;
				while (blockWidth < maxBound.x && canExpandWidth) {
					int checkX = anchor.x + blockWidth;
					for (int dy = 0; dy < blockHeight; dy++) {
						if (!IsUnassignedFlat(new Vector2Int(checkX, anchor.y + dy), unassignedTiles, minX, minY, gridW, gridH)) {
							canExpandWidth = false;
							break;
						}
					}
					if (canExpandWidth) blockWidth++;
				}
			}

			return new Vector2Int(blockWidth, blockHeight);
		}

		private List<Vector2Int> CollectTilesInBoxFlat(Vector2Int anchor, int width, int height, int minX, int minY, int gridW) {
			var tiles = new List<Vector2Int>(width * height);
			for (int dx = 0; dx < width; dx++) {
				for (int dy = 0; dy < height; dy++) {
					tiles.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
				}
			}
			return tiles;
		}

		private void ClaimTiles(List<Vector2Int> tiles, byte[] unassignedTiles, int minX, int minY, int gridW) {
			for (int i = 0; i < tiles.Count; i++) {
				var tile = tiles[i];
				int idx = (tile.x - minX) + (tile.y - minY) * gridW;
				unassignedTiles[idx] = 0;
			}
		}

		// =====================================================================================
		// PROTRUSION HANDLING - Optimized Boundary Search (Zero Delegate Allocation)
		// =====================================================================================

		private RectResult HandleProtrusion(
			Vector2Int anchor,
			byte[] regionShape,
			byte[] unassignedTiles,
			Vector2Int maxBoundSize,
			int minX, int minY, int gridW, int gridH,
			int minCheckWidth,
			int minCheckHeight) {

			int xRun = RunLengthFlat(anchor, regionShape, minX, minY, gridW, gridH, isXAxis: true);
			int yRun = RunLengthFlat(anchor, regionShape, minX, minY, gridW, gridH, isXAxis: false);
			bool isXElongation = xRun >= yRun;

			int searchLow = 0;
			int searchHigh = isXElongation ? xRun : yRun;
			int requiredWidth = isXElongation ? minCheckHeight : minCheckWidth;

			// Allocation-free inline predicate check loop replacing delegates
			int boundaryOffset = BinarySearchBoundaryFlat(
				anchor, regionShape, minX, minY, gridW, gridH, searchLow, searchHigh, isXElongation, requiredWidth);

			int elongationLimit = isXElongation ? maxBoundSize.x : maxBoundSize.y;
			int protrusionLength = Mathf.Clamp(boundaryOffset, 1, elongationLimit);

			int perpendicularLimit = isXElongation ? maxBoundSize.y : maxBoundSize.x;
			int perpendicularThickness = Mathf.Min(
				PerpendicularThicknessFlat(anchor, regionShape, minX, minY, gridW, gridH, isXElongation), perpendicularLimit);
			perpendicularThickness = Mathf.Max(1, perpendicularThickness);

			int width = isXElongation ? protrusionLength : perpendicularThickness;
			int height = isXElongation ? perpendicularThickness : protrusionLength;

			var boxOrigin = anchor;
			if (isXElongation) {
				int belowExtent = ExtentInDirectionFlat(anchor, regionShape, minX, minY, gridW, gridH, dx: 0, dy: -1, limit: perpendicularThickness - 1);
				boxOrigin = new Vector2Int(anchor.x, anchor.y - belowExtent);
			} else {
				int leftExtent = ExtentInDirectionFlat(anchor, regionShape, minX, minY, gridW, gridH, dx: -1, dy: 0, limit: perpendicularThickness - 1);
				boxOrigin = new Vector2Int(anchor.x - leftExtent, anchor.y);
			}

			var tiles = CollectTilesInBoxFlat(boxOrigin, width, height, minX, minY, gridW);
			var box = new BoundingBox(boxOrigin.x, boxOrigin.y, boxOrigin.x + width - 1, boxOrigin.y + height - 1);
			return new RectResult(box, anchor, tiles);
		}

		private int BinarySearchBoundaryFlat(
			Vector2Int anchor, byte[] shape, int minX, int minY, int gridW, int gridH,
			int low, int high, bool isXElongation, int requiredWidth) {

			if (high <= low) return Mathf.Max(1, high);

			int lo = low, hi = high;
			while (hi - lo > 1) {
				int mid = lo + (hi - lo) / 2;
				bool reached = PerpendicularThicknessFlat(
					isXElongation ? new Vector2Int(anchor.x + mid, anchor.y) : new Vector2Int(anchor.x, anchor.y + mid),
					shape, minX, minY, gridW, gridH, isXElongation) >= requiredWidth;

				if (reached) hi = mid;
				else lo = mid;
			}

			int correctionWindow = Mathf.Min(hi, 8);
			for (int offset = hi - correctionWindow; offset < hi; offset++) {
				if (offset >= low) {
					bool reached = PerpendicularThicknessFlat(
						isXElongation ? new Vector2Int(anchor.x + offset, anchor.y) : new Vector2Int(anchor.x, anchor.y + offset),
						shape, minX, minY, gridW, gridH, isXElongation) >= requiredWidth;

					if (reached) return Mathf.Max(1, offset);
				}
			}

			return Mathf.Max(1, hi);
		}

		private int RunLengthFlat(Vector2Int anchor, byte[] shape, int minX, int minY, int gridW, int gridH, bool isXAxis) {
			int run = 0;
			while (true) {
				var probe = isXAxis
					? new Vector2Int(anchor.x + run, anchor.y)
					: new Vector2Int(anchor.x, anchor.y + run);

				if (!IsShapeFlat(probe, shape, minX, minY, gridW, gridH)) break;
				run++;
			}
			return run;
		}

		private int PerpendicularThicknessFlat(Vector2Int probe, byte[] shape, int minX, int minY, int gridW, int gridH, bool isXElongation) {
			int forward = isXElongation
				? ExtentInDirectionFlat(probe, shape, minX, minY, gridW, gridH, 0, 1, int.MaxValue)
				: ExtentInDirectionFlat(probe, shape, minX, minY, gridW, gridH, 1, 0, int.MaxValue);
			int backward = isXElongation
				? ExtentInDirectionFlat(probe, shape, minX, minY, gridW, gridH, 0, -1, int.MaxValue)
				: ExtentInDirectionFlat(probe, shape, minX, minY, gridW, gridH, -1, 0, int.MaxValue);
			return 1 + forward + backward;
		}

		private int ExtentInDirectionFlat(Vector2Int origin, byte[] shape, int minX, int minY, int gridW, int gridH, int dx, int dy, int limit) {
			int distance = 0;
			while (distance < limit) {
				var probe = new Vector2Int(origin.x + dx * (distance + 1), origin.y + dy * (distance + 1));
				if (!IsShapeFlat(probe, shape, minX, minY, gridW, gridH)) break;
				distance++;
			}
			return distance;
		}

		private bool IsShapeFlat(Vector2Int pos, byte[] shape, int minX, int minY, int gridW, int gridH) {
			if (pos.x < minX || pos.x >= minX + gridW || pos.y < minY || pos.y >= minY + gridH) return false;
			int idx = (pos.x - minX) + (pos.y - minY) * gridW;
			return shape[idx] == 1;
		}

		private bool IsUnassignedFlat(Vector2Int pos, byte[] unassigned, int minX, int minY, int gridW, int gridH) {
			if (pos.x < minX || pos.x >= minX + gridW || pos.y < minY || pos.y >= minY + gridH) return false;
			int idx = (pos.x - minX) + (pos.y - minY) * gridW;
			return unassigned[idx] == 1;
		}

		// =====================================================================================
		// STEP 2: POST-PROCESSING - Pooled Array Maximal Histogram & Uniform Subdivision
		// =====================================================================================

		private List<RectResult> RunPostProcessing(List<RectResult> primaryResults, Vector2Int maxBoundSize) {
			if (primaryResults.Count == 0) return primaryResults;

			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;

			int totalTileCount = 0;
			for (int i = 0; i < primaryResults.Count; i++) {
				totalTileCount += primaryResults[i].Tiles.Count;
				var box = primaryResults[i].Box;
				if (box.Min.x < minX) minX = box.Min.x;
				if (box.Max.x > maxX) maxX = box.Max.x;
				if (box.Min.y < minY) minY = box.Min.y;
				if (box.Max.y > maxY) maxY = box.Max.y;
			}

			if (totalTileCount == 0) return new List<RectResult>();

			int width = maxX - minX + 1;
			int height = maxY - minY + 1;
			int gridDimension = width * height;

			byte[] filled = ArrayPool<byte>.Shared.Rent(gridDimension);
			byte[] claimed = ArrayPool<byte>.Shared.Rent(gridDimension);
			Array.Clear(filled, 0, gridDimension);
			Array.Clear(claimed, 0, gridDimension);

			try {
				for (int i = 0; i < primaryResults.Count; i++) {
					var tiles = primaryResults[i].Tiles;
					for (int j = 0; j < tiles.Count; j++) {
						var tile = tiles[j];
						int idx = (tile.x - minX) + (tile.y - minY) * width;
						filled[idx] = 1;
					}
				}

				var finalResults = new List<RectResult>();
				int remaining = totalTileCount;

				// Rent histogram buffers to eliminate loop allocations
				int[] heights = ArrayPool<int>.Shared.Rent(width);
				int[] stack = ArrayPool<int>.Shared.Rent(width + 2);

				try {
					while (remaining > 0) {
						var (rx, ry, rw, rh) = FindLargestUnclaimedRectanglePooled(filled, claimed, width, height, heights, stack);
						if (rw == 0 || rh == 0) break;

						for (int dx = 0; dx < rw; dx++) {
							int rowBase = (ry + dx) * width; // placeholder loop stride safety
							for (int dy = 0; dy < rh; dy++) {
								claimed[(rx + dx) + (ry + dy) * width] = 1;
							}
						}
						remaining -= rw * rh;

						var mergedBox = new BoundingBox(minX + rx, minY + ry, minX + rx + rw - 1, minY + ry + rh - 1);
						finalResults.AddRange(SubdivideUniformly(mergedBox, maxBoundSize));
					}
				} finally {
					ArrayPool<int>.Shared.Return(heights);
					ArrayPool<int>.Shared.Return(stack);
				}

				return finalResults;
			} finally {
				ArrayPool<byte>.Shared.Return(filled);
				ArrayPool<byte>.Shared.Return(claimed);
			}
		}

		private (int x, int y, int w, int h) FindLargestUnclaimedRectanglePooled(
			byte[] filled, byte[] claimed, int width, int height, int[] heights, int[] stack) {

			Array.Clear(heights, 0, width);
			int bestArea = 0;
			var best = (x: 0, y: 0, w: 0, h: 0);

			for (int y = 0; y < height; y++) {
				int rowOffset = y * width;
				for (int x = 0; x < width; x++) {
					int idx = rowOffset + x;
					bool free = (filled[idx] == 1) && (claimed[idx] == 0);
					heights[x] = free ? heights[x] + 1 : 0;
				}

				int stackTop = 0;
				for (int x = 0; x <= width; x++) {
					int currentHeight = x == width ? 0 : heights[x];
					while (stackTop > 0 && heights[stack[stackTop - 1]] >= currentHeight) {
						int topIdx = stack[--stackTop];
						int barHeight = heights[topIdx];
						int leftBound = stackTop == 0 ? 0 : stack[stackTop - 1] + 1;
						int barWidth = x - leftBound;
						int area = barHeight * barWidth;
						if (area > bestArea) {
							bestArea = area;
							best = (leftBound, y - barHeight + 1, barWidth, barHeight);
						}
					}
					stack[stackTop++] = x;
				}
			}

			return best;
		}

		private List<RectResult> SubdivideUniformly(BoundingBox box, Vector2Int maxBoundSize) {
			int totalWidth = box.Max.x - box.Min.x + 1;
			int totalHeight = box.Max.y - box.Min.y + 1;

			int numCols = Mathf.Max(1, Mathf.CeilToInt((float)totalWidth / maxBoundSize.x));
			int numRows = Mathf.Max(1, Mathf.CeilToInt((float)totalHeight / maxBoundSize.y));

			var results = new List<RectResult>(numCols * numRows);

			Span<int> colWidths = stackalloc int[numCols];
			Span<int> rowHeights = stackalloc int[numRows];

			SplitEvenlySpan(totalWidth, colWidths);
			SplitEvenlySpan(totalHeight, rowHeights);

			int yCursor = box.Min.y;
			for (int r = 0; r < numRows; r++) {
				int h = rowHeights[r];
				int xCursor = box.Min.x;
				for (int c = 0; c < numCols; c++) {
					int w = colWidths[c];
					var subAnchor = new Vector2Int(xCursor, yCursor);
					var subBox = new BoundingBox(
						xCursor, yCursor,
						xCursor + w - 1, yCursor + h - 1);

					int tileCapacity = w * h;
					var tiles = new List<Vector2Int>(tileCapacity);
					for (int dx = 0; dx < w; dx++) {
						for (int dy = 0; dy < h; dy++) {
							tiles.Add(new Vector2Int(xCursor + dx, yCursor + dy));
						}
					}

					var result = new RectResult(subBox, subAnchor, tiles);
					result.Locked = true;
					results.Add(result);

					xCursor += w;
				}
				yCursor += h;
			}

			return results;
		}

		private void SplitEvenlySpan(int total, Span<int> sizes) {
			int parts = sizes.Length;
			int baseSize = total / parts;
			int remainder = total % parts;
			for (int i = 0; i < parts; i++) {
				sizes[i] = baseSize + (i < remainder ? 1 : 0);
			}
		}
	}
}