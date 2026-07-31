using System;
using System.Collections.Generic;
using System.Text;
using Kope.Core.Attribute;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Algorithms;
using Kope.Feature.PathFinding.Data;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.Algorithms;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;
using UnityEngine.Serialization;
using ZLinq;

/// <summary>
/// Handles Inspector-driven pathfinding requests, benchmarking, and gizmo visualization.
/// Every real search goes through <see cref="HierarchicalHomogeneousSpatialIndexingAStar"/> —
/// this class never builds or drives AStarMacro/AStarMicro itself for a live request (the
/// cost-calculator benchmark suite is the one deliberate exception: it profiles AStarMacro
/// directly since it's comparing macro cost-calculation formulas, not running a real request).
/// </summary>
public class PathFinding : MonoBehaviour {
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
	[FormerlySerializedAs("costCalculationType")]
	[SerializeField] private CostCalculationType macroCostCalculationType = CostCalculationType.Manhattan;
	[FormerlySerializedAs("greedyNess")]
	[SerializeField, Range(1f, 1.5f)] private float macroGreedyNess = 1f;

	[Header("Micro Request Settings")]
	[SerializeField] private CostCalculationType microCostCalculationType = CostCalculationType.Octile;
	[SerializeField, Range(1f, 1.5f)] private float microGreedyNess = 1f;

	[Header("Shared Request Settings")]
	[SerializeField] private Transform startTransform;
	[SerializeField] private Transform endTransform;
	[SerializeField] private MovementCapability capability = MovementCapability.Ground;

	[Header("Recording Settings")]
	[SerializeField, Tooltip(
		"Universal recording toggle — gates whether the macro AND micro recorders get passed into " +
		"HierarchicalHomogeneousSpatialIndexingAStar.FindPath at all. When off, the search runs with " +
		"both recorders null for max performance, so Animated/ManualScrub have no per-step open/closed " +
		"set data to draw on either visualizer. FinalPathOnly is unaffected by this toggle for both — " +
		"each draws straight from its own FindPath result, not the recorder."
	)]
	private bool enableRecording = true;

	[Header("Start/End Gizmo Styling")]
	[SerializeField] private Color startSphereColor = Color.green;
	[SerializeField] private Color endSphereColor = Color.red;
	[SerializeField, Min(0.01f)] private float startEndSphereRadius = 0.25f;

	[Header("Macro Visualization Controls")]
	[SerializeField, Tooltip("Master switch for the macro overlay gizmo.")]
	private bool showMacroOverlay = true;
	[FormerlySerializedAs("macroMode")]
	[SerializeField] private PathfindingVisualizationMode macroMode = PathfindingVisualizationMode.AnimatedStepByStep;
	[FormerlySerializedAs("secondsPerStep")]
	[SerializeField, Min(0.001f), Tooltip("Seconds each macro step is held before advancing. Drives Animated playback in BOTH Play Mode and Edit Mode, independently of the micro visualizer's own speed.")]
	private float macroSecondsPerStep = 0.05f;
	[FormerlySerializedAs("manualStepIndex")]
	[SerializeField, Tooltip("Drives ManualScrub, and doubles as the live cursor during Animated playback. One index past the last step reveals the final macro path.")]
	private int macroManualStepIndex = 0;

	[Header("Macro Gizmo Styling")]
	[FormerlySerializedAs("currentColor")]
	[SerializeField] private Color macroCurrentColor = Color.yellow;
	[FormerlySerializedAs("openSetColor")]
	[SerializeField] private Color macroOpenSetColor = new(0.15f, 0.9f, 0.3f, 0.5f);
	[FormerlySerializedAs("closedSetColor")]
	[SerializeField] private Color macroClosedSetColor = new(0.8f, 0.2f, 0.2f, 0.25f);
	[FormerlySerializedAs("finalPathColor")]
	[SerializeField] private Color macroFinalPathColor = Color.green;
	[FormerlySerializedAs("showStepLabel")]
	[SerializeField] private bool macroShowStepLabel = true;

	[Header("Micro Visualization Controls")]
	[SerializeField, Tooltip("Master switch for the micro overlay gizmo.")]
	private bool showMicroOverlay = true;
	[SerializeField] private PathfindingVisualizationMode microMode = PathfindingVisualizationMode.AnimatedStepByStep;
	[SerializeField, Min(0.001f), Tooltip("Seconds each micro step is held before advancing. Independent of the macro visualizer's own speed.")]
	private float microSecondsPerStep = 0.05f;
	[SerializeField, Tooltip("Drives ManualScrub, and doubles as the live cursor during Animated playback. One index past the last step reveals the final micro path.")]
	private int microManualStepIndex = 0;

