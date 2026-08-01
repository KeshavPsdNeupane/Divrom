using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Kope.Feature.PathFindingOld.Interface;
using Kope.Feature.PathFindingOld.Node;

namespace Kope.Feature.PathFindingOld.Utility {

	/// <summary>
	/// =========================================================================================
	/// PURE HISTOGRAM REGION SLICER (NO PRIMARY PASS, NO CLUSTERING)
	/// =========================================================================================
	/// The other two slicers in this family are two-stage: a primary pass first carves the
	/// region into SOME valid partition of rectangles (dual-axis + protrusion-aware in the
	/// original, a single-direction greedy grow in GreedyClusteringHistogramSlicer), those
	/// rectangles get grouped into spatially-connected islands via Union-Find, and only THEN does
	/// each island get a maximal-rectangle histogram sweep to find the actual near-optimal
	/// covering.
	///
	/// This slicer skips both of those stages. There is no primary pass and no clustering step -
	/// each isolatedRegions entry's raw tile list is dropped directly into one dense grid sized
	/// to that entry's own bounding box, and the exact same incremental maximal-rectangle
	/// histogram extraction loop (the one from RunPostProcessingOnCluster, reproduced verbatim
	/// below) runs once, directly, over that grid.
	///
	/// CORRECTNESS: this is safe because the histogram/maximal-rectangle extraction only ever
	/// depends on the `filled` grid's pattern of free cells - not on any rectangle boundaries a
	/// prior stage might have introduced. There is no prior stage here; `filled` is built
	/// straight from the region's tile list, so there is nothing for a prior stage to have gotten
	/// "wrong" in the first place. Output rectangles (post-SubdivideUniformly) should be
	/// byte-identical to what GreedyClusteringHistogramSlicer / AdaptiveClusteredBoundedRegion-
	/// Slicer_Iterative produce for the same isolatedRegions + maxBoundSize, PROVIDED every
	/// isolatedRegions entry really is what its name implies (a single spatially-compact blob) -
	/// see the tradeoff note below for the one case where that assumption matters.
	///
	/// THE TRADEOFF THIS VERSION IS FOR MEASURING: clustering in the other two slicers was never
	/// load-bearing for correctness - its only job was to keep each histogram grid small, bounded
	/// to one connected island's own bbox, instead of one grid sized to the whole isolatedRegions
	/// entry's bbox. This version gives that up. If an isolatedRegions entry's tiles are spatially
	/// compact (which "isolated region" suggests they should be), this is strictly less work than
	/// the two-stage versions - no primary-pass rectangle objects, no List&lt;Vector2Int&gt;
	/// allocation per primary rectangle, no Union-Find bucket pass. But if an entry's tiles are
	/// spread across a large, sparse bounding box (e.g. several small blobs far apart bundled
	/// under one dictionary key), this slicer rents and sweeps that ENTIRE bbox - including all
	/// the empty space between the blobs - where the clustered versions would have kept each blob
	/// in its own small local grid. Benchmark against real isolatedRegions data to see which case
	/// you're actually in; the macro/micro bake logs so far (537k tiles, 2558 slices, all three
	/// slicers agreeing) suggest the regions in this project are compact, but that's worth
	/// confirming rather than assuming.
	/// =========================================================================================
	/// </summary>
	public class PureHistogramRegionSlicer : IRectangleRegionSlicer {

		private struct RectResult {
			public BoundingBox Box;
			public Vec2Int Anchor;
			public List<Vec2Int> Tiles;
			public bool Locked;

			public RectResult(BoundingBox box, Vec2Int anchor, List<Vec2Int> tiles) {
				Box = box;
				Anchor = anchor;
				Tiles = tiles;
				Locked = false;
			}
		}

		public Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)> Slice(
			Dictionary<Vec2Int, List<Vec2Int>> isolatedRegions,
			Vec2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)>();

