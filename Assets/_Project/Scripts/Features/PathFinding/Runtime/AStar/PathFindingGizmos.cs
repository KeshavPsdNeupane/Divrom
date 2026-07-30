using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Data;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;
using ZLinq;

/// <summary>
/// Handles Inspector-driven execution, benchmarking, and gizmo visualization 
/// for the AStarMacro pathfinding system.
/// </summary>
public class PathFindingGizmos : MonoBehaviour {
	[Message(
		"Gizmo visualization supports both Packed and GlobalStream grid data container formats.\n\n" +
		"• GlobalStream (Preferred for Baking & Storage): Superior memory efficiency, reduced " +
		"disk footprint, and minimal allocation overhead during hydration.\n" +
		"• Packed: Supported for live visualization and debug inspection.",
		MessageSeverity.Info
	)]
	[Header("Graph Data")]
	[SerializeField] private GridDataContainerBase graphDataContainer;

	[Header("Graph Reference")]
	[SerializeField] private PathfindingGraphManager graphManager;

	[Header("Macro Request Settings")]
	[SerializeField] private CostCalculationType costCalculationType = CostCalculationType.Manhattan;
	[SerializeField, Range(1f, 1.5f)] private float greedyNess = 1f;
	[SerializeField] private Transform startTransform;
	[SerializeField] private Transform endTransform;
	[SerializeField] private MovementCapability capability = MovementCapability.Ground;

	[Header("Recording Settings")]
	[SerializeField, Tooltip(
		"Universal recording toggle — gates whether a recorder is passed into pathfinding at all " +
		"(macro now, micro later once that recorder exists). When off, FindPath runs with recorder=null " +
		"for max performance, so Animated/ManualScrub have no per-step open/closed set data to draw. " +
		"FinalPathOnly is unaffected by this toggle — it draws straight from the FindPath result, not the recorder."
	)]
	private bool enableRecording = true;

	[Header("Start/End Gizmo Styling")]
	[SerializeField] private Color startSphereColor = Color.green;
	[SerializeField] private Color endSphereColor = Color.red;
	[SerializeField, Min(0.01f)] private float startEndSphereRadius = 0.25f;


	[Header("Macro Visualization Controls")]
	[SerializeField, Tooltip("Master switch for the macro overlay gizmo. Off = nothing drawn in OnDrawGizmos, including start/end spheres.")]
	private bool showMacroOverlay = true;
	[SerializeField]
	private MacroPathfinderGizmos.VisualizationMode macroMode =
		MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep;
	[SerializeField, Min(0.001f), Tooltip("Seconds each step is held before advancing. Drives Animated playback in BOTH Play Mode (coroutine) and Edit Mode (EditorApplication.update).")]
	private float secondsPerStep = 0.05f;
	[SerializeField, Tooltip("Drives ManualScrub, and doubles as the live cursor during Animated playback. One index past the last step reveals the final path.")]
	private int manualStepIndex = 0;

	[Header("Macro Gizmo Styling")]
	[SerializeField] private Color currentColor = Color.yellow;
	[SerializeField] private Color openSetColor = new(0.15f, 0.9f, 0.3f, 0.5f);
	[SerializeField] private Color closedSetColor = new(0.8f, 0.2f, 0.2f, 0.25f);
	[SerializeField] private Color finalPathColor = Color.green;
	[SerializeField] private bool showStepLabel = true;

	[Header("Benchmark Settings")]
	[SerializeField, Range(0.1f, 0.5f), Tooltip("How much to increment the greediness factor (w) per benchmark step (from 1.0 up to 1.5).")]
	private float benchmarkStep = 0.1f;
	[SerializeField, Min(1), Tooltip("How many times to run the pathfinder per configuration to calculate the average time/ticks.")]
	private int benchmarkIterations = 50;
	[SerializeField, Range(1, 100), Tooltip("How many random start/end pairs to generate for the benchmark suite.")]
	private int benchmarkRandomPairs = 5;

	[Header("Random Benchmark Settings")]
	[SerializeField, Tooltip(
		"Seed for \"Run Random Benchmark Suite\". Drives the RNG that rolls one greediness value per " +
		"(pair, cost type) combo, bounded by the fixed RandomBenchmarkMinGreediness/MaxGreediness range " +
		"(class constants, not exposed here). Same seed + same graph/pairs = same run."
	)]
	private int randomBenchmarkSeed = 12345;

