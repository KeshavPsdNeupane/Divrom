using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Kope.Feature.PathFinding.Interface;

namespace Kope.Feature.PathFinding.Utility {

	/// <summary>
	/// =========================================================================================
	/// ADAPTIVE GREEDY MESHING ALGORITHM — JOB SYSTEM / BURST VERSION
	/// =========================================================================================
	/// Same two-stage algorithm as the managed GreedyClusteringHistogramSlicer: stage 1 greedily
	/// carves each isolatedRegions entry into a valid (not necessarily optimal) set of
	/// rectangles via a padded-grid greedy right-then-down grow; stage 2 clusters those
	/// rectangles by spatial adjacency (Union-Find over a coarse bucket hash) and re-optimizes
	/// each cluster with the same incremental maximal-rectangle histogram sweep used by the
	/// Pure slicer. Both stages now run inside one [BurstCompile] IJobParallelFor.Execute(index)
	/// call per isolatedRegions entry, mirroring the original's per-kvp foreach exactly - so
	/// regions are independent and parallelize across worker threads the same way.
	///
	/// SHARES ITS OUTPUT TYPE (SlicedRectOut) WITH PureHistogramRegionSlicer.Burst.cs - see that
	/// file for the type. Both are namespace-scope so either slicer's SliceNative() result can
	/// be handled generically.
	///
	/// WHAT MADE THIS ONE STRAIGHTFORWARD TO PORT, AND WHAT DIDN'T:
	/// - Stage 1 never needed its tile Lists for anything except handing them to stage 2 as
	///   "the tiles this primary rectangle covers" - and since a greedy-grown block is always a
	///   solid rectangle, those tiles are always exactly the block's own extent. So stage 1's
	///   NativeList<PrimaryRect> only stores MinX/MinY/MaxX/MaxY per rectangle (no per-tile
	///   List<Vector2Int>, no per-tile allocation at all) - the same "tiles == box extent" trick
	///   used to drop the tile-List from the Pure slicer's SlicedRectOut.
	/// - Stage 2's cluster-membership grouping (originally a Dictionary<int, List<RectResult>>
	///   built from Union-Find roots) has no direct Burst equivalent for arbitrary grouping, so
	///   it's replaced with a two-pass scan: pass A path-compresses every root, pass B walks the
	///   n primary rects once per not-yet-visited cluster, collecting members with a matching
	///   root into a reused NativeArray<int> buffer. That's O(n) per cluster-start instead of an
	///   O(1) dictionary bucket append, so worst case (a region that is entirely disconnected
	///   singletons) is O(n^2) instead of O(n). For anything resembling real level geometry -
	///   where isolatedRegions entries are spatially compact and stage 1's rectangles mostly DO
	///   touch each other - actual cluster counts stay low and this is a non-issue; benchmark if
	///   your data skews toward many small disconnected primary rects per region.
	/// - The adjacency bucket hash itself ports cleanly to NativeParallelMultiHashMap<int2,int>
	///   (int2 already implements IEquatable<int2>), sized via an exact two-pass count instead of
	///   a guess, so no mid-job rehash.
	///
	/// REQUIRES: com.unity.burst, com.unity.collections, com.unity.mathematics packages.
	/// ASSUMPTIONS: same BoundingBox/IRectangleRegionSlicer assumptions as the Pure slicer's
	/// Burst file - see that file's header.
	/// =========================================================================================
	/// </summary>
	public class GreedyClusteringHistogramSlicerMarshalled : IRectangleRegionSlicer {

		// Blittable per-region metadata for the flattened input (one per isolatedRegions entry).
		private struct RegionInfo {
			public int2 Key;
			public int TileStart;
			public int TileCount;
		}

		// Stage-1 output: a greedily-grown rectangle. No tile list - see class doc, "tiles ==
		// box extent" for why that's safe to drop.
		private struct PrimaryRect {
			public int MinX, MinY, MaxX, MaxY;
		}

