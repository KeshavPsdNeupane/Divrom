// PathFindingGizmos.cs
using System.Collections;
using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.EntityIdentity;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;
using ZLinq;


public class PathFindingGizmos : MonoBehaviour {
	[Header("Graph Data")]
	[SerializeField] private PathFindingGridDataContainer _graphDataContainer;
	[Header("Graph Reference")]
	[SerializeField] private PathfindingGraphManager _graphManager;

	[Header("Macro Request Settings")]
	[SerializeField] private Transform _startTransform;
	[SerializeField] private Transform _endTransform;
	[SerializeField] private MovementCapability _capability;

	[Header("Recording Settings")]
	[SerializeField, Tooltip(
		"Universal recording toggle — gates whether a recorder is passed into pathfinding at all " +
		"(macro now, micro later once that recorder exists). When off, FindPath runs with recorder=null " +
		"for max performance, so Animated/ManualScrub have no per-step open/closed set data to draw. " +
		"FinalPathOnly is unaffected by this toggle — it draws straight from the FindPath result, not the recorder."
	)]
	private bool _enableRecording = true;

	[Header("Macro Visualization Controls")]
	[SerializeField, Tooltip("Master switch for the macro overlay gizmo. Off = nothing drawn in OnDrawGizmos, including start/end spheres.")]
	private bool _showMacroOverlay = true;
	[SerializeField]
	private MacroPathfinderGizmos.VisualizationMode _macroMode =
		MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep;
	[SerializeField, Tooltip("Seconds each step is held before advancing. Drives Animated playback in BOTH Play Mode (coroutine) and Edit Mode (EditorApplication.update).")]
	private float _secondsPerStep = 0.05f;
	[SerializeField, Tooltip("Drives ManualScrub, and doubles as the live cursor during Animated playback. One index past the last step reveals the final path.")]
	private int _manualStepIndex = 0;

