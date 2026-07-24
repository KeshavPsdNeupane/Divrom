using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// Blittable output record shared by every Burst-job slicer in this family (currently
	/// PureHistogramRegionSlicer and GreedyClusteringHistogramSlicer). MinX/MinY/MaxX/MaxY
	/// replace a BoundingBox + tile list: the tiles are just the box's full extent, which is
	/// trivial to regenerate on the main thread rather than carrying a managed List through the
	/// job boundary. Key is whatever the caller used as the isolatedRegions dictionary key
	/// (matches the "anchor" second tuple element in the managed Slice() return value).
	/// Living at namespace scope, rather than nested in one class, is what lets SliceNative()
	/// callers write generic code (a NativeList&lt;SlicedRectOut&gt;) that doesn't care which
	/// slicer produced it.
	/// </summary>
	public struct SlicedRectOut {
		/// <summary>
		/// The isolatedRegions dictionary key that produced this rectangle. Matches the
		/// "anchor" second tuple element in the managed Slice() return value.
		/// </summary>
		public int2 AnchorKey;
		/// <summary>
		/// The rectangle's inclusive bounds. The caller can reconstruct the full tile list trivially
		/// by iterating over the rectangle's width and height, so the algorithm don't carry a managed List&lt;Vector2Int&gt;
		/// through the job boundary.
		/// </summary>
		public int MinX, MinY, MaxX, MaxY;
	}

	/// <summary>
	/// =========================================================================================
	/// PURE HISTOGRAM REGION SLICER — JOB SYSTEM / BURST VERSION
	/// =========================================================================================
	/// Same algorithm as the managed PureHistogramRegionSlicer (single dense grid per
	/// isolatedRegions entry, no primary pass, no clustering, incremental maximal-rectangle
	/// histogram sweep). The public Slice(...) signature is untouched, so this is a drop-in
	/// replacement for anything that consumes IRectangleRegionSlicer.
	///
	/// WHAT CHANGED AND WHY:
	/// - List<Vector2Int>, Dictionary<...>, and ArrayPool<T>.Shared are managed-heap types and
	///   cannot appear inside Burst-compiled code, so the hot loop no longer touches any of them.
	/// - Vector2Int/Mathf are replaced with Unity.Mathematics' int2/math inside the job, since
	///   those are the blittable equivalents Burst actually compiles.
	/// - The per-kvp foreach in the original Slice() becomes one IJobParallelFor index per
	///   isolatedRegions entry. Each entry's histogram sweep is entirely self-contained (its own
	///   scratch buffers, its own output bucket), so this is safe to parallelize across worker
	///   threads with zero synchronization needed between regions.
	/// - Managed marshalling (flatten Dictionary -> NativeArray going in, NativeStream ->
	///   Dictionary/List coming out) still happens on the main thread in plain C#, because the
	///   public return type requires it. That marshalling is NOT Burst-compiled — only the
	///   histogram/maximal-rectangle extraction inside HistogramExtractionJob is. If you don't
	///   need to preserve the IRectangleRegionSlicer contract, skip straight to the job below and
	///   consume its NativeStream output directly — that avoids the flatten-in/marshal-out cost
	///   entirely, which matters more the more the 537k-tile/2558-slice numbers in the original
	///   header scale up.
	///
	/// ASSUMPTIONS (I don't have BoundingBox / IRectangleRegionSlicer's source):
	/// - BoundingBox has a (int minX, int minY, int maxX, int maxY) constructor and is usable as
	///   a Dictionary key, same as the original code already required.
	/// - Swap in your real BoundingBox/IRectangleRegionSlicer if these differ.
	///
	/// REQUIRES: com.unity.burst, com.unity.collections, com.unity.mathematics packages.
	/// =========================================================================================
	/// </summary>
	public class PureHistogramRegionSlicerMarshalled : IRectangleRegionSlicer {

		// Blittable per-region metadata for the flattened input (one per isolatedRegions entry).
		private struct RegionInfo {
			public int2 Key;
			public int TileStart;
			public int TileCount;
		}

		/// <summary>
		/// MARSHALLED: matches IRectangleRegionSlicer exactly. Flattens the managed input,
		/// runs the Burst job, then marshals the NativeStream output back into the
		/// Dictionary/List shape the interface promises. Use this when you need to stay a
		/// drop-in replacement for existing callers.
		/// </summary>
		public Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)> Slice(
			Dictionary<Vec2Int, List<Vec2Int>> isolatedRegions,
			Vec2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vec2Int, List<Vec2Int>)>();
			if (isolatedRegions == null || isolatedRegions.Count == 0) return finalSlicedRegions;

			var resultStream = RunHistogramJob(isolatedRegions, maxBoundSize);
			try {
				var reader = resultStream.AsReader();
				for (int streamIndex = 0; streamIndex < reader.ForEachCount; streamIndex++) {
					int count = reader.BeginForEachIndex(streamIndex);
					for (int i = 0; i < count; i++) {
						var r = reader.Read<SlicedRectOut>();

						var box = new BoundingBox(r.MinX, r.MinY, r.MaxX, r.MaxY);
						var anchor = new Vec2Int(r.AnchorKey.x, r.AnchorKey.y);

						int w = r.MaxX - r.MinX + 1;
						int h = r.MaxY - r.MinY + 1;
						var tileList = new List<Vec2Int>(w * h);
						for (int dx = 0; dx < w; dx++) {
							for (int dy = 0; dy < h; dy++) {
								tileList.Add(new Vec2Int(r.MinX + dx, r.MinY + dy));
							}
						}

						finalSlicedRegions[box] = (anchor, tileList);
					}
					reader.EndForEachIndex();
				}
			} finally {
				resultStream.Dispose();
			}

			return finalSlicedRegions;
		}

		/// <summary>
		/// LEAN: same job, but skips the marshal-out step entirely - no BoundingBox, no
		/// per-rect List&lt;Vector2Int&gt;, no Dictionary. Returns the raw flattened results
		/// still sitting in native memory, for callers that can consume MinX/MinY/MaxX/MaxY
		/// directly (feeding a compute buffer, another job, a burst-side consumer, etc).
		/// Flatten-IN (Dictionary -> NativeArray) still happens in managed code either way -
		/// that's inherent to isolatedRegions being a managed Dictionary at the call site - but
		/// this skips the more expensive marshal-OUT (a List allocation per output rectangle).
		///
		/// CALLER OWNS THE RETURNED LIST: dispose it yourself when done (Allocator.Persistent
		/// by default so it safely outlives this call; pass Allocator.TempJob/Temp explicitly
		/// if you're consuming it within the same frame/job chain).
		/// </summary>
		public NativeList<SlicedRectOut> SliceNative(
			Dictionary<Vec2Int, List<Vec2Int>> isolatedRegions,
			Vec2Int maxBoundSize,
			Allocator resultAllocator = Allocator.Persistent) {

			var results = new NativeList<SlicedRectOut>(resultAllocator);
			if (isolatedRegions == null || isolatedRegions.Count == 0) return results;

			var resultStream = RunHistogramJob(isolatedRegions, maxBoundSize);
			try {
				var reader = resultStream.AsReader();
				for (int streamIndex = 0; streamIndex < reader.ForEachCount; streamIndex++) {
					int count = reader.BeginForEachIndex(streamIndex);
					for (int i = 0; i < count; i++) {
						results.Add(reader.Read<SlicedRectOut>());
					}
					reader.EndForEachIndex();
				}
			} finally {
				resultStream.Dispose();
			}

			return results;
		}

		/// <summary>
		/// Shared by Slice() and SliceNative(): flattens the managed input into NativeArrays,
		/// schedules+completes the Burst job, disposes the (now-unneeded) input arrays, and
		/// hands back the raw NativeStream for the caller to drain however it likes. Caller
		/// owns the returned stream and must Dispose() it.
		/// </summary>
		private static NativeStream RunHistogramJob(
			Dictionary<Vec2Int, List<Vec2Int>> isolatedRegions, Vec2Int maxBoundSize) {

			int regionCount = isolatedRegions.Count;
			int totalTiles = 0;
			foreach (var kvp in isolatedRegions) {
				if (kvp.Value != null) totalTiles += kvp.Value.Count;
			}

			var regionInfos = new NativeArray<RegionInfo>(regionCount, Allocator.TempJob);
			var allTiles = new NativeArray<int2>(math.max(1, totalTiles), Allocator.TempJob);

			int tileCursor = 0;
			int regionCursor = 0;
			foreach (var kvp in isolatedRegions) {
				var tiles = kvp.Value;
				if (tiles == null || tiles.Count == 0) continue;

				regionInfos[regionCursor] = new RegionInfo {
					Key = new int2(kvp.Key.X, kvp.Key.Y),
					TileStart = tileCursor,
					TileCount = tiles.Count
				};
				for (int i = 0; i < tiles.Count; i++) {
					allTiles[tileCursor++] = new int2(tiles[i].X, tiles[i].Y);
				}
				regionCursor++;
			}
			var activeRegionInfos = regionInfos.GetSubArray(0, regionCursor);

			// One NativeStream "for-each index" bucket per isolated region.
			var resultStream = new NativeStream(math.max(1, regionCursor), Allocator.TempJob);

			if (regionCursor > 0) {
				var job = new HistogramExtractionJob {
					Regions = activeRegionInfos,
					AllTiles = allTiles,
					MaxBoundSize = new int2(maxBoundSize.X, maxBoundSize.Y),
					Results = resultStream.AsWriter()
				};

				// Regions are fully independent -> parallel, batch size 1 since region cost
				// varies a lot (a 4-tile region and a 40,000-tile region can be neighbors).
				job.Schedule(regionCursor, 1).Complete();
			}

			regionInfos.Dispose();
			allTiles.Dispose();

			return resultStream;
		}

		// =====================================================================================
		// BURST JOB: the exact same incremental maximal-rectangle histogram loop as the managed
		// version, rewritten against blittable types. One Execute(index) == one isolatedRegions
		// entry, fully self-contained (own scratch buffers, own output bucket) so regions run in
		// parallel across worker threads with no cross-talk.
		// =====================================================================================
		[BurstCompile]
		private struct HistogramExtractionJob : IJobParallelFor {

			[ReadOnly] public NativeArray<RegionInfo> Regions;
			[ReadOnly] public NativeArray<int2> AllTiles;
			public int2 MaxBoundSize;

			public NativeStream.Writer Results;

			public void Execute(int index) {
				Results.BeginForEachIndex(index);

				var region = Regions[index];
				int tileStart = region.TileStart;
				int tileCount = region.TileCount;

				int minX = int.MaxValue, maxX = int.MinValue;
				int minY = int.MaxValue, maxY = int.MinValue;
				for (int i = 0; i < tileCount; i++) {
					var t = AllTiles[tileStart + i];
					if (t.x < minX) minX = t.x;
					if (t.x > maxX) maxX = t.x;
					if (t.y < minY) minY = t.y;
					if (t.y > maxY) maxY = t.y;
				}

				int width = maxX - minX + 1;
				int height = maxY - minY + 1;
				int gridDimension = width * height;

				// Allocator.Temp is thread-local and scoped to this single Execute() call -
				// exactly analogous to the ArrayPool rent/return pattern in the managed version,
				// just Burst-safe.
				var filled = new NativeArray<byte>(gridDimension, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var claimed = new NativeArray<byte>(gridDimension, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var heights = new NativeArray<int>(gridDimension, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var stack = new NativeArray<int>(width + 2, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var rowBestArea = new NativeArray<int>(height, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var rowBestX = new NativeArray<int>(height, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var rowBestY = new NativeArray<int>(height, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var rowBestW = new NativeArray<int>(height, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var rowBestH = new NativeArray<int>(height, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var activeCols = new NativeArray<int>(width, Allocator.Temp, NativeArrayOptions.ClearMemory);
				var dirtyRowsBuf = new NativeArray<int>(height, Allocator.Temp, NativeArrayOptions.ClearMemory);

				for (int i = 0; i < tileCount; i++) {
					var t = AllTiles[tileStart + i];
					int idx = (t.x - minX) + (t.y - minY) * width;
					filled[idx] = 1;
				}

				int remaining = tileCount;

				// ---- initial full build ----
				for (int y = 0; y < height; y++) {
					int rowOffset = y * width;
					int prevRowOffset = rowOffset - width;
					for (int x = 0; x < width; x++) {
						bool free = filled[rowOffset + x] == 1;
						int prev = y > 0 ? heights[prevRowOffset + x] : 0;
						heights[rowOffset + x] = free ? prev + 1 : 0;
					}
					ComputeRowBest(heights, y, width, stack,
						out int ba, out int bx, out int by, out int bw, out int bh);
					rowBestArea[y] = ba; rowBestX[y] = bx; rowBestY[y] = by; rowBestW[y] = bw; rowBestH[y] = bh;
				}

				while (remaining > 0) {
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

					var mergedMin = new int2(minX + brx, minY + bry);
					var mergedMax = new int2(minX + brx + brw - 1, minY + bry + brh - 1);
					EmitSubdivided(mergedMin, mergedMax, MaxBoundSize, region.Key, ref Results);

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
							out int ba, out int bx, out int by, out int bw, out int bh);
						rowBestArea[yy] = ba; rowBestX[yy] = bx; rowBestY[yy] = by; rowBestW[yy] = bw; rowBestH[yy] = bh;
					}
				}

				filled.Dispose();
				claimed.Dispose();
				heights.Dispose();
				stack.Dispose();
				rowBestArea.Dispose();
				rowBestX.Dispose();
				rowBestY.Dispose();
				rowBestW.Dispose();
				rowBestH.Dispose();
				activeCols.Dispose();
				dirtyRowsBuf.Dispose();

				Results.EndForEachIndex();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void ComputeRowBest(
				NativeArray<int> heights, int y, int width, NativeArray<int> stack,
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

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void EmitSubdivided(int2 boxMin, int2 boxMax, int2 maxBoundSize, int2 regionKey, ref NativeStream.Writer results) {
				int totalWidth = boxMax.x - boxMin.x + 1;
				int totalHeight = boxMax.y - boxMin.y + 1;

				int numCols = math.max(1, (int)math.ceil((float)totalWidth / maxBoundSize.x));
				int numRows = math.max(1, (int)math.ceil((float)totalHeight / maxBoundSize.y));

				int yCursor = boxMin.y;
				for (int r = 0; r < numRows; r++) {
					int h = SplitEvenly(totalHeight, numRows, r);
					int xCursor = boxMin.x;
					for (int c = 0; c < numCols; c++) {
						int w = SplitEvenly(totalWidth, numCols, c);

						results.Write(new SlicedRectOut {
							AnchorKey = regionKey,
							MinX = xCursor,
							MinY = yCursor,
							MaxX = xCursor + w - 1,
							MaxY = yCursor + h - 1
						});

						xCursor += w;
					}
					yCursor += h;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static int SplitEvenly(int total, int parts, int index) {
				int baseSize = total / parts;
				int remainder = total % parts;
				return baseSize + (index < remainder ? 1 : 0);
			}
		}
	}
}