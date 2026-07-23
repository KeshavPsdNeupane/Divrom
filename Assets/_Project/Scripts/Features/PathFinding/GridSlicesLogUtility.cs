using System.Collections.Generic;
using System.Text;
using Kope.Feature.PathFinding.Interface;
using UnityEngine;

namespace Kope.Feature.PathFinding.Utility {
	public class SliceAnalysisSummarizer {
		// Lightweight value type to aggregate stats per anchor without heap allocations
		private struct RegionStats {
			public int SliceCount;
			public int TotalTiles;
			public float TotalAspectRatio;
			public float SumSquaredTiles; // Used for O(1) single-pass variance calculation
		}

		/// <summary>
		/// Output a complete summary report covering performance timings, region breakdowns, 
		/// aspect ratios, and slice size uniformity metrics at once.
		/// </summary>
		public void MakeSummary(
			IRectangleRegionSlicer slicer,
			Vector2Int maxBoundSize,
			int totalExtractedRegions,
			Dictionary<BoundingBox, (Vector2Int Anchor, List<Vector2Int> Tiles)> slicedRegions,
			System.Diagnostics.Stopwatch stopwatch) {

			if (slicedRegions == null || slicedRegions.Count == 0) {
				Debug.LogWarning("[SliceAnalysisSummarizer] No sliced regions provided for summary.");
				return;
			}

			// Aggregate stats per anchor in a single pass without allocating List<BoundingBox>
			var anchorStats = new Dictionary<Vector2Int, RegionStats>(
				slicedRegions.Count / 2,
				Vector2IntComparer.Instance
			);

			int totalBoxes = slicedRegions.Count;
			int totalSlicedTiles = 0;

			foreach (var kvp in slicedRegions) {
				BoundingBox box = kvp.Key;
				Vector2Int anchor = kvp.Value.Anchor;
				int tileCount = kvp.Value.Tiles != null ? kvp.Value.Tiles.Count : 0;
				float sqTiles = (float)tileCount * tileCount;

				totalSlicedTiles += tileCount;

				if (anchorStats.TryGetValue(anchor, out var stats)) {
					stats.SliceCount++;
					stats.TotalTiles += tileCount;
					stats.TotalAspectRatio += box.AspectRatio;
					stats.SumSquaredTiles += sqTiles;
					anchorStats[anchor] = stats;
				} else {
					anchorStats[anchor] = new RegionStats {
						SliceCount = 1,
						TotalTiles = tileCount,
						TotalAspectRatio = box.AspectRatio,
						SumSquaredTiles = sqTiles
					};
				}
			}

			// Single consolidated StringBuilder block to output all metrics at once
			var summaryBuilder = new StringBuilder(1024);
			summaryBuilder.AppendLine("================================================================================");
			summaryBuilder.AppendLine("[PATHFINDING BAKE SUMMARY]");
			summaryBuilder.AppendLine($"Slicer Algorithm     : {(slicer != null ? slicer.GetType().Name : "None")}");
			summaryBuilder.AppendLine($"Max Bound Constraint : {maxBoundSize}");
			summaryBuilder.AppendLine($"Execution Duration   : {stopwatch.ElapsedMilliseconds} ms ({stopwatch.ElapsedTicks} ticks)");
			summaryBuilder.AppendLine($"Extracted Regions    : {totalExtractedRegions} | Total Slices Created: {totalBoxes}");
			summaryBuilder.AppendLine("--------------------------------------------------------------------------------");
			summaryBuilder.AppendLine("[Anchor Region Breakdown]");

			float sumOfRegionAspectAverages = 0f;
			float sumOfRegionUniformities = 0f;

			foreach (var kvp in anchorStats) {
				Vector2Int anchor = kvp.Key;
				RegionStats stats = kvp.Value;

				float avgAspectRatio = stats.SliceCount > 0 ? stats.TotalAspectRatio / stats.SliceCount : 0f;
				float avgTilesPerSlice = stats.SliceCount > 0 ? (float)stats.TotalTiles / stats.SliceCount : 0f;

				// Calculate Size Uniformity (1.0 = identical slice sizes, 0.0 = high size disparity)
				float regionUniformity = 1f;
				if (stats.SliceCount > 1 && avgTilesPerSlice > 0f) {
					float tileVariance = Mathf.Max(0f, (stats.SumSquaredTiles / stats.SliceCount) - (avgTilesPerSlice * avgTilesPerSlice));
					float tileStdDev = Mathf.Sqrt(tileVariance);
					float coefficientOfVariation = tileStdDev / avgTilesPerSlice;
					regionUniformity = Mathf.Clamp01(1f - coefficientOfVariation);
				}

				sumOfRegionAspectAverages += avgAspectRatio;
				sumOfRegionUniformities += regionUniformity;

				summaryBuilder.Append("  - Anchor ").Append(anchor)
							  .Append(" | Slices: ").Append(stats.SliceCount)
							  .Append(" | Tiles: ").Append(stats.TotalTiles)
							  .Append(" | Avg Aspect Ratio: ").AppendFormat("{0:F5}", avgAspectRatio)
							  .Append(" | Size Uniformity: ").AppendFormat("{0:P1}", regionUniformity)
							  .AppendLine();
			}

			// Calculate region-weighted metrics across active regions
			int activeRegionCount = anchorStats.Count;
			float ultimateAverageAspectRatio = activeRegionCount > 0 ? sumOfRegionAspectAverages / activeRegionCount : 0f;
			float ultimateAverageUniformity = activeRegionCount > 0 ? sumOfRegionUniformities / activeRegionCount : 1f;
			float avgSlicesPerRegion = totalExtractedRegions > 0 ? (float)totalBoxes / totalExtractedRegions : 0f;

			summaryBuilder.AppendLine("--------------------------------------------------------------------------------");
			summaryBuilder.AppendLine("[Global Metrics]");
			summaryBuilder.AppendLine($"  - Ultimate Region-Avg Aspect Ratio : {ultimateAverageAspectRatio:F5} (across {activeRegionCount} active regions)");
			summaryBuilder.AppendLine($"  - Ultimate Region-Avg Uniformity   : {ultimateAverageUniformity:P1} (100% = equal slice sizes)");
			summaryBuilder.AppendLine($"  - Avg Slices Per Region            : {avgSlicesPerRegion:F2}");
			summaryBuilder.AppendLine($"  - Total Sliced Tiles Processed     : {totalSlicedTiles}");
			summaryBuilder.AppendLine("================================================================================");

			Debug.Log(summaryBuilder.ToString());
		}

		// Backward compatibility overload
		public void MakeSummary(Dictionary<BoundingBox, (Vector2Int Anchor, List<Vector2Int> Tiles)> slicedRegions) {
			MakeSummary(null, Vector2Int.zero, 0, slicedRegions, new System.Diagnostics.Stopwatch());
		}
	}
}