	// Fixed bounds for the greediness roll in RunRandomBenchmarkSuite. Intentionally NOT [SerializeField] —
	// this mirrors AStarMacro's actual supported greediness range (see _greedyNess's [Range] above), not
	// something that should be tuned per-scene from the Inspector.
	private const float RandomBenchmarkMinGreediness = 1.0f;
	private const float RandomBenchmarkMaxGreediness = 1.5f;

	public MacroPathfinderGizmos MacroGizmos { get; } = new();

	private AStarMacro _pathfinder;
	private Coroutine _macroStepCoroutine;

#if UNITY_EDITOR
	private bool _editModeAnimating;
	private double _editModeLastTickTime;
#endif

	private void Awake() {
		EnsurePathfinder();
	}

	private void OnDisable() {
		StopMacroAnimation();
	}

	private void EnsurePathfinder() {
		if (this.graphDataContainer == null) {
			Debug.LogWarning("Graph Data Container is not assigned. Cannot initialize Macro Pathfinder.");
			return;
		}

		var neighborDict = this.graphDataContainer.MacroAdjacencyList;

		MacroGraphManager macroGraphManager = new(this.graphDataContainer.MacroGridNodeDict, neighborDict);
		MicroGraphManager microGraphManager = new(this.graphDataContainer.MicroGridNodeDict);

		this.graphManager = new(macroGraphManager, microGraphManager);
		this._pathfinder = new AStarMacro(this.graphManager, this.greedyNess, this.costCalculationType);
	}

	#region Context Menu Actions

	/// <summary>
	/// Runs pathfinding and records every step so you can scrub it — this itself needs no Play Mode,
	/// and now animated playback afterward doesn't either.
	/// </summary>
	[ContextMenu("Run Macro Pathfinding")]
	public void ExecuteMacroPathfindingEditMode() {
		EnsurePathfinder();
		if (this._pathfinder == null) return;

		if (this.startTransform == null || this.endTransform == null) {
			Debug.LogWarning("Start/End transform not assigned. Cannot run Macro Pathfinding.");
			return;
		}

		if (!this.enableRecording) {
			Debug.LogWarning(
				"Recording is disabled (_enableRecording = false). Pathfinding will still run and log its " +
				"result below. Animated/ManualScrub will have nothing to draw since they need per-step " +
				"open/closed set data from the recorder, but FinalPathOnly still works — it draws straight " +
				"from the FindPath result now, not the recorder."
			);
		}

		// clear the shit so it wont keep drawing the old path when you run it again
		MacroGizmos.Recorder.Clear();
		MacroGizmos.FinalPath = null;

		SyncMacroDrawerSettings();
		this.manualStepIndex = 0;

		// the float-> int logic is in the Vec2Int constructor, so we can just pass the world position directly
		var startVec = new Vec2Int(this.startTransform.position);
		var endVec = new Vec2Int(this.endTransform.position);

		System.Diagnostics.Stopwatch stopwatch = new();

		stopwatch.Start();
		MacroPathFindingResult result = this._pathfinder.FindPath(
			startVec,
			endVec,
			this.capability,
			this.enableRecording ? MacroGizmos.Recorder : null
		);
		stopwatch.Stop();

		// Decoupled from the recorder on purpose: the gizmo's FinalPath is set straight from the
		// result, so FinalPathOnly (and the end-of-animation reveal) works even with recording off.
		// result.Path is EMPTY_PATH (not null) on failure, so this correctly clears any stale line too.
		MacroGizmos.FinalPath = result.Path;

		// Quick standard read on what kind of request this was before diving into the result numbers.
		Debug.Log(DescribePointRelationship(startVec, endVec));

		ResultLog(
			result,
			"Macro",
			startVec.ToString(),
			endVec.ToString(),
			this.capability.ToString(),
			stopwatch.ElapsedMilliseconds,
			stopwatch.ElapsedTicks
		);
		TryStartMacroAnimation();

#if UNITY_EDITOR
		UnityEditor.SceneView.RepaintAll();
#endif
	}