			foreach (var kvp in isolatedRegions) {
				var regionTiles = kvp.Value;
				if (regionTiles == null || regionTiles.Count == 0) continue;

				var sliced = RunHistogramExtraction(regionTiles, maxBoundSize);

				foreach (var result in sliced) {
					finalSlicedRegions[result.Box] = (kvp.Key, result.Tiles);
				}
			}
			return finalSlicedRegions;
		}

		// =====================================================================================
		// SINGLE STAGE: dense grid straight from the region's tiles, then the exact same
		// incremental maximal-rectangle histogram loop used per-cluster elsewhere.
		// =====================================================================================

		private static List<RectResult> RunHistogramExtraction(List<Vec2Int> regionTiles, Vec2Int maxBoundSize) {
			int minX = int.MaxValue, maxX = int.MinValue;
			int minY = int.MaxValue, maxY = int.MinValue;
			for (int i = 0; i < regionTiles.Count; i++) {
				var t = regionTiles[i];
				if (t.X < minX) minX = t.X;
				if (t.X > maxX) maxX = t.X;
				if (t.Y < minY) minY = t.Y;
				if (t.Y > maxY) maxY = t.Y;
			}

			int width = maxX - minX + 1;
			int height = maxY - minY + 1;
			int gridDimension = width * height;

			byte[] filled = ArrayPool<byte>.Shared.Rent(gridDimension);
			byte[] claimed = ArrayPool<byte>.Shared.Rent(gridDimension);
			Array.Clear(filled, 0, gridDimension);
			Array.Clear(claimed, 0, gridDimension);

			int[] heights = ArrayPool<int>.Shared.Rent(gridDimension);
			int[] stack = ArrayPool<int>.Shared.Rent(width + 2);
			int[] rowBestArea = ArrayPool<int>.Shared.Rent(height);
			int[] rowBestX = ArrayPool<int>.Shared.Rent(height);
			int[] rowBestY = ArrayPool<int>.Shared.Rent(height);
			int[] rowBestW = ArrayPool<int>.Shared.Rent(height);
			int[] rowBestH = ArrayPool<int>.Shared.Rent(height);
			int[] activeCols = ArrayPool<int>.Shared.Rent(width);
			int[] dirtyRowsBuf = ArrayPool<int>.Shared.Rent(height);

			try {
				for (int i = 0; i < regionTiles.Count; i++) {
					var t = regionTiles[i];
					int idx = (t.X - minX) + (t.Y - minY) * width;
					filled[idx] = 1;
				}

				var finalResults = new List<RectResult>();
				int remaining = regionTiles.Count;

				// ---- initial full build ----
				for (int y = 0; y < height; y++) {
					int rowOffset = y * width;
					int prevRowOffset = rowOffset - width;
					for (int x = 0; x < width; x++) {
						bool free = filled[rowOffset + x] == 1; // claimed is all-zero at this point
						int prev = y > 0 ? heights[prevRowOffset + x] : 0;
						heights[rowOffset + x] = free ? prev + 1 : 0;
					}
					ComputeRowBest(heights, y, width, stack,
						out rowBestArea[y], out rowBestX[y], out rowBestY[y], out rowBestW[y], out rowBestH[y]);
				}

				while (remaining > 0) {
					// Global best = first strictly-greater row-best in row-ascending order.
					int bestArea = 0, brx = 0, bry = 0, brw = 0, brh = 0;
					for (int y = 0; y < height; y++) {
						if (rowBestArea[y] > bestArea) {
							bestArea = rowBestArea[y];
							brx = rowBestX[y];
							bry = rowBestY[y];
							brw = rowBestW[y];
							brh = rowBestH[y];
						}
					}
					if (brw == 0 || brh == 0) break;

					for (int dx = 0; dx < brw; dx++) {
						for (int dy = 0; dy < brh; dy++) {
							claimed[(brx + dx) + (bry + dy) * width] = 1;
						}
					}
					remaining -= brw * brh;

					var mergedBox = new BoundingBox(minX + brx, minY + bry, minX + brx + brw - 1, minY + bry + brh - 1);
					finalResults.AddRange(SubdivideUniformly(mergedBox, maxBoundSize));

					// ---- incremental heights patch ----
					int dirtyCount = 0;

					for (int yy = bry; yy < bry + brh; yy++) {
						int rowOffset = yy * width;
						for (int x = brx; x < brx + brw; x++) heights[rowOffset + x] = 0;
						dirtyRowsBuf[dirtyCount++] = yy;
					}

					int activeCount = brw;
					for (int i = 0; i < brw; i++) activeCols[i] = brx + i;

					int y2 = bry + brh;
					while (y2 < height && activeCount > 0) {
						int rowOffset = y2 * width;
						int prevRowOffset = rowOffset - width;
						bool rowChanged = false;
						int writeIdx = 0;
						for (int i = 0; i < activeCount; i++) {
							int x = activeCols[i];
							int idx = rowOffset + x;
							bool free = filled[idx] == 1 && claimed[idx] == 0;
							int prev = heights[prevRowOffset + x];
							int newH = free ? prev + 1 : 0;
							int old = heights[idx];
							if (newH != old) {
								heights[idx] = newH;
								rowChanged = true;
								activeCols[writeIdx++] = x;
							}
						}
						activeCount = writeIdx;
						if (rowChanged) dirtyRowsBuf[dirtyCount++] = y2;
						y2++;
					}

					for (int i = 0; i < dirtyCount; i++) {
						int yy = dirtyRowsBuf[i];
						ComputeRowBest(heights, yy, width, stack,
							out rowBestArea[yy], out rowBestX[yy], out rowBestY[yy], out rowBestW[yy], out rowBestH[yy]);
					}
				}

				return finalResults;
			} finally {
				ArrayPool<byte>.Shared.Return(filled);
				ArrayPool<byte>.Shared.Return(claimed);
				ArrayPool<int>.Shared.Return(heights);
				ArrayPool<int>.Shared.Return(stack);
				ArrayPool<int>.Shared.Return(rowBestArea);
				ArrayPool<int>.Shared.Return(rowBestX);
				ArrayPool<int>.Shared.Return(rowBestY);
				ArrayPool<int>.Shared.Return(rowBestW);
				ArrayPool<int>.Shared.Return(rowBestH);
				ArrayPool<int>.Shared.Return(activeCols);
				ArrayPool<int>.Shared.Return(dirtyRowsBuf);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ComputeRowBest(
			int[] heights, int y, int width, int[] stack,
			out int bestArea, out int bestX, out int bestY, out int bestW, out int bestH) {

			int rowOffset = y * width;
			bestArea = 0;
			bestX = 0;
			bestY = 0;
			bestW = 0;
			bestH = 0;

			int stackTop = 0;
			for (int x = 0; x <= width; x++) {
				int currentHeight = x == width ? 0 : heights[rowOffset + x];
				while (stackTop > 0 && heights[rowOffset + stack[stackTop - 1]] >= currentHeight) {
					int topIdx = stack[--stackTop];
					int barHeight = heights[rowOffset + topIdx];
					int leftBound = stackTop == 0 ? 0 : stack[stackTop - 1] + 1;
					int barWidth = x - leftBound;
					int area = barHeight * barWidth;
					if (area > bestArea) {
						bestArea = area;
						bestX = leftBound;
						bestY = y - barHeight + 1;
						bestW = barWidth;
						bestH = barHeight;
					}
				}
				stack[stackTop++] = x;
			}
		}

		private static List<RectResult> SubdivideUniformly(BoundingBox box, Vec2Int maxBoundSize) {
			int totalWidth = box.Max.X - box.Min.X + 1;
			int totalHeight = box.Max.Y - box.Min.Y + 1;

			int numCols = Mathf.Max(1, Mathf.CeilToInt((float)totalWidth / maxBoundSize.X));
			int numRows = Mathf.Max(1, Mathf.CeilToInt((float)totalHeight / maxBoundSize.Y));

			var results = new List<RectResult>(numCols * numRows);

			Span<int> colWidths = stackalloc int[numCols];
			Span<int> rowHeights = stackalloc int[numRows];

			SplitEvenlySpan(totalWidth, colWidths);
			SplitEvenlySpan(totalHeight, rowHeights);

			int yCursor = box.Min.Y;
			for (int r = 0; r < numRows; r++) {
				int h = rowHeights[r];
				int xCursor = box.Min.X;
				for (int c = 0; c < numCols; c++) {
					int w = colWidths[c];
					var subAnchor = new Vec2Int(xCursor, yCursor);
					var subBox = new BoundingBox(
						xCursor, yCursor,
						xCursor + w - 1, yCursor + h - 1);

					int tileCapacity = w * h;
					var tiles = new List<Vec2Int>(tileCapacity);
					for (int dx = 0; dx < w; dx++) {
						for (int dy = 0; dy < h; dy++) {
							tiles.Add(new Vec2Int(xCursor + dx, yCursor + dy));
						}
					}

					var result = new RectResult(subBox, subAnchor, tiles) {
						Locked = true
					};
					results.Add(result);

					xCursor += w;
				}
				yCursor += h;
			}

			return results;
		}

		private static void SplitEvenlySpan(int total, Span<int> sizes) {
			int parts = sizes.Length;
			int baseSize = total / parts;
			int remainder = total % parts;
			for (int i = 0; i < parts; i++) {
				sizes[i] = baseSize + (i < remainder ? 1 : 0);
			}
		}
	}
}