	[Header("Micro Gizmo Styling")]
	[SerializeField] private Color microCurrentColor = Color.cyan;
	[SerializeField] private Color microOpenSetColor = new(0.15f, 0.9f, 0.3f, 0.5f);
	[SerializeField] private Color microClosedSetColor = new(0.8f, 0.2f, 0.2f, 0.25f);
	[SerializeField] private Color microFinalPathColor = Color.blue;
	[SerializeField] private bool microShowStepLabel = true;

	[Header("Benchmark Settings")]
	[SerializeField, Range(0.1f, 0.5f), Tooltip("How much to increment the greediness factor (w) per benchmark step (from 1.0 up to 1.5).")]
	private float benchmarkStep = 0.1f;
	[SerializeField, Min(1), Tooltip("How many times to run the pathfinder per configuration to calculate the average time/ticks.")]
	private int benchmarkIterations = 50;
	[SerializeField, Range(1, 100), Tooltip("How many random start/end pairs to generate for the benchmark suite.")]
	private int benchmarkRandomPairs = 5;

	// Lazily constructed — warmed explicitly in Awake, but the getters stay null-safe for edit-mode
	// gizmo calls (OnDrawGizmos can fire before Awake runs on a MonoBehaviour in the editor).
	private MacroPathfinderGizmos _macroGizmos;
	public MacroPathfinderGizmos MacroGizmos => this._macroGizmos ??= new MacroPathfinderGizmos(this);

	private MicroPathfinderGizmos _microGizmos;
	public MicroPathfinderGizmos MicroGizmos => this._microGizmos ??= new MicroPathfinderGizmos(this);

	// The one real pathfinding entry point. Gizmos never see this — they only ever get handed a
	// Recorder to fill and a result Path to draw.
	private HierarchicalHomogeneousSpatialIndexingAStar _pathfinder;

	private void Awake() {
		EnsurePathfinder();
		// Warm both visualizers now rather than waiting for first access.
		_ = MacroGizmos;
		_ = MicroGizmos;
	}

	private void OnDisable() {
		this._macroGizmos?.Stop();
		this._microGizmos?.Stop();
	}

	private void EnsurePathfinder() {
		if (this.graphDataContainer == null) {
			Debug.LogWarning("Graph Data Container is not assigned. Cannot initialize Pathfinder.");
			return;
		}

		var neighborDict = this.graphDataContainer.MacroAdjacencyList;

		MacroGraphManager macroGraphManager = new(this.graphDataContainer.MacroGridNodeDict, neighborDict);
		MicroGraphManager microGraphManager = new(this.graphDataContainer.MicroGridNodeDict);

		this.graphManager = new(macroGraphManager, microGraphManager);

		PathFindingConfig macroConfig = new(
			this.macroCostCalculationType, PathFindingConfig.DEFAULT_INITIAL_CAPACITY,
			this.macroGreedyNess, PathFindingConfig.MAX_ITERATIONS_RATIO
			);
		PathFindingConfig microConfig = new(
			this.microCostCalculationType, PathFindingConfig.DEFAULT_INITIAL_CAPACITY,
			this.microGreedyNess, PathFindingConfig.MAX_ITERATIONS_RATIO
			);

		this._pathfinder = new HierarchicalHomogeneousSpatialIndexingAStar(this.graphManager, microConfig, macroConfig);
	}

	#region Context Menu Actions