	[Header("Macro Gizmo Styling")]
	[SerializeField] private Color _currentColor = Color.yellow;
	[SerializeField] private Color _openSetColor = new(0.15f, 0.9f, 0.3f, 0.5f);
	[SerializeField] private Color _closedSetColor = new(0.8f, 0.2f, 0.2f, 0.25f);
	[SerializeField] private Color _finalPathColor = Color.green;
	[SerializeField] private bool _showStepLabel = true;

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
		// Covers component disable, destroy, and script recompiles — don't leak the update hook.
		StopMacroAnimation();
	}

	private void EnsurePathfinder() {
		if (this._graphDataContainer == null) {
			Debug.LogWarning("Graph Data Container is not assigned. Cannot initialize Macro Pathfinder.");
			return;
		}

		var neighborDict = this._graphDataContainer.GridData.MacroAdjacencyListWrapper.AsValueEnumerable()
		.Aggregate(new SerializableDictionary<BoundingBox, List<MacroConnectionData>>(), (dict, kvp) => {
			dict[kvp.Key] = kvp.Value.Connections;
			return dict;
		});

		MacroGraphManager macroGraphManager = new(this._graphDataContainer.GridData.MacroGridNodeDict, neighborDict);
		MicroGraphManager microGraphManager = new(this._graphDataContainer.GridData.MicroGridNodeDict);

		this._graphManager = new(macroGraphManager, microGraphManager);
		this._pathfinder = new AStarMacro(this._graphManager);
	}

	/// <summary>
	/// Runs pathfinding and records every step so you can scrub it — this itself needs no Play Mode,
	/// and now animated playback afterward doesn't either.
	/// </summary>
	[ContextMenu("Run Macro Pathfinding")]
	public void ExecuteMacroPathfindingEditMode() {
		EnsurePathfinder();
		if (this._pathfinder == null) return;

		if (this._startTransform == null || this._endTransform == null) {
			Debug.LogWarning("Start/End transform not assigned. Cannot run Macro Pathfinding.");
			return;
		}

		if (!this._enableRecording) {
			Debug.LogWarning(
				"Recording is disabled (_enableRecording = false). Pathfinding will still run and log its " +
				"result below. Animated/ManualScrub will have nothing to draw since they need per-step " +
				"open/closed set data from the recorder, but FinalPathOnly still works — it draws straight " +
				"from the FindPath result now, not the recorder."
			);
		}

		//clear the shit so it wont keep drawing the old path when you run it again
		MacroGizmos.Recorder.Clear();
		MacroGizmos.FinalPath = null;

		SyncMacroDrawerSettings();
		this._manualStepIndex = 0;

		// the float-> int logic is in the Vec2Int constructor, so we can just pass the world position directly
		var startVec = new Vec2Int(this._startTransform.position);
		var endVec = new Vec2Int(this._endTransform.position);

		System.Diagnostics.Stopwatch stopwatch = new();

		stopwatch.Start();
		MacroPathFindingResult result = this._pathfinder.FindPath(
			startVec,
			endVec,
			this._capability,
			this._enableRecording ? MacroGizmos.Recorder : null
		);
		stopwatch.Stop();

		// Decoupled from the recorder on purpose: the gizmo's FinalPath is set straight from the
		// result, so FinalPathOnly (and the end-of-animation reveal) works even with recording off.
		// result.Path is EMPTY_PATH (not null) on failure, so this correctly clears any stale line too.
		MacroGizmos.FinalPath = result.Path;

		ResultLog(
			result,
			"Macro",
			startVec.ToString(),
			endVec.ToString(),
			this._capability.ToString(),
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
		if (!this._enableRecording) {
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

		this._macroMode = MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep;
		SyncMacroDrawerSettings();
		this._manualStepIndex = 0;

		TryStartMacroAnimation();
	}

	private void TryStartMacroAnimation() {
		StopMacroAnimation();

		if (this._macroMode != MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep) return;

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
		while (this._manualStepIndex < maxStep) {
			yield return new WaitForSeconds(this._secondsPerStep);
			this._manualStepIndex++;
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

		if (Application.isPlaying || this._macroMode != MacroPathfinderGizmos.VisualizationMode.AnimatedStepByStep) {
			StopEditModeAnimation();
			return;
		}

		double now = UnityEditor.EditorApplication.timeSinceStartup;
		if (now - this._editModeLastTickTime < this._secondsPerStep) return;
		this._editModeLastTickTime = now;

		int maxStep = MacroGizmos.MaxStepIndex;
		if (this._manualStepIndex >= maxStep) {
			StopEditModeAnimation();
			return;
		}

		this._manualStepIndex++;
		UnityEditor.SceneView.RepaintAll();
	}
#endif

	private void SyncMacroDrawerSettings() {
		MacroGizmos.Mode = this._macroMode;
		MacroGizmos.CurrentColor = this._currentColor;
		MacroGizmos.OpenSetColor = this._openSetColor;
		MacroGizmos.ClosedSetColor = this._closedSetColor;
		MacroGizmos.FinalPathColor = this._finalPathColor;
		MacroGizmos.ShowStepLabel = this._showStepLabel;
	}

	private void OnDrawGizmos() {
		if (!this._showMacroOverlay) return;

		SyncMacroDrawerSettings();
		MacroGizmos.DrawGizmos(this._manualStepIndex);

		if (this._startTransform != null) { Gizmos.color = Color.cyan; Gizmos.DrawSphere(this._startTransform.position, 0.3f); }
		if (this._endTransform != null) { Gizmos.color = Color.magenta; Gizmos.DrawSphere(this._endTransform.position, 0.3f); }
	}




	public static void ResultLog<Tlist>(
		PathFindingResult<Tlist> tresult, string pathfindingType,
		string startPos, string endPos, string capability, long elapsedMilliseconds, long elapsedTicks) {
		string pathString = tresult.Path != null ? string.Join(" -> ", tresult.Path) : "No path found";
		Debug.Log($"{pathfindingType} Pathfinding completed on time({elapsedMilliseconds}ms/{elapsedTicks} ticks)." +
		$" Total Node Searches: {tresult.TotalNodeSearches}, Path found: {tresult.Path.Count} nodes." +
		$" Path: {pathString}. Start: {startPos}, End: {endPos}, Capability: {capability}");
	}
}