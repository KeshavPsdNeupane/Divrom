using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Kope.Feature.PathFindingOld.Node;
using Kope.Feature.PathFindingOld.PathFinding;
using UnityEngine;

/// <summary>
/// Handles path analysis, comparative formatting, ranking tables, and file export
/// for the pathfinding benchmark suite.
/// </summary>
public static class PathfindingBenchmarkReporter {
	public const float RandomBenchmarkMinGreediness = 1.0f;
	public const float RandomBenchmarkMaxGreediness = 1.5f;

	#region Path Analysis

	public static string DescribePointRelationship(Vec2Int start, Vec2Int end) {
		int dx = end.X - start.X;
		int dy = end.Y - start.Y;
		float straightLineDist = Mathf.Sqrt(dx * dx + dy * dy);
		int manhattanDist = Mathf.Abs(dx) + Mathf.Abs(dy);
		string direction = DescribeDirection(dx, dy);

		return $"<b>Path Profile:</b> Δ=({dx}, {dy}) | Direction: {direction} | " +
			$"Straight-Line Dist: {straightLineDist:F2} | Manhattan Dist: {manhattanDist}";
	}

	/// <summary>Simple 8-way compass reading off a delta — N/S/E/W plus the diagonals.</summary>
	public static string DescribeDirection(int dx, int dy) {
		if (dx == 0 && dy == 0) return "Same Point";

		string vertical = dy > 0 ? "North" : dy < 0 ? "South" : "";
		string horizontal = dx > 0 ? "East" : dx < 0 ? "West" : "";

		if (vertical.Length > 0 && horizontal.Length > 0) return $"{vertical}-{horizontal}";
		return vertical.Length > 0 ? vertical : horizontal;
	}

	#endregion

	#region Comparative Summaries & Ranking

	/// <summary>Picks the dictionary entry with the lowest value under the given selector.</summary>
	private static KeyValuePair<CostCalculationType, CostTypeSummary> MinBy(
		Dictionary<CostCalculationType, CostTypeSummary> source, Func<CostTypeSummary, float> selector) {

		KeyValuePair<CostCalculationType, CostTypeSummary> best = default;
		float bestVal = float.MaxValue;
		bool first = true;

		foreach (var kvp in source) {
			float val = selector(kvp.Value);
			if (first || val < bestVal) {
				bestVal = val;
				best = kvp;
				first = false;
			}
		}
		return best;
	}

	public static string BuildComparisonSummary(Dictionary<CostCalculationType, CostTypeSummary> summaries, string title) {
		var fastest = MinBy(summaries, s => s.MeanMs);
		var leastExpansions = MinBy(summaries, s => s.MeanExpansions);
		var shortestPath = MinBy(summaries, s => s.MeanPathLength);

		var winCounts = new Dictionary<CostCalculationType, int>();
		foreach (var key in summaries.Keys) winCounts[key] = 0;
		winCounts[fastest.Key]++;
		winCounts[leastExpansions.Key]++;
		winCounts[shortestPath.Key]++;

		CostCalculationType best = fastest.Key;
		int bestWins = -1;
		foreach (var kvp in winCounts) {
			if (kvp.Value > bestWins) {
				bestWins = kvp.Value;
				best = kvp.Key;
			}
		}

		StringBuilder sb = new();
		sb.AppendLine($"\n<b><color=#00FF88>-- {title} --</color></b>");
		sb.AppendLine($"Fastest: {fastest.Key} ({fastest.Value.MeanMs:F4} ms avg)");
		sb.AppendLine($"Fewest Expansions: {leastExpansions.Key} ({leastExpansions.Value.MeanExpansions:F1} avg)");
		sb.AppendLine($"Shortest Path: {shortestPath.Key} ({shortestPath.Value.MeanPathLength:F1} nodes avg)");
		sb.AppendLine($"<b>Best: {best}</b> — won {winCounts[best]}/3 categories");
		return sb.ToString();
	}

