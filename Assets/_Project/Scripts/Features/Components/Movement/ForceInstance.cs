
using System;
using System.Collections.Generic;
using ThirdParty;
using UnityEngine;

namespace Kope.Component.Movement {
	public class ForceInstance {
		public readonly Vector3 Force;
		public readonly CountdownTimer Timer;
		private Action<ForceInstance> _onExpired;

		public bool IsExpired => this.Timer.IsFinished;

		public ForceInstance(Vector3 force, float duration, Action<ForceInstance> onExpiredCallback) {
			this.Force = force;
			this._onExpired = onExpiredCallback;
			this.Timer = new CountdownTimer(duration);
			this.Timer.OnTimerStop += HandleTimerStop;
			this.Timer.Start();
		}

		private void HandleTimerStop() => this._onExpired?.Invoke(this);
		public void Tick(float deltaTime) => this.Timer.Tick(deltaTime);
		public void Dispose() {
			this.Timer.OnTimerStop -= HandleTimerStop;
			this._onExpired = null;
			this.Timer.Stop();
		}
	}

	// --- Sub Classes ---

	/// <summary>
	/// Handles the accumulation and recalculation of physical forces (Knockbacks, Impulses).
	/// </summary>
	public class MovementForceHandler {
		private readonly List<ForceInstance> _activeForces = new();
		private Vector3 _cachedNetForce = Vector3.zero;
		private readonly Vector3 _dimMask;

		public Vector3 NetForce => _cachedNetForce;

		public MovementForceHandler(Vector3 dimensionMask) {
			this._dimMask = dimensionMask;
		}

		public void AddForce(Vector3 force, float duration) {
			ForceInstance instance = new(force, duration, HandleForceExpiration);
			this._activeForces.Add(instance);
			Recalculate();
		}

		public void Tick(float deltaTime) {
			for (int i = this._activeForces.Count - 1; i >= 0; i--) {
				this._activeForces[i].Tick(deltaTime);
			}
		}

		public void ClearAllForces() {
			foreach (var force in this._activeForces) force.Dispose();
			this._activeForces.Clear();
			Recalculate();
		}

		private void HandleForceExpiration(ForceInstance instance) {
			if (this._activeForces.Remove(instance)) {
				instance.Dispose();
				Recalculate();
			}
		}

		private void Recalculate() {
			this._cachedNetForce = Vector3.zero;
			foreach (var f in this._activeForces) this._cachedNetForce += f.Force;
			this._cachedNetForce = Vector3.Scale(this._cachedNetForce, this._dimMask);
		}
	}
}