	/// <summary>
	/// Replays the Animated Step-By-Step visualization from the steps already recorded by
	/// "Run Macro Pathfinding" — does NOT touch the pathfinder or recompute anything.
	/// Works in both Play Mode and Edit Mode.
	/// </summary>
	[ContextMenu("Play Macro Animation (No Rerun)")]
	public void PlayRecordedMacroAnimation() {
		if (!this.enableRecording) {
			Debug.LogWarning(
				"Recording is disabled (_enableRecording = false), so no steps were captured on the last " +
				"run. Enable \"Enable Recording\" and run \"Run Macro Pathfinding\" again before playing it back."
			);
			return;
		}

		if (MacroGizmos.Recorder.Steps == null || MacroGizmos.Recorder.Steps.Count == 0) {
			Debug.LogWarning("No recorded Macro Pathfinding steps yet. Run \"Run Macro Pathfinding\" first.");
			return;
		}

		this.macroMode = MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep;
		SyncMacroDrawerSettings();
		this.manualStepIndex = 0;

		TryStartMacroAnimation();
	}

	/// <summary>
	/// Sweeps every cost-calculation formula across the greediness range for several random
	/// start/end point pairs. Each pair gets its own labeled log block with a per-cost-type table,
	/// a standard path profile (direction/distance), and a per-pair verdict on which cost type
	/// performed best. Once every pair has run, a final overall verdict is logged across the
	/// whole suite.
	///
	/// Optimized vs the naive version:
	/// - One reused Stopwatch (.Restart() per iteration) instead of Stopwatch.StartNew() per
	///   iteration, which was allocating pairs*costTypes*steps*iterations Stopwatch objects —
	///   garbage that then has to get collected mid-benchmark, adding noise to what you're measuring.
	/// - One GC.Collect() per pair instead of one per (costType, greediness) config. That was
	///   3 * greedinessSteps blocking collects per pair; collapsing to one per pair keeps the
	///   heap clean going into the sweep without paying that cost repeatedly for no real benefit.
	/// - avgMs derived from the same ticks total instead of separately accumulating
	///   sw.Elapsed.TotalMilliseconds (skips a TimeSpan alloc+read every iteration).
	/// </summary>
	[ContextMenu("Run Benchmark Suite")]
	public void RunBenchmarkSuite() {
		EnsurePathfinder();
		if (this.graphManager == null) {
			Debug.LogError("Missing graph manager for benchmark.");
			return;
		}

		var testPairs = this.graphManager.GiveRandomTestPoints(this.benchmarkRandomPairs);
		int pairCount = testPairs != null ? testPairs.AsValueEnumerable().Count() : 0;
		if (testPairs == null || pairCount == 0) {
			Debug.LogError("No valid random test points could be generated from the micro graph.");
			return;
		}

		CostCalculationType[] costTypes = (CostCalculationType[])Enum.GetValues(typeof(CostCalculationType));

		// Calculate total steps required (from 1.0 to 1.5 inclusive) to avoid float precision issues in the loop
		int greedinessSteps = Mathf.RoundToInt((1.5f - 1.0f) / this.benchmarkStep);

		// Reused across every timed call in the whole suite instead of allocated per iteration.
		System.Diagnostics.Stopwatch sw = new();

		// Suite-wide bucket, one per cost type, tallied across every pair — feeds the final verdict.
		var overallSummaries = new Dictionary<CostCalculationType, CostTypeSummary>();
		foreach (var costType in costTypes) overallSummaries[costType] = new CostTypeSummary();

		// Suite-wide bucket, one per (greediness step, cost type), tallied across every pair —
		// feeds the final per-greediness ranking table ("A>B>C" per w value).
		var perGreedinessSummaries = new Dictionary<int, Dictionary<CostCalculationType, CostTypeSummary>>();
		for (int stepIdx = 0; stepIdx <= greedinessSteps; stepIdx++) {
			var stepBucket = new Dictionary<CostCalculationType, CostTypeSummary>();
			foreach (var costType in costTypes) stepBucket[costType] = new CostTypeSummary();
			perGreedinessSummaries[stepIdx] = stepBucket;
		}

		int pairIndex = 0;
		foreach (var (startVec, endVec) in testPairs) {
			pairIndex++;

			// One collect per pair, not per config — see class-doc comment above for why.
			GC.Collect();

			StringBuilder sb = new();
			sb.AppendLine($"<b><color=#00FFFF>=== MACRO BENCHMARK [{pairIndex}/{pairCount}] | Start: {startVec} -> End: {endVec} ===</color></b>");
			sb.AppendLine($"<b>Settings:</b> {this.benchmarkIterations} iterations per step | Greediness Step: {this.benchmarkStep}");
			sb.AppendLine(DescribePointRelationship(startVec, endVec));

			// Per-pair bucket, one per cost type, so we can compare them once this pair's sweep is done.
			var pairSummaries = new Dictionary<CostCalculationType, CostTypeSummary>();

			foreach (var costType in costTypes) {
				sb.AppendLine($"\n<b><color=#FFFF00>Metric: {costType}</color></b>");
				sb.AppendLine("<b>Greediness | Path Len | Avg Expansions | Avg Evaluations | Avg Ticks | Avg MS</b>");

				var costSummary = new CostTypeSummary();

				for (int stepIdx = 0; stepIdx <= greedinessSteps; stepIdx++) {
					float currentGreediness = Mathf.Clamp(1.0f + (stepIdx * this.benchmarkStep), 1.0f, 1.5f); // Security clamp

					AStarMacro benchFinder = new(this.graphManager, currentGreediness, costType);

					// WARMUP RUN — JIT + cache warm, recorder off for max speed.
					benchFinder.FindPath(startVec, endVec, this.capability, null);

					long totalTicks = 0;
					long totalExpansions = 0;
					long totalEvaluations = 0;
					int pathLength = 0;

					for (int i = 0; i < this.benchmarkIterations; i++) {
						sw.Restart();
						var result = benchFinder.FindPath(startVec, endVec, this.capability, null);
						sw.Stop();

						totalTicks += sw.ElapsedTicks;
						totalExpansions += result.TotalNodeExpansions;
						totalEvaluations += result.TotalNodeEvaluations;
						pathLength = result.Path != null ? result.Path.Count : 0;
					}

					float avgTicks = (float)totalTicks / this.benchmarkIterations;
					float avgMs = (float)(totalTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency) / this.benchmarkIterations;
					float avgExp = (float)totalExpansions / this.benchmarkIterations;
					float avgEval = (float)totalEvaluations / this.benchmarkIterations;

					sb.AppendLine($"{currentGreediness,-10:F2} | {pathLength,-8} | {avgExp,-14:F1} | {avgEval,-15:F1} | {avgTicks,-9:F0} | {avgMs,-6:F4}");

					costSummary.Accumulate(avgMs, avgTicks, avgExp, avgEval, pathLength);
					perGreedinessSummaries[stepIdx][costType].Accumulate(avgMs, avgTicks, avgExp, avgEval, pathLength);
				}

				pairSummaries[costType] = costSummary;
				overallSummaries[costType].Add(costSummary);
			}

			sb.AppendLine(BuildComparisonSummary(pairSummaries, "Pair Summary"));
			sb.AppendLine("------------------------------------------------------------------------------------------------------------------");
			Debug.Log(sb.ToString());
		}

		// Every pair has run — log the final rollup across the whole suite.
		Debug.Log(BuildOverallSuiteSummary(overallSummaries, perGreedinessSummaries, greedinessSteps, this.benchmarkStep, pairCount));
	}