	/// <summary>Final rollup after every pair in the suite has run, plus the per-cost-type totals table.</summary>
	public static string BuildOverallSuiteSummary(
		Dictionary<CostCalculationType, CostTypeSummary> overallSummaries,
		Dictionary<int, Dictionary<CostCalculationType, CostTypeSummary>> perGreedinessSummaries,
		int greedinessSteps,
		float benchmarkStep,
		int pairCount) {

		StringBuilder sb = new();
		sb.AppendLine("<b><color=#00FFFF>=== OVERALL BENCHMARK VERDICT ===</color></b>");
		sb.AppendLine($"Across all {pairCount} start/end pairs:");
		sb.AppendLine("<b>Cost Type    | Avg MS   | Avg Expansions | Avg Evaluations | Avg Path Len</b>");
		foreach (var kvp in overallSummaries) {
			var s = kvp.Value;
			sb.AppendLine($"{kvp.Key,-12} | {s.MeanMs,-8:F4} | {s.MeanExpansions,-14:F1} | {s.MeanEvaluations,-15:F1} | {s.MeanPathLength,-6:F1}");
		}
		sb.Append(BuildComparisonSummary(overallSummaries, "Overall Verdict (All Pairs)"));
		sb.Append(BuildGreedinessRankingTable(perGreedinessSummaries, greedinessSteps, benchmarkStep));
		return sb.ToString();
	}

	public static string BuildRandomOverallSuiteSummary(
		Dictionary<CostCalculationType, CostTypeSummary> overallSummaries, int pairCount, int seed) {

		StringBuilder sb = new();
		sb.AppendLine("<b><color=#00FFFF>=== RANDOM BENCHMARK — OVERALL VERDICT ===</color></b>");
		sb.AppendLine(
			$"Seed: {seed} | Pairs: {pairCount} | Greediness Range: " +
			$"[{RandomBenchmarkMinGreediness:F2}, {RandomBenchmarkMaxGreediness:F2}]"
		);
		sb.AppendLine("<b>Cost Type    | Avg MS   | Avg Expansions | Avg Evaluations | Avg Path Len | Samples</b>");
		foreach (var kvp in overallSummaries) {
			var s = kvp.Value;
			sb.AppendLine($"{kvp.Key,-12} | {s.MeanMs,-8:F4} | {s.MeanExpansions,-14:F1} | {s.MeanEvaluations,-15:F1} | {s.MeanPathLength,-6:F1} | {s.StepCount}");
		}
		sb.Append(BuildComparisonSummary(overallSummaries, "Overall Verdict (All Pairs, Random Greediness)"));
		return sb.ToString();
	}

	/// <summary>
	/// One row per greediness (w) value, ranking every cost type fastest-to-slowest (by avg ms,
	/// averaged across all pairs at that w) as an "A > B > C" string — e.g. so you can see at a
	/// glance whether the winner flips as greediness climbs from 1.0 toward 1.5.
	/// </summary>
	public static string BuildGreedinessRankingTable(
		Dictionary<int, Dictionary<CostCalculationType, CostTypeSummary>> perGreedinessSummaries,
		int greedinessSteps,
		float benchmarkStep) {

		StringBuilder sb = new();
		sb.AppendLine("\n<b><color=#FF88FF>-- Algorithm Ranking by Greediness (fastest > slowest, avg ms across all pairs) --</color></b>");
		sb.AppendLine("<b>Greediness | Ranking</b>");

		for (int stepIdx = 0; stepIdx <= greedinessSteps; stepIdx++) {
			float greediness = Mathf.Clamp(1.0f + (stepIdx * benchmarkStep), 1.0f, 1.5f);
			var stepSummaries = perGreedinessSummaries[stepIdx];

			var ranked = new List<KeyValuePair<CostCalculationType, CostTypeSummary>>(stepSummaries);
			ranked.Sort((a, b) => a.Value.MeanMs.CompareTo(b.Value.MeanMs));

			var labels = new List<string>();
			foreach (var kvp in ranked) labels.Add($"{kvp.Key} ({kvp.Value.MeanMs:F4}ms)");
			string rankingStr = string.Join(" > ", labels);

			sb.AppendLine($"{greediness,-10:F2} | {rankingStr}");
		}

		return sb.ToString();
	}