	[ContextMenu("Run Pathfinding")]
	public void ExecutePathfindingEditMode() {
		EnsurePathfinder();
		if (this._pathfinder == null) return;

		if (this.startTransform == null || this.endTransform == null) {
			Debug.LogWarning("Start/End transform not assigned. Cannot run Pathfinding.");
			return;
		}

		if (!this.enableRecording) {
			Debug.LogWarning(
				"Recording is disabled (_enableRecording = false). Pathfinding will still run and log its " +
				"result below. Animated/ManualScrub will have nothing to draw on either visualizer since " +
				"they need per-step open/closed set data from their recorders, but FinalPathOnly still " +
				"works for both — each draws straight from its own FindPath result, not the recorder."
			);
		}

		// Clear both visualizers so a rerun doesn't keep drawing stale steps/path from the last one.
		MacroGizmos.ResetForNewSearch();
		MicroGizmos.ResetForNewSearch();
		SyncGizmoSettings();

		// the float-> int logic is in the Vec2Int constructor, so we can just pass the world position directly
		var startVec = new Vec2Int(this.startTransform.position);
		var endVec = new Vec2Int(this.endTransform.position);

		System.Diagnostics.Stopwatch stopwatch = new();
		stopwatch.Start();
		PathFindingResultAggregate result = this._pathfinder.FindPath(
			startVec,
			endVec,
			this.capability,
			this.enableRecording ? MacroGizmos.Recorder : null,
			this.enableRecording ? MicroGizmos.Recorder : null
		);
		stopwatch.Stop();

		// Decoupled from the recorders on purpose: each gizmo's FinalPath is set straight from its
		// own result, so FinalPathOnly (and the end-of-animation reveal) works even with recording
		// off. A result's Path is EMPTY_PATH (not null) on failure, so this also clears stale lines.
		if (result.MacroResult != null) MacroGizmos.FinalPath = result.MacroResult.Path;
		if (result.MicroResult != null) MicroGizmos.FinalPath = result.MicroResult.Path;

		// Quick standard read on what kind of request this was before diving into the result numbers.
		Debug.Log(PathfindingBenchmarkReporter.DescribePointRelationship(startVec, endVec));
		// Debug.Log(
		// 	$"Hierarchical Pathfinding finished — ErrorPath: {result.ErrorPath} " +
		// 	$"(Total: {stopwatch.ElapsedMilliseconds}ms / {stopwatch.ElapsedTicks} ticks)."
		// );

		if (result.MacroResult != null) {
			ResultLog(result.MacroResult, "Macro", startVec.ToString(), endVec.ToString(), this.capability.ToString(), stopwatch.ElapsedMilliseconds, stopwatch.ElapsedTicks);
		}
		if (result.MicroResult != null) {
			ResultLog(result.MicroResult, "Micro", startVec.ToString(), endVec.ToString(), this.capability.ToString(), stopwatch.ElapsedMilliseconds, stopwatch.ElapsedTicks);
		}

		TryStartPlayback();

#if UNITY_EDITOR
		UnityEditor.SceneView.RepaintAll();
#endif
	}

	[ContextMenu("Play Macro Animation (No Rerun)")]
	public void PlayRecordedMacroAnimation() {
		if (!this.enableRecording) {
			Debug.LogWarning(
				"Recording is disabled (_enableRecording = false), so no steps were captured on the last " +
				"run. Enable \"Enable Recording\" and run \"Run Pathfinding\" again before playing it back."
			);
			return;
		}

		if (MacroGizmos.Recorder.Steps == null || MacroGizmos.Recorder.Steps.Count == 0) {
			Debug.LogWarning("No recorded Macro Pathfinding steps yet. Run \"Run Pathfinding\" first.");
			return;
		}

		this.macroMode = PathfindingVisualizationMode.AnimatedStepByStep;
		SyncGizmoSettings();
		MacroGizmos.CurrentStepIndex = 0;
		MacroGizmos.Play();
	}

	[ContextMenu("Play Micro Animation (No Rerun)")]
	public void PlayRecordedMicroAnimation() {
		if (!this.enableRecording) {
			Debug.LogWarning(
				"Recording is disabled (_enableRecording = false), so no steps were captured on the last " +
				"run. Enable \"Enable Recording\" and run \"Run Pathfinding\" again before playing it back."
			);
			return;
		}

		if (MicroGizmos.Recorder.Steps == null || MicroGizmos.Recorder.Steps.Count == 0) {
			Debug.LogWarning("No recorded Micro Pathfinding steps yet. Run \"Run Pathfinding\" first.");
			return;
		}

		this.microMode = PathfindingVisualizationMode.AnimatedStepByStep;
		SyncGizmoSettings();
		MicroGizmos.CurrentStepIndex = 0;
		MicroGizmos.Play();
	}

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

			// One collect per pair, not per config — see method-doc comment above for why.
			GC.Collect();

			StringBuilder sb = new();
			sb.AppendLine($"<b><color=#00FFFF>=== MACRO BENCHMARK [{pairIndex}/{pairCount}] | Start: {startVec} -> End: {endVec} ===</color></b>");
			sb.AppendLine($"<b>Settings:</b> {this.benchmarkIterations} iterations per step | Greediness Step: {this.benchmarkStep}");
			sb.AppendLine(PathfindingBenchmarkReporter.DescribePointRelationship(startVec, endVec));