	/// <summary>
	/// Random counterpart to "Run Benchmark Suite". Instead of sweeping every greediness step for
	/// every cost type, each (pair, cost type) combo gets exactly ONE greediness value, rolled
	/// randomly within the fixed [<see cref="RandomBenchmarkMinGreediness"/>, <see cref="RandomBenchmarkMaxGreediness"/>]
	/// range and drawn from a <see cref="System.Random"/> seeded with <see cref="randomBenchmarkSeed"/> —
	/// same seed against the same graph/pairs reproduces the exact same run.
	///
	/// Results are timed and aggregated the same way as the sweep suite (per-pair summary, then a
	/// suite-wide rollup across every pair), logged to the console as usual, and then the full
	/// report is stripped of Unity's rich-text console markup (&lt;b&gt;, &lt;color=...&gt;, etc.)
	/// and saved as a plain-text file under persistentDataPath/PathfindingBenchmarks/.
	/// </summary>
	[ContextMenu("Run Random Benchmark Suite")]
	public void RunRandomBenchmarkSuite() {
		EnsurePathfinder();
		if (this.graphManager == null) {
			Debug.LogError("Missing graph manager for benchmark.");
			return;
		}

		var testPairs = this.graphManager.GiveRandomTestPoints(this.benchmarkRandomPairs);
		int pairCount = testPairs != null ? testPairs.AsValueEnumerable().Count() : 0;
		if (testPairs == null || pairCount == 0) {
			Debug.LogError("No valid random test points could be generated from the micro graph.");
			return;
		}

		CostCalculationType[] costTypes = (CostCalculationType[])Enum.GetValues(typeof(CostCalculationType));
		System.Random rng = new(this.randomBenchmarkSeed);
		System.Diagnostics.Stopwatch sw = new();

		// Suite-wide bucket, one per cost type, tallied across every pair — feeds the final verdict.
		var overallSummaries = new Dictionary<CostCalculationType, CostTypeSummary>();
		foreach (var costType in costTypes) overallSummaries[costType] = new CostTypeSummary();

		// Full report kept alongside the console logs so it can be sanitized and written to disk
		// once the whole suite has finished.
		StringBuilder fullReport = new();
		fullReport.AppendLine($"<b><color=#00FFFF>=== RANDOM MACRO BENCHMARK SUITE | Seed: {this.randomBenchmarkSeed} ===</color></b>");
		fullReport.AppendLine(
			$"<b>Settings:</b> {pairCount} pairs | {this.benchmarkIterations} iterations per config | " +
			$"Greediness Range: [{RandomBenchmarkMinGreediness:F2}, {RandomBenchmarkMaxGreediness:F2}]"
		);

		int pairIndex = 0;
		foreach (var (startVec, endVec) in testPairs) {
			pairIndex++;

			// One collect per pair, same reasoning as the sweep suite — keeps GC noise out of the timing.
			GC.Collect();

			StringBuilder sb = new();
			sb.AppendLine($"<b><color=#00FFFF>=== RANDOM BENCHMARK [{pairIndex}/{pairCount}] | Start: {startVec} -> End: {endVec} ===</color></b>");
			sb.AppendLine(DescribePointRelationship(startVec, endVec));
			sb.AppendLine("<b>Cost Type    | Greediness | Path Len | Avg Expansions | Avg Evaluations | Avg Ticks | Avg MS</b>");

			// Per-pair bucket, one per cost type, so we can compare them once this pair's rolls are done.
			var pairSummaries = new Dictionary<CostCalculationType, CostTypeSummary>();

			foreach (var costType in costTypes) {
				// Single random roll for this (pair, cost type) combo, bounded by the fixed range above.
				float randomGreediness = (float)(RandomBenchmarkMinGreediness +
					(rng.NextDouble() * (RandomBenchmarkMaxGreediness - RandomBenchmarkMinGreediness)));

				AStarMacro benchFinder = new(this.graphManager, randomGreediness, costType);

				// WARMUP RUN — JIT + cache warm, recorder off for max speed.
				benchFinder.FindPath(startVec, endVec, this.capability, null);

				long totalTicks = 0;
				long totalExpansions = 0;
				long totalEvaluations = 0;
				int pathLength = 0;

				for (int i = 0; i < this.benchmarkIterations; i++) {
					sw.Restart();
					var result = benchFinder.FindPath(startVec, endVec, this.capability, null);
					sw.Stop();

					totalTicks += sw.ElapsedTicks;
					totalExpansions += result.TotalNodeExpansions;
					totalEvaluations += result.TotalNodeEvaluations;
					pathLength = result.Path != null ? result.Path.Count : 0;
				}

				float avgTicks = (float)totalTicks / this.benchmarkIterations;
				float avgMs = (float)(totalTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency) / this.benchmarkIterations;
				float avgExp = (float)totalExpansions / this.benchmarkIterations;
				float avgEval = (float)totalEvaluations / this.benchmarkIterations;

				sb.AppendLine($"{costType,-12} | {randomGreediness,-10:F4} | {pathLength,-8} | {avgExp,-14:F1} | {avgEval,-15:F1} | {avgTicks,-9:F0} | {avgMs,-6:F4}");

				var costSummary = new CostTypeSummary();
				costSummary.Accumulate(avgMs, avgTicks, avgExp, avgEval, pathLength);

				pairSummaries[costType] = costSummary;
				overallSummaries[costType].Add(costSummary);
			}

			sb.AppendLine(BuildComparisonSummary(pairSummaries, "Pair Summary"));
			sb.AppendLine("------------------------------------------------------------------------------------------------------------------");

			string pairBlock = sb.ToString();
			Debug.Log(pairBlock);
			fullReport.AppendLine(pairBlock);
		}

		// Every pair has run — log and record the final rollup across the whole suite.
		string overallBlock = BuildRandomOverallSuiteSummary(overallSummaries, pairCount, this.randomBenchmarkSeed);
		Debug.Log(overallBlock);
		fullReport.AppendLine(overallBlock);

		// Strip Unity's console-only rich-text markup before this hits disk, then save it.
		string sanitized = SanitizeLogBloat(fullReport.ToString());
		string writtenPath = WriteBenchmarkReportToFile(sanitized, "RandomMacroBenchmark", this.randomBenchmarkSeed);

		if (writtenPath != null) {
			Debug.Log($"Random Benchmark Suite report saved to: {writtenPath}");
		}
	}
	#endregion

