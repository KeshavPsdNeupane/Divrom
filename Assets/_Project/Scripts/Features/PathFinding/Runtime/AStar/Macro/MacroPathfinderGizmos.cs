using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;
using UnityEngine;

public class MacroPathfinderGizmos {
	public enum VisualizationMode {
		FinalPathOnly,
		AnimatedStepByStep,
		ManualScrub
	}

	public MacroPathfindingRecorder Recorder { get; } = new();
	// Visual Settings
	public VisualizationMode Mode { get; set; } = VisualizationMode.AnimatedStepByStep;
	public Color CurrentColor { get; set; } = Color.yellow;
	public Color OpenSetColor { get; set; } = new Color(0.15f, 0.9f, 0.3f, 0.5f);
	public Color ClosedSetColor { get; set; } = new Color(0.8f, 0.2f, 0.2f, 0.25f);
	public Color FinalPathColor { get; set; } = Color.green;
	public bool ShowStepLabel { get; set; } = true;

	/// <summary>
	/// The finished path drawn by FinalPathOnly mode and by the end-of-animation reveal
	/// (Animated/ManualScrub scrubbed to MaxStepIndex). Set this straight from a
	/// PathFindingResult (e.g. <c>MacroGizmos.FinalPath = result.Path;</c>) — deliberately
	/// decoupled from Recorder.FinalPath so the line still draws with recording disabled,
	/// since result.Path exists regardless of whether a recorder was passed into FindPath.
	/// </summary>
	public List<BoundingBox> FinalPath { get; set; }

	/// <summary>
	/// One index past the last recorded step. Both drawing AND playback treat this value as
	/// "search finished" — scrubbing (or animating) to exactly this index reveals the final path.
	/// 0 if nothing has been recorded yet.
	/// </summary>
	public int MaxStepIndex => Recorder.Steps?.Count ?? 0;

	/// <summary>
	/// Renders the macro search Gizmos. Call inside MonoBehaviour.OnDrawGizmos().
	/// AnimatedStepByStep and ManualScrub now render identically — pass whatever step index
	/// you've got (a coroutine-driven one, or one dragged in the Inspector) and this draws it.
	/// FinalPathOnly ignores stepIndex entirely and always shows the finished path.
	/// </summary>
	public void DrawGizmos(int stepIndex) {
		if (Recorder == null) return;

		if (Mode == VisualizationMode.FinalPathOnly) {
			DrawFinalPath();
			return;
		}

		if (Recorder.Steps == null || Recorder.Steps.Count == 0) return;

		int clampedStep = Mathf.Clamp(stepIndex, 0, MaxStepIndex);

		// One index past the last recorded step = search finished, reveal the final path.
		if (clampedStep >= Recorder.Steps.Count) {
			DrawFinalPath();
			return;
		}

		var step = Recorder.Steps[clampedStep];

		// Closed Set (Evaluated) — dimmest, flattest layer
		foreach (var box in step.ClosedSet) {
			GridGizmoUtil.DrawFlatRect(box, 0f, ClosedSetColor, new Color(0f, 0f, 0f, 0.25f));
		}

		// Open Set (Discovered / frontier)
		foreach (var box in step.OpenSet) {
			GridGizmoUtil.DrawFlatRect(box, 0.01f, OpenSetColor, Color.black);
		}

		// Currently Evaluating Node — top layer, always visible above the rest
		GridGizmoUtil.DrawFlatRect(step.Current, 0.02f, CurrentColor, Color.white);

#if UNITY_EDITOR
		if (ShowStepLabel) {
			UnityEditor.Handles.color = Color.white;
			UnityEditor.Handles.Label(
				GridGizmoUtil.FloatCenter(step.Current) + Vector3.up,
				$"Step {clampedStep + 1}/{Recorder.Steps.Count}\nOpen: {CountOf(step.OpenSet)}   Closed: {CountOf(step.ClosedSet)}"
			);
		}
#endif
	}

	private void DrawFinalPath() {
		if (FinalPath == null) return;

		Vector3 lastCenter = default;
		for (int i = 0; i < FinalPath.Count; i++) {
			var box = FinalPath[i];
			GridGizmoUtil.DrawFlatRect(box, 0.03f, FinalPathColor, Color.black);

			Vector3 center = GridGizmoUtil.FloatCenter(box);
			if (i > 0) {
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(lastCenter, center);
			}
			lastCenter = center;
		}
	}

	private static int CountOf(IEnumerable<BoundingBox> set) {
		int n = 0;
		foreach (var _ in set) n++;
		return n;
	}
}