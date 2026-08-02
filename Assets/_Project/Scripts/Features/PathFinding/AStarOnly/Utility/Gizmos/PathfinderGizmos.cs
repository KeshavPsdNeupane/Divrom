using System.Collections.Generic;
using Kope.Feature.PathFindingNew.PathFinding;
using Kope.Feature.PathFindingNew.Utility;
using UnityEngine;


public enum PathfindingVisualizationMode {
	AnimatedStepByStep,
	ManualScrub,
	FinalPathOnly
}


/// <summary>
/// Gizmo overlay for the single-tier <see cref="AStar"/> search (Kope.Feature.PathFindingNew).
/// Structural sibling of MacroPathfinderGizmos/MicroPathfinderGizmos from the old hierarchical
/// suite — same shape (Recorder + FinalPath + step cursor + playback) — just pointed at the new
/// system's <see cref="PathfindingRecorder"/> instead of Macro/MicroPathfindingRecorder. There's
/// only one tier here, so there's only one gizmo class instead of a Macro/Micro pair.
/// <para>
/// Deliberately reuses <see cref="PathfindingVisualizationMode"/> and
/// <see cref="GizmoPlaybackController"/> as-is from the old suite (both live in the global
/// namespace already) — neither has any dependency on macro/micro/old/new specifics, so there's
/// no reason to fork them. Keep both scripts in the same assembly/project so this type can see
/// them.
/// </para>
/// Fully self-contained and dumb by design — it only ever draws whatever Recorder/FinalPath it's
/// handed; it never runs a search itself.
/// </summary>
public class PathfinderGizmos {
	public PathfindingRecorder Recorder { get; } = new();

	// Visual Settings
	public PathfindingVisualizationMode Mode { get; set; } = PathfindingVisualizationMode.AnimatedStepByStep;
	public Color CurrentColor { get; set; } = Color.cyan;
	public Color OpenSetColor { get; set; } = new(0.15f, 0.9f, 0.3f, 0.5f);
	public Color ClosedSetColor { get; set; } = new(0.8f, 0.2f, 0.2f, 0.25f);
	public Color FinalPathColor { get; set; } = Color.blue;
	public bool ShowStepLabel { get; set; } = true;
	public float SecondsPerStep { get; set; } = 0.05f;

	/// <summary>
	/// The finished path drawn by FinalPathOnly mode and by the end-of-animation reveal
	/// (Animated/ManualScrub scrubbed to MaxStepIndex). Set this straight from a
	/// PathFindingResult (e.g. <c>gizmos.FinalPath = result.Path;</c>) — deliberately decoupled
	/// from Recorder so the line still draws with recording disabled, since result.Path exists
	/// regardless of whether a recorder was passed into FindPath.
	/// </summary>
	public List<Vec2Int> FinalPath { get; set; }

	/// <summary>
	/// One index past the last recorded step. Both drawing AND playback treat this value as
	/// "search finished" — scrubbing (or animating) to exactly this index reveals the final path.
	/// 0 if nothing has been recorded yet.
	/// </summary>
	public int MaxStepIndex => Recorder.Steps?.Count ?? 0;

	/// <summary>Current scrub/animation position. Drives both ManualScrub (set directly) and
	/// AnimatedStepByStep (advanced by Play()). Clamped to [0, MaxStepIndex] on draw.</summary>
	public int CurrentStepIndex { get; set; }

	public bool IsPlaying => this._playback.IsPlaying;

	private readonly GizmoPlaybackController _playback;

	/// <param name="coroutineHost">MonoBehaviour used to host Play Mode animation playback.</param>
	public PathfinderGizmos(MonoBehaviour coroutineHost) {
		this._playback = new GizmoPlaybackController(coroutineHost);
	}

	/// <summary>Stops any running animation and clears the recorder/final path/step cursor —
	/// call before running a fresh search.</summary>
	public void ResetForNewSearch() {
		Stop();
		Recorder.Clear();
		FinalPath = null;
		CurrentStepIndex = 0;
	}

	/// <summary>Starts Animated playback from CurrentStepIndex up to MaxStepIndex. No-op outside AnimatedStepByStep mode.</summary>
	public void Play() {
		Stop();
		if (Mode != PathfindingVisualizationMode.AnimatedStepByStep) return;
		this._playback.Play(SecondsPerStep, () => CurrentStepIndex, i => CurrentStepIndex = i, () => MaxStepIndex);
	}

	public void Stop() => this._playback.Stop();

	/// <summary>
	/// Renders the search Gizmos. Call inside MonoBehaviour.OnDrawGizmos().
	/// AnimatedStepByStep and ManualScrub render identically off CurrentStepIndex.
	/// FinalPathOnly ignores the step cursor entirely and always shows the finished path.
	/// </summary>
	public void DrawGizmos() {
		if (Recorder == null) return;

		if (Mode == PathfindingVisualizationMode.FinalPathOnly) {
			DrawFinalPath();
			return;
		}

		if (Recorder.Steps == null || Recorder.Steps.Count == 0) return;

		int clampedStep = Mathf.Clamp(CurrentStepIndex, 0, MaxStepIndex);

		// One index past the last recorded step = search finished, reveal the final path.
		if (clampedStep >= Recorder.Steps.Count) {
			DrawFinalPath();
			return;
		}

		var step = Recorder.Steps[clampedStep];

		// Closed Set (Evaluated) — dimmest, flattest layer
		foreach (var cell in step.ClosedSet) {
			GridGizmoUtil.DrawFlatRect(cell, 0f, ClosedSetColor, new Color(0f, 0f, 0f, 0.25f));
		}

		// Open Set (Discovered / frontier)
		foreach (var cell in step.OpenSet) {
			GridGizmoUtil.DrawFlatRect(cell, 0.01f, OpenSetColor, Color.black);
		}

		// Currently Evaluating Node — top layer, always visible above the rest
		GridGizmoUtil.DrawFlatRect(step.Current, 0.02f, CurrentColor, Color.white);

#if UNITY_EDITOR
		if (ShowStepLabel) {
			UnityEditor.Handles.color = Color.white;
			UnityEditor.Handles.Label(
				GridGizmoUtil.LayeredCenter(step.Current, 0.02f) + Vector3.up,
				$"Step {clampedStep + 1}/{Recorder.Steps.Count}\nOpen: {CountOf(step.OpenSet)}   Closed: {CountOf(step.ClosedSet)}"
			);
		}
#endif
	}

	private void DrawFinalPath() {
		if (FinalPath == null) return;

		Vector3 lastCenter = default;
		for (int i = 0; i < FinalPath.Count; i++) {
			var cell = FinalPath[i];
			GridGizmoUtil.DrawFlatRect(cell, 0.03f, FinalPathColor, Color.black);

			Vector3 center = GridGizmoUtil.LayeredCenter(cell, 0.03f);
			if (i > 0) {
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(lastCenter, center);
			}
			lastCenter = center;
		}
	}

	private static int CountOf(IEnumerable<Vec2Int> set) {
		int n = 0;
		foreach (var _ in set) n++;
		return n;
	}
}