	#region Path Analysis & Comparative Summaries

	/// <summary>
	/// Standard, no-frills read on what kind of request a start/end pair represents:
	/// how far apart they are (straight-line and Manhattan) and which compass direction
	/// end sits relative to start. Nothing fancier than that — just enough context to
	/// sanity-check the numbers that follow.
	///
	/// Assumes Vec2Int exposes public int X / Y fields. If your Vec2Int uses lowercase
	/// x/y instead, swap the casing below.
	/// </summary>
	private static string DescribePointRelationship(Vec2Int start, Vec2Int end) {
		int dx = end.X - start.X;
		int dy = end.Y - start.Y;
		float straightLineDist = Mathf.Sqrt(dx * dx + dy * dy);
		int manhattanDist = Mathf.Abs(dx) + Mathf.Abs(dy);
		string direction = DescribeDirection(dx, dy);

		return $"<b>Path Profile:</b> Δ=({dx}, {dy}) | Direction: {direction} | " +
			$"Straight-Line Dist: {straightLineDist:F2} | Manhattan Dist: {manhattanDist}";
	}

	/// <summary>Simple 8-way compass reading off a delta — N/S/E/W plus the diagonals.</summary>
	private static string DescribeDirection(int dx, int dy) {
		if (dx == 0 && dy == 0) return "Same Point";

		string vertical = dy > 0 ? "North" : dy < 0 ? "South" : "";
		string horizontal = dx > 0 ? "East" : dx < 0 ? "West" : "";

		if (vertical.Length > 0 && horizontal.Length > 0) return $"{vertical}-{horizontal}";
		return vertical.Length > 0 ? vertical : horizontal;
	}