		/// <summary>
		/// MARSHALLED: matches IRectangleRegionSlicer exactly. Use this when you need to stay a
		/// drop-in replacement for existing callers.
		/// </summary>
		public Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize) {

			var finalSlicedRegions = new Dictionary<BoundingBox, (Vector2Int, List<Vector2Int>)>();
			if (isolatedRegions == null || isolatedRegions.Count == 0) return finalSlicedRegions;

			var resultStream = RunClusteringJob(isolatedRegions, maxBoundSize);
			try {
				var reader = resultStream.AsReader();
				for (int streamIndex = 0; streamIndex < reader.ForEachCount; streamIndex++) {
					int count = reader.BeginForEachIndex(streamIndex);
					for (int i = 0; i < count; i++) {
						var r = reader.Read<SlicedRectOut>();

						var box = new BoundingBox(r.MinX, r.MinY, r.MaxX, r.MaxY);
						var anchor = new Vector2Int(r.AnchorKey.x, r.AnchorKey.y);

						int w = r.MaxX - r.MinX + 1;
						int h = r.MaxY - r.MinY + 1;
						var tileList = new List<Vector2Int>(w * h);
						for (int dx = 0; dx < w; dx++) {
							for (int dy = 0; dy < h; dy++) {
								tileList.Add(new Vector2Int(r.MinX + dx, r.MinY + dy));
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
		/// LEAN: skips the marshal-out step entirely - no BoundingBox, no per-rect
		/// List&lt;Vector2Int&gt;, no Dictionary. Returns the raw flattened results still
		/// sitting in native memory. CALLER OWNS THE RETURNED LIST: dispose it yourself
		/// (Allocator.Persistent by default; pass Allocator.TempJob/Temp explicitly if you're
		/// consuming it within the same frame/job chain).
		/// </summary>
		public NativeList<SlicedRectOut> SliceNative(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions,
			Vector2Int maxBoundSize,
			Allocator resultAllocator = Allocator.Persistent) {

			var results = new NativeList<SlicedRectOut>(resultAllocator);
			if (isolatedRegions == null || isolatedRegions.Count == 0) return results;

			var resultStream = RunClusteringJob(isolatedRegions, maxBoundSize);
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
		/// Shared by Slice() and SliceNative(): flattens the managed input, schedules+completes
		/// the Burst job, disposes the (now-unneeded) input arrays, and hands back the raw
		/// NativeStream for the caller to drain. Caller owns the returned stream and must
		/// Dispose() it.
		/// </summary>
		private static NativeStream RunClusteringJob(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions, Vector2Int maxBoundSize) {

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
					Key = new int2(kvp.Key.x, kvp.Key.y),
					TileStart = tileCursor,
					TileCount = tiles.Count
				};
				for (int i = 0; i < tiles.Count; i++) {
					allTiles[tileCursor++] = new int2(tiles[i].x, tiles[i].y);
				}
				regionCursor++;
			}
			var activeRegionInfos = regionInfos.GetSubArray(0, regionCursor);

			var resultStream = new NativeStream(math.max(1, regionCursor), Allocator.TempJob);

			if (regionCursor > 0) {
				var job = new GreedyClusteringHistogramJob {
					Regions = activeRegionInfos,
					AllTiles = allTiles,
					MaxBoundSize = new int2(maxBoundSize.x, maxBoundSize.y),
					Results = resultStream.AsWriter()
				};

				// Regions are fully independent -> parallel, batch size 1 (region cost varies a
				// lot - a 4-tile region and a 40,000-tile fragmented region can be neighbors).
				job.Schedule(regionCursor, 1).Complete();
			}

			regionInfos.Dispose();
			allTiles.Dispose();

			return resultStream;
		}

		// =====================================================================================
		// BURST JOB: stage 1 (greedy carve) + stage 2 (adjacency clustering + per-cluster
		// incremental maximal-rectangle sweep), both scoped to one isolatedRegions entry per
		// Execute(index) call, fully self-contained.
		// =====================================================================================
		[BurstCompile]
		private struct GreedyClusteringHistogramJob : IJobParallelFor {

			[ReadOnly] public NativeArray<RegionInfo> Regions;
			[ReadOnly] public NativeArray<int2> AllTiles;
			public int2 MaxBoundSize;

			public NativeStream.Writer Results;

			public void Execute(int index) {
				Results.BeginForEachIndex(index);

				var region = Regions[index];
				int tileStart = region.TileStart;
				int tileCount = region.TileCount;

				int regionMinX = int.MaxValue, regionMaxX = int.MinValue;
				int regionMinY = int.MaxValue, regionMaxY = int.MinValue;
				for (int i = 0; i < tileCount; i++) {
					var t = AllTiles[tileStart + i];
					if (t.x < regionMinX) regionMinX = t.x;
					if (t.x > regionMaxX) regionMaxX = t.x;
					if (t.y < regionMinY) regionMinY = t.y;
					if (t.y > regionMaxY) regionMaxY = t.y;
				}

				int width = regionMaxX - regionMinX + 1;
				int height = regionMaxY - regionMinY + 1;

				// ---- STAGE 1: greedy carve, sentinel-padded grid (see managed version's doc
				// comment point 7 for why the padding removes the bounds check from every probe) ----
				int strideW = width + 1;
				int paddedDimension = strideW * (height + 1);

				var unassignedGrid = new NativeArray<byte>(paddedDimension, Allocator.Temp, NativeArrayOptions.ClearMemory);
				for (int i = 0; i < tileCount; i++) {
					var t = AllTiles[tileStart + i];
					int idx = (t.x - regionMinX) + (t.y - regionMinY) * strideW;
					unassignedGrid[idx] = 1;
				}

				var primaryResults = new NativeList<PrimaryRect>(Allocator.Temp);
				for (int y = 0; y < height; y++) {
					int rowOffset = y * strideW;
					for (int x = 0; x < width; x++) {
						int anchorIdx = x + rowOffset;
						if (unassignedGrid[anchorIdx] == 0) continue;

						var rect = ExtractAndClaimGreedyRect(
							x, y, unassignedGrid, MaxBoundSize, strideW, anchorIdx, regionMinX, regionMinY);
						primaryResults.Add(rect);
					}
				}
				unassignedGrid.Dispose();

				// ---- STAGE 2: adjacency clustering (Union-Find over a bucket hash) ----
				int n = primaryResults.Length;
				var parent = new NativeArray<int>(n, Allocator.Temp);
				for (int i = 0; i < n; i++) parent[i] = i;

				int bucketW = math.max(1, MaxBoundSize.x * 2);
				int bucketH = math.max(1, MaxBoundSize.y * 2);

				// Exact two-pass bucket population: pass A sizes the multimap, pass B fills it,
				// so there's no mid-job rehash. bucketRange caches each rect's bucket span so
				// pass B doesn't recompute FloorDiv four more times per rect.
				var bucketRange = new NativeArray<int4>(n, Allocator.Temp);
				int capacityNeeded = 0;
				for (int i = 0; i < n; i++) {
					var r = primaryResults[i];
					int minBx = FloorDiv(r.MinX, bucketW), maxBx = FloorDiv(r.MaxX, bucketW);
					int minBy = FloorDiv(r.MinY, bucketH), maxBy = FloorDiv(r.MaxY, bucketH);
					bucketRange[i] = new int4(minBx, maxBx, minBy, maxBy);
					capacityNeeded += (maxBx - minBx + 1) * (maxBy - minBy + 1);
				}

				var buckets = new NativeParallelMultiHashMap<int2, int>(math.max(1, capacityNeeded), Allocator.Temp);
				for (int i = 0; i < n; i++) {
					var br = bucketRange[i];
					for (int bx = br.x; bx <= br.y; bx++) {
						for (int by = br.z; by <= br.w; by++) {
							buckets.Add(new int2(bx, by), i);
						}
					}
				}

				for (int i = 0; i < n; i++) {
					var br = bucketRange[i];
					var boxI = primaryResults[i];
					for (int bx = br.x; bx <= br.y; bx++) {
						for (int by = br.z; by <= br.w; by++) {
							if (buckets.TryGetFirstValue(new int2(bx, by), out int j, out var it)) {
								do {
									if (j > i && BoxesTouchOrOverlap(boxI, primaryResults[j])) {
										Union(parent, i, j);
									}
								} while (buckets.TryGetNextValue(out j, ref it));
							}
						}
					}
				}
				buckets.Dispose();
				bucketRange.Dispose();

				// ---- group clusters (see class doc for the O(n) per-cluster-start tradeoff) and
				// run the incremental maximal-rectangle sweep on each ----
				var visited = new NativeArray<bool>(n, Allocator.Temp);
				var memberBuffer = new NativeArray<int>(n, Allocator.Temp);

				for (int i = 0; i < n; i++) {
					if (visited[i]) continue;
					int root = Find(parent, i);

					int memberCount = 0;
					for (int j = 0; j < n; j++) {
						if (!visited[j] && Find(parent, j) == root) {
							memberBuffer[memberCount++] = j;
							visited[j] = true;
						}
					}

					RunClusterSweep(primaryResults, memberBuffer, memberCount, MaxBoundSize, region.Key, ref Results);
				}

				visited.Dispose();
				memberBuffer.Dispose();
				parent.Dispose();
				primaryResults.Dispose();

				Results.EndForEachIndex();
			}

			/// <summary>
			/// Standard greedy rectangle grow from an anchor (extend right, then extend down
			/// while the whole row beneath stays free), fused with claim - identical logic to
			/// the managed version's ExtractAndClaimGreedyRect, just returning a box instead of
			/// a box + tile List (tiles are always exactly the box's extent for a greedy-grown
			/// block, so there's nothing the tile list would have told a caller that the box
			/// doesn't already say).
			/// </summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static PrimaryRect ExtractAndClaimGreedyRect(
				int anchorX, int anchorY, NativeArray<byte> unassignedGrid, int2 maxBoundSize,
				int strideW, int anchorIdx, int regionMinX, int regionMinY) {

				int blockWidth = 1;
				int probeIdx = anchorIdx + 1;
				while (blockWidth < maxBoundSize.x && unassignedGrid[probeIdx] == 1) {
					blockWidth++;
					probeIdx++;
				}

				int blockHeight = 1;
				int rowBase = anchorIdx + strideW;
				bool canExpandHeight = true;
				while (blockHeight < maxBoundSize.y && canExpandHeight) {
					int idx = rowBase;
					for (int dx = 0; dx < blockWidth; dx++) {
						if (unassignedGrid[idx] == 0) {
							canExpandHeight = false;
							break;
						}
						idx++;
					}
					if (canExpandHeight) {
						blockHeight++;
						rowBase += strideW;
					}
				}

				int rowStart = anchorIdx;
				for (int dy = 0; dy < blockHeight; dy++) {
					int idx = rowStart;
					for (int dx = 0; dx < blockWidth; dx++) {
						unassignedGrid[idx] = 0;
						idx++;
					}
					rowStart += strideW;
				}

				return new PrimaryRect {
					MinX = regionMinX + anchorX,
					MinY = regionMinY + anchorY,
					MaxX = regionMinX + anchorX + blockWidth - 1,
					MaxY = regionMinY + anchorY + blockHeight - 1
				};
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static bool BoxesTouchOrOverlap(PrimaryRect a, PrimaryRect b) {
				return a.MinX - 1 <= b.MaxX && a.MaxX + 1 >= b.MinX &&
					   a.MinY - 1 <= b.MaxY && a.MaxY + 1 >= b.MinY;
			}

			/// <summary>Proper floor division (handles negative coordinates), matching what the
			/// managed version got from Mathf.FloorToInt((float)x / bucketSize) - done in
			/// integer arithmetic here to sidestep any float-precision edge cases in Burst.</summary>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static int FloorDiv(int a, int b) {
				int q = a / b;
				int r = a % b;
				if (r != 0 && ((r < 0) != (b < 0))) q--;
				return q;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static int Find(NativeArray<int> parent, int x) {
				while (parent[x] != x) {
					parent[x] = parent[parent[x]];
					x = parent[x];
				}
				return x;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void Union(NativeArray<int> parent, int a, int b) {
				int ra = Find(parent, a);
				int rb = Find(parent, b);
				if (ra != rb) parent[ra] = rb;
			}

			/// <summary>
			/// Rasterizes one cluster's member PrimaryRects into a local dense grid sized to the
			/// cluster's own bbox (rasterizing from boxes instead of tile lists - same "tiles ==
			/// box extent" equivalence as stage 1), then runs the exact same incremental
			/// maximal-rectangle histogram sweep as the Pure slicer / the managed version's
			/// RunPostProcessingOnCluster, emitting subdivided SlicedRectOut results directly.
			/// </summary>
			private static void RunClusterSweep(
				NativeList<PrimaryRect> primaryResults, NativeArray<int> memberBuffer, int memberCount,
				int2 maxBoundSize, int2 regionKey, ref NativeStream.Writer results) {

				int minX = int.MaxValue, maxX = int.MinValue;
				int minY = int.MaxValue, maxY = int.MinValue;
				int remaining = 0;
				for (int k = 0; k < memberCount; k++) {
					var r = primaryResults[memberBuffer[k]];
					if (r.MinX < minX) minX = r.MinX;
					if (r.MaxX > maxX) maxX = r.MaxX;
					if (r.MinY < minY) minY = r.MinY;
					if (r.MaxY > maxY) maxY = r.MaxY;
					remaining += (r.MaxX - r.MinX + 1) * (r.MaxY - r.MinY + 1);
				}

				int width = maxX - minX + 1;
				int height = maxY - minY + 1;
				int gridDimension = width * height;

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

				for (int k = 0; k < memberCount; k++) {
					var r = primaryResults[memberBuffer[k]];
					for (int y = r.MinY; y <= r.MaxY; y++) {
						int rowOffset = (y - minY) * width;
						for (int x = r.MinX; x <= r.MaxX; x++) {
							filled[rowOffset + (x - minX)] = 1;
						}
					}
				}

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
					EmitSubdivided(mergedMin, mergedMax, maxBoundSize, regionKey, ref results);

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
							out int ba2, out int bx2, out int by2, out int bw2, out int bh2);
						rowBestArea[yy] = ba2; rowBestX[yy] = bx2; rowBestY[yy] = by2; rowBestW[yy] = bw2; rowBestH[yy] = bh2;
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