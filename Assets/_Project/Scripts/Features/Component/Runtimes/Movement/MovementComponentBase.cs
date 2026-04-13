using System;
using System.Collections.Generic;
using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentRegistry;
using Kope.SaveSystem;
using Kope.Core;
using Newtonsoft.Json;
using ThirdParty;

namespace Kope.Component.Movement {

	[Serializable]
	[SaveId("player_movement_data")]
	public class MovementComponentSaveData : ISaveData {
		[JsonProperty("pos")]
		public Vec3 Position { get; set; }
		[JsonProperty("dim")]
		public Dimension Dimension { get; set; }

		public MovementComponentSaveData() {
		}

		public MovementComponentSaveData(Dimension dimension, Vec3 position) {
			this.Dimension = dimension;
			this.Position = position;
		}

		public MovementComponentSaveData(Dimension dimension, Vector3 position) {
			this.Dimension = dimension;
			this.Position = new Vec3(position);
		}
	}

	/// <summary>
	/// Represents a single external force acting on the entity.
	/// Manages its own lifecycle via CountdownTimer.
	/// </summary>
	public class ForceInstance {
		public readonly Vector3 Force;
		public readonly CountdownTimer Timer;
		private Action<ForceInstance> _onExpired;

		public bool IsExpired => this.Timer.IsFinished;

		public ForceInstance(Vector3 force, float duration, Action<ForceInstance> onExpiredCallback) {
			this.Force = force;
			this._onExpired = onExpiredCallback;
			this.Timer = new CountdownTimer(duration);

			// Hook to the timer stop to trigger removal from the component list
			this.Timer.OnTimerStop += HandleTimerStop;
			this.Timer.Start();
		}

		private void HandleTimerStop() {
			this._onExpired?.Invoke(this);
		}

		public void Tick(float deltaTime) => this.Timer.Tick(deltaTime);

		public void Dispose() {
			this.Timer.OnTimerStop -= HandleTimerStop;
			this._onExpired = null;
			this.Timer.Stop();
		}
	}

	public class MovementComponentBase : InitializableBase, IMovementComponent, ISaveable {
		[Header("References")]
		[SerializeField] protected Dimension dimension = Dimension.TwoD;
		[SerializeField] protected Rigidbody2D rb;
		[SerializeField] protected EntityComponentsRegistry ecr;

		[Header("Settings")]
		[SerializeField] protected float defaultMovementSpeed = 2f;
		public const float MOVEMENT_EPSILON = 0.1f;

		// Force Accumulator State
		private readonly List<ForceInstance> _activeForces = new();
		private Vector3 _cachedNetForce = Vector3.zero;

		// Movement Intent State
		private CharacterStatsSystem _readOnlycharacterStatsSystem;
		private Vector3 _lastDirection = Vector3.right;
		protected MovementIntent _currentIntent;

		public float Mass => this.rb.mass;
		public Vector3 Direction => this._currentIntent.Direction;
		public Vector3 Position => this.rb.position;
		public Dimension Dimension => this.dimension;
		public Rigidbody2D Rigidbody => this.rb;

		#region Initialization & Stats

		protected override bool OnInit() {
			if (this.ecr == null || this.rb == null) {
				MyLogger.Error($"MovementComponentBase ({gameObject.name}): Missing required references.");
				return false;
			}

			if (this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out CharacterStatsSystem statsSystem)) {
				this._readOnlycharacterStatsSystem = statsSystem;
			}
			return true;
		}

		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable() {
			UnsubscribeFromStats();
			StopMovementIntent();
		}

		private void SubscribeToStats() {
			if (this._readOnlycharacterStatsSystem?.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsSubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
				SetDefaultMovementSpeed(this._readOnlycharacterStatsSystem.CurrentStats[CharacterStatType.AGI].GetValue());
			}
		}

		private void UnsubscribeFromStats() {
			if (this._readOnlycharacterStatsSystem?.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsUnsubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
			}
		}

		public virtual void SetDefaultMovementSpeed(float speed) => this.defaultMovementSpeed = speed;

		#endregion

		#region Movement Logic

		public virtual void SetMovementIntent(MovementIntent intent) {
			if (intent.Direction.sqrMagnitude > MOVEMENT_EPSILON) {
				intent.Direction.Normalize();
				this._lastDirection = intent.Direction;
			} else {
				intent.Direction = Vector3.zero;
			}
			this._currentIntent = intent;
		}

		protected override void OnUpdate() {
			TickForceInstances(Time.deltaTime);
		}

		public virtual void ApplyKnockback(Vector3 direction, float duration, float impulse = 1f) {
			Vector3 forceVector = direction.normalized * impulse;

			ForceInstance instance = new(forceVector, duration, HandleForceExpiration);
			this._activeForces.Add(instance);

			RecalculateNetForce();
		}

		private void TickForceInstances(float deltaTime) {
			// Iterate backwards to safely handle removals within the same frame
			for (int i = this._activeForces.Count - 1; i >= 0; i--) {
				this._activeForces[i].Tick(deltaTime);
			}
		}

		private void HandleForceExpiration(ForceInstance instance) {
			if (this._activeForces.Remove(instance)) {
				instance.Dispose();
				this.RecalculateNetForce();
			}
		}

		private void RecalculateNetForce() {
			_cachedNetForce = Vector3.zero;
			// Using for-loop instead of foreach to avoid allocation
			for (int i = 0; i < this._activeForces.Count; i++) {
				this._cachedNetForce += this._activeForces[i].Force;
			}

			if (this.dimension == Dimension.TwoD) {
				_cachedNetForce.z = 0;
			}
		}

		public virtual void ApplyPhysics(float stateSpeedMultiplier = 1f) {
			Vector3 moveVelocity = Vector3.zero;

			// 1. Base Input/AI Velocity
			if (this._currentIntent.IntentType != MovementIntentType.Stop) {
				moveVelocity = this.defaultMovementSpeed * stateSpeedMultiplier * this._currentIntent.Direction;
			}

			// 2. Additive Physics Velocity (Net Force)
			Vector3 targetVelocity = moveVelocity + this._cachedNetForce;

			if (this.dimension == Dimension.TwoD) {
				targetVelocity.z = 0;
			}

			// 3. 30/70 Velocity Blending logic
			Vector3 physicsInfluence = this.rb.linearVelocity * 0.3f;
			Vector3 intentInfluence = targetVelocity * 0.7f;
			this.rb.linearVelocity = physicsInfluence + intentInfluence;
		}

		public void StopMovementIntent() {
			// our intent is stopping not the physics forces, so we clear the intent but keep forces intact
			this._currentIntent = new MovementIntent(
				Vector3.zero,
				MovementIntentType.Stop,
				MovementIntentPriority.Normal
			);
		}

		public virtual Vector3 GetLookingAtDirection() {
			return this.dimension == Dimension.TwoD
				? new Vector3(this._lastDirection.x, this._lastDirection.y, 0f)
				: this.rb.transform.forward;
		}

		#endregion

		#region Persistence

		public ISaveData GetSaveData() => new MovementComponentSaveData(this.dimension, new Vec3(this.rb.position));

		public void LoadFromSaveData(ISaveData data) {
			if (data is MovementComponentSaveData saveData) {
				this.rb.position = saveData.Position.ToVector3();
				this.dimension = saveData.Dimension;
			}
		}

		#endregion
	}
}