	/// <summary>
	/// Running accumulator for one CostCalculationType's stats, either across the
	/// greediness sweep for a single pair, or across every pair in the suite.
	/// </summary>
	private class CostTypeSummary {
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

	/// <summary>
	/// Builds a "who wins" readout for a set of cost-type summaries: fastest, fewest
	/// expansions, and shortest path each get their own line, and whichever cost type
	/// takes the most of those three categories is called out as the overall pick
	/// (fastest breaks ties, since raw speed is usually what you notice first).
	/// Shared by both the per-pair summary and the final overall suite summary.
	/// </summary>
	private static string BuildComparisonSummary(Dictionary<CostCalculationType, CostTypeSummary> summaries, string title) {
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
	private static string BuildOverallSuiteSummary(
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

	/// <summary>
	/// Final rollup for the random suite — same per-cost-type totals table and comparison verdict as
	/// the sweep suite's overall summary, minus the per-greediness ranking table (greediness here is
	/// randomly rolled per combo rather than stepped, so there's no clean per-w bucket to rank within).
	/// </summary>
	private static string BuildRandomOverallSuiteSummary(
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
	private static string BuildGreedinessRankingTable(
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
	private static string SanitizeLogBloat(string richText) {
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
	private static string WriteBenchmarkReportToFile(string sanitizedContent, string filePrefix, int seed = -1) {
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

	#region Animation Coroutines & Ticks

	private void TryStartMacroAnimation() {
		StopMacroAnimation();

		if (this.macroMode != MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep) return;

		if (Application.isPlaying) {
			this._macroStepCoroutine = StartCoroutine(AnimateMacroStepsRoutine());
		}
#if UNITY_EDITOR
		else {
			StartEditModeAnimation();
		}
#endif
	}

	private void StopMacroAnimation() {
		if (this._macroStepCoroutine != null) {
			StopCoroutine(this._macroStepCoroutine);
			this._macroStepCoroutine = null;
		}
#if UNITY_EDITOR
		StopEditModeAnimation();
#endif
	}

	private IEnumerator AnimateMacroStepsRoutine() {
		int maxStep = MacroGizmos.MaxStepIndex;
		while (this.manualStepIndex < maxStep) {
			yield return new WaitForSeconds(this.secondsPerStep);
			this.manualStepIndex++;
		}
	}

#if UNITY_EDITOR
	private void StartEditModeAnimation() {
		this._editModeAnimating = true;
		this._editModeLastTickTime = UnityEditor.EditorApplication.timeSinceStartup;
		UnityEditor.EditorApplication.update -= EditModeAnimationTick; // avoid double-subscribe
		UnityEditor.EditorApplication.update += EditModeAnimationTick;
	}

	private void StopEditModeAnimation() {
		if (!this._editModeAnimating) return;
		this._editModeAnimating = false;
		UnityEditor.EditorApplication.update -= EditModeAnimationTick;
	}

	private void EditModeAnimationTick() {
		// Object destroyed, or something else stopped us — unhook defensively either way.
		if (this == null || !this._editModeAnimating) {
			UnityEditor.EditorApplication.update -= EditModeAnimationTick;
			return;
		}

		if (Application.isPlaying || this.macroMode != MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep) {
			StopEditModeAnimation();
			return;
		}

		double now = UnityEditor.EditorApplication.timeSinceStartup;
		if (now - this._editModeLastTickTime < this.secondsPerStep) return;
		this._editModeLastTickTime = now;

		int maxStep = MacroGizmos.MaxStepIndex;
		if (this.manualStepIndex >= maxStep) {
			StopEditModeAnimation();
			return;
		}

		this.manualStepIndex++;
		UnityEditor.SceneView.RepaintAll();
	}
#endif
	#endregion

	#region Visuals & Utilities

	private void SyncMacroDrawerSettings() {
		MacroGizmos.Mode = this.macroMode;
		MacroGizmos.CurrentColor = this.currentColor;
		MacroGizmos.OpenSetColor = this.openSetColor;
		MacroGizmos.ClosedSetColor = this.closedSetColor;
		MacroGizmos.FinalPathColor = this.finalPathColor;
		MacroGizmos.ShowStepLabel = this.showStepLabel;
	}

	private void OnDrawGizmos() {
		if (!this.showMacroOverlay) return;

		SyncMacroDrawerSettings();
		MacroGizmos.DrawGizmos(this.manualStepIndex);

		if (this.startTransform != null) {
			Gizmos.color = this.startSphereColor;
			Gizmos.DrawSphere(this.startTransform.position, this.startEndSphereRadius);
		}
		if (this.endTransform != null) {
			Gizmos.color = this.endSphereColor;
			Gizmos.DrawSphere(this.endTransform.position, this.startEndSphereRadius);
		}
	}

	public static void ResultLog<Tlist>(
		PathFindingResult<Tlist> tresult, string pathfindingType,
		string startPos, string endPos, string capability, long elapsedMilliseconds, long elapsedTicks) {

		string pathString = tresult.Path != null ? string.Join(" -> ", tresult.Path) : "No path found";

		Debug.Log($"{pathfindingType} Pathfinding with {tresult.CostCalculationType} Greedyness: {tresult.Greediness} cost calculation completed on time({elapsedMilliseconds}ms/{elapsedTicks} ticks).\n" +
		$"Total Nodes: {tresult.TotalNodes}, Total Expansions: {tresult.TotalNodeExpansions}, " +
		$"Total Node Evaluations: {tresult.TotalNodeEvaluations}, Path found: {(tresult.Path != null ? tresult.Path.Count : 0)} nodes.\n" +
		$"Start: {startPos}, End: {endPos}, Capability: {capability}\nPath: {pathString}");
	}

	#endregion
}