using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Drives step-by-step gizmo animation playback in both Play Mode (via a hosted coroutine) and
/// Edit Mode (via EditorApplication.update). Extracted out of PathFindingGizmos because that
/// Play/Edit-mode split was identical for macro and micro — instead of pasting it twice, each
/// visualizer (<see cref="MacroPathfinderGizmos"/>, <see cref="MicroPathfinderGizmos"/>) owns its
/// own instance, so their playback is fully independent: starting/stopping one never touches the
/// other's step cursor or coroutine.
/// </summary>
internal class GizmoPlaybackController {
	private readonly MonoBehaviour _coroutineHost;
	private Coroutine _playModeCoroutine;

#if UNITY_EDITOR
	private bool _editModeAnimating;
	private double _editModeLastTickTime;
	private float _editSecondsPerStep;
	private Func<int> _editGetStep;
	private Action<int> _editSetStep;
	private Func<int> _editGetMax;
#endif

	/// <param name="coroutineHost">MonoBehaviour used to host the Play Mode coroutine. Only needed
	/// for Play Mode playback — Edit Mode playback doesn't touch it.</param>
	public GizmoPlaybackController(MonoBehaviour coroutineHost) {
		this._coroutineHost = coroutineHost;
	}

	/// <summary>True while either a Play Mode coroutine or an Edit Mode tick loop is actively running.</summary>
	public bool IsPlaying {
		get {
#if UNITY_EDITOR
			return this._playModeCoroutine != null || this._editModeAnimating;
#else
            return this._playModeCoroutine != null;
#endif
		}
	}

	/// <summary>
	/// Starts advancing the caller's step index (via <paramref name="getStepIndex"/>/<paramref name="setStepIndex"/>)
	/// from its current value up to <paramref name="getMaxStepIndex"/>, one step every <paramref name="secondsPerStep"/>.
	/// Works in both Play Mode and Edit Mode.
	/// </summary>
	public void Play(float secondsPerStep, Func<int> getStepIndex, Action<int> setStepIndex, Func<int> getMaxStepIndex) {
		Stop();

		if (this._coroutineHost != null && Application.isPlaying) {
			this._playModeCoroutine = this._coroutineHost.StartCoroutine(
				AnimateRoutine(secondsPerStep, getStepIndex, setStepIndex, getMaxStepIndex));
		}
#if UNITY_EDITOR
		else {
			StartEditModeAnimation(secondsPerStep, getStepIndex, setStepIndex, getMaxStepIndex);
		}
#endif
	}

	public void Stop() {
		if (this._playModeCoroutine != null && this._coroutineHost != null) {
			this._coroutineHost.StopCoroutine(this._playModeCoroutine);
		}
		this._playModeCoroutine = null;
#if UNITY_EDITOR
		StopEditModeAnimation();
#endif
	}

	private IEnumerator AnimateRoutine(float secondsPerStep, Func<int> getStepIndex, Action<int> setStepIndex, Func<int> getMaxStepIndex) {
		int maxStep = getMaxStepIndex();
		while (getStepIndex() < maxStep) {
			yield return new WaitForSeconds(secondsPerStep);
			setStepIndex(getStepIndex() + 1);
		}
	}

#if UNITY_EDITOR
	private void StartEditModeAnimation(float secondsPerStep, Func<int> getStepIndex, Action<int> setStepIndex, Func<int> getMaxStepIndex) {
		this._editSecondsPerStep = secondsPerStep;
		this._editGetStep = getStepIndex;
		this._editSetStep = setStepIndex;
		this._editGetMax = getMaxStepIndex;

		this._editModeAnimating = true;
		this._editModeLastTickTime = UnityEditor.EditorApplication.timeSinceStartup;
		UnityEditor.EditorApplication.update -= EditModeTick; // avoid double-subscribe
		UnityEditor.EditorApplication.update += EditModeTick;
	}

	private void StopEditModeAnimation() {
		if (!this._editModeAnimating) return;
		this._editModeAnimating = false;
		UnityEditor.EditorApplication.update -= EditModeTick;
	}

	private void EditModeTick() {
		// Host destroyed, or something else stopped us — unhook defensively either way.
		if (this._coroutineHost == null || !this._editModeAnimating) {
			UnityEditor.EditorApplication.update -= EditModeTick;
			return;
		}

		if (Application.isPlaying) {
			StopEditModeAnimation();
			return;
		}

		double now = UnityEditor.EditorApplication.timeSinceStartup;
		if (now - this._editModeLastTickTime < this._editSecondsPerStep) return;
		this._editModeLastTickTime = now;

		int maxStep = this._editGetMax();
		if (this._editGetStep() >= maxStep) {
			StopEditModeAnimation();
			return;
		}

		this._editSetStep(this._editGetStep() + 1);
		UnityEditor.SceneView.RepaintAll();
	}
#endif
}