	#endregion

	#region Report Sanitizing & File Export

	/// <summary>
	/// Strips Unity's rich-text console markup (&lt;b&gt;, &lt;/b&gt;, &lt;color=#RRGGBB&gt;,
	/// &lt;/color&gt;, etc.) out of a report so it reads as clean plain text on disk instead of
	/// carrying console-only formatting bloat. The report only ever uses that handful of Unity
	/// rich-text tags, so a generic "strip anything in angle brackets" pass is safe here.
	/// </summary>
	public static string SanitizeLogBloat(string richText) {
		if (string.IsNullOrEmpty(richText)) return richText;

		string plain = Regex.Replace(richText, "<[^>]+>", string.Empty);

		// Tag removal tends to leave stretches of empty lines behind — collapse 3+ in a row down to one blank line.
		plain = Regex.Replace(plain, "(\r?\n){3,}", "\n\n");

		return plain.Trim();
	}

	/// <summary>
	/// Writes an already-sanitized report to disk under
	/// <c>&lt;persistentDataPath&gt;/PathfindingBenchmarks/</c>, named with the given prefix, a
	/// timestamp, and (when non-negative) the seed that produced it. Returns the full path written,
	/// or null if the write failed (e.g. no write permissions on the target platform).
	/// </summary>
	public static string WriteBenchmarkReportToFile(string sanitizedContent, string filePrefix, int seed = -1) {
		try {
			string folder = Path.Combine(Application.persistentDataPath, "PathfindingBenchmarks");
			Directory.CreateDirectory(folder);

			string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			string seedSuffix = seed >= 0 ? $"_seed{seed}" : string.Empty;
			string fileName = $"{filePrefix}_{timestamp}{seedSuffix}.txt";
			string fullPath = Path.Combine(folder, fileName);

			File.WriteAllText(fullPath, sanitizedContent);
			return fullPath;
		} catch (Exception e) {
			Debug.LogError($"Failed to write benchmark report to disk: {e}");
			return null;
		}
	}

	#endregion
}

/// <summary>
/// Tracks aggregate metrics for a specific cost calculation type across multiple benchmark runs.
/// </summary>
public class CostTypeSummary {
	public int StepCount;
	public float SumAvgMs;
	public float SumAvgTicks;
	public float SumAvgExpansions;
	public float SumAvgEvaluations;
	public float SumAvgPathLength;

	public void Accumulate(float avgMs, float avgTicks, float avgExpansions, float avgEvaluations, int pathLength) {
		this.StepCount++;
		this.SumAvgMs += avgMs;
		this.SumAvgTicks += avgTicks;
		this.SumAvgExpansions += avgExpansions;
		this.SumAvgEvaluations += avgEvaluations;
		this.SumAvgPathLength += pathLength;
	}

	/// <summary>Folds another summary's totals into this one — used to roll per-pair sums up into the suite-wide bucket.</summary>
	public void Add(CostTypeSummary other) {
		this.StepCount += other.StepCount;
		this.SumAvgMs += other.SumAvgMs;
		this.SumAvgTicks += other.SumAvgTicks;
		this.SumAvgExpansions += other.SumAvgExpansions;
		this.SumAvgEvaluations += other.SumAvgEvaluations;
		this.SumAvgPathLength += other.SumAvgPathLength;
	}

	public float MeanMs => this.StepCount > 0 ? this.SumAvgMs / this.StepCount : 0f;
	public float MeanExpansions => this.StepCount > 0 ? this.SumAvgExpansions / this.StepCount : 0f;
	public float MeanEvaluations => this.StepCount > 0 ? this.SumAvgEvaluations / this.StepCount : 0f;
	public float MeanPathLength => this.StepCount > 0 ? this.SumAvgPathLength / this.StepCount : 0f;
}