			// Per-pair bucket, one per cost type, so we can compare them once this pair's sweep is done.
			var pairSummaries = new Dictionary<CostCalculationType, CostTypeSummary>();

			foreach (var costType in costTypes) {
				sb.AppendLine($"\n<b><color=#FFFF00>Metric: {costType}</color></b>");
				sb.AppendLine("<b>Greediness | Path Len | Avg Expansions | Avg Evaluations | Avg Ticks | Avg MS</b>");

				var costSummary = new CostTypeSummary();

				for (int stepIdx = 0; stepIdx <= greedinessSteps; stepIdx++) {
					float currentGreediness = Mathf.Clamp(1.0f + (stepIdx * this.benchmarkStep), 1.0f, 1.5f); // Security clamp

					PathFindingConfig config = new(
						costType, PathFindingConfig.DEFAULT_INITIAL_CAPACITY,
						currentGreediness, PathFindingConfig.MAX_ITERATIONS_RATIO
						);
					AStarMacro benchFinder = new(this.graphManager, config);

					// Validate once per benchFinder — start/end are identical across the warmup run and
					// every timed iteration below, so a single PreCheck here covers all of them instead
					// of relying on FindPath to re-validate on every single call.
					if (!benchFinder.PreCheck(startVec, endVec, this.capability, out _)) {
						sb.AppendLine($"{currentGreediness,-10:F2} | PreCheck failed for {startVec} -> {endVec} — skipped.");
						continue;
					}

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

			sb.AppendLine(PathfindingBenchmarkReporter.BuildComparisonSummary(pairSummaries, "Pair Summary"));
			sb.AppendLine("------------------------------------------------------------------------------------------------------------------");
			Debug.Log(sb.ToString());
		}

		// Every pair has run — log the final rollup across the whole suite.
		Debug.Log(PathfindingBenchmarkReporter.BuildOverallSuiteSummary(overallSummaries, perGreedinessSummaries, greedinessSteps, this.benchmarkStep, pairCount));
	}
	#endregion

	#region Playback

	/// <summary>Starts Animated playback on whichever visualizer(s) are set to AnimatedStepByStep —
	/// each Play() call is independently a no-op otherwise, so this is safe to call unconditionally.</summary>
	private void TryStartPlayback() {
		MacroGizmos.Play();
		MicroGizmos.Play();
	}

	#endregion

	#region Visuals & Utilities

	/// <summary>Pushes Inspector-configured styling/mode/speed into both gizmo instances, and keeps
	/// each manual-step-index field mirrored against its gizmo's live cursor (so the Inspector slider
	/// both drives ManualScrub and reflects where Animated playback currently is).</summary>
	private void SyncGizmoSettings() {
		MacroGizmos.Mode = this.macroMode;
		MacroGizmos.CurrentColor = this.macroCurrentColor;
		MacroGizmos.OpenSetColor = this.macroOpenSetColor;
		MacroGizmos.ClosedSetColor = this.macroClosedSetColor;
		MacroGizmos.FinalPathColor = this.macroFinalPathColor;
		MacroGizmos.ShowStepLabel = this.macroShowStepLabel;
		MacroGizmos.SecondsPerStep = this.macroSecondsPerStep;
		if (MacroGizmos.Mode == PathfindingVisualizationMode.ManualScrub) {
			MacroGizmos.CurrentStepIndex = this.macroManualStepIndex;
		} else {
			this.macroManualStepIndex = MacroGizmos.CurrentStepIndex;
		}

		MicroGizmos.Mode = this.microMode;
		MicroGizmos.CurrentColor = this.microCurrentColor;
		MicroGizmos.OpenSetColor = this.microOpenSetColor;
		MicroGizmos.ClosedSetColor = this.microClosedSetColor;
		MicroGizmos.FinalPathColor = this.microFinalPathColor;
		MicroGizmos.ShowStepLabel = this.microShowStepLabel;
		MicroGizmos.SecondsPerStep = this.microSecondsPerStep;
		if (MicroGizmos.Mode == PathfindingVisualizationMode.ManualScrub) {
			MicroGizmos.CurrentStepIndex = this.microManualStepIndex;
		} else {
			this.microManualStepIndex = MicroGizmos.CurrentStepIndex;
		}
	}

	private void OnDrawGizmos() {
		if (!this.showMacroOverlay && !this.showMicroOverlay) return;

		SyncGizmoSettings();

		if (this.showMacroOverlay) {
			MacroGizmos.DrawGizmos();
		}
		if (this.showMicroOverlay) {
			MicroGizmos.DrawGizmos();
		}

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