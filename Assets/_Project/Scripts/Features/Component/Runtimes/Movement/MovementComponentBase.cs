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
		public MovementComponentSaveData() { }
		public MovementComponentSaveData(Vector3 position) {
			this.Position = new Vec3(position);
		}
	}

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
	/// <summary>
	/// Centralized Locomotion Controller for the Kope Framework.
	/// 
	/// DESIGN RATIONALE:
	/// This component acts as a 'Velocity Mediator,' blending high-level Movement Intent 
	/// (Input/AI) with low-level Physical Forces (Knockbacks/Impulses). 
	/// 
	/// By centralizing these layers, we avoid 'Velocity Fighting' and ensure consistent 
	/// behavior during complex states like Stuns or SuperStuns. The multi-interface 
	/// implementation allows external systems (Combat, Save, Status) to interact with 
	/// the Actor through narrow contracts without requiring a direct dependency on 
	/// the movement logic.
	/// </summary>
	public class MovementComponentBase : InitializableBase,
	IMovementComponent, ISaveable, IStunnable, IKnockbackable {
		[Header("References")]
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
		private Vector3 _lastDirection = new(0f, -1f, 0f);
		private AxisMode _dimension;
		private Vector3 _dimMask;
		private BasicCountDownTimer _stunTimer;
		protected MovementIntent _currentIntent;

		public float Mass => this.rb.mass;
		public Vector3 Direction => this._currentIntent.Direction;
		public Vector3 Position => this.rb.position;
		public AxisMode Dimension => this._dimension;
		public Rigidbody2D Rigidbody => this.rb;

		public bool IsStunned => this._stunTimer != null && this._stunTimer.IsRunning;

		// ILastDirectionProvider implementation.
		public Vector3 LastDirection => this._lastDirection;


		#region Initialization & Stats

		protected override bool OnInit() {
			if (this.ecr == null || this.rb == null) {
				MyLogger.Error($"MovementComponentBase ({gameObject.name}): Missing required references.");
				return false;
			}
			if (this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out CharacterStatsSystem statsSystem)) {
				this._readOnlycharacterStatsSystem = statsSystem;
			}
			this._dimension = this.ecr.ComponentRegistry.Dimension;

			this._stunTimer = new BasicCountDownTimer(0f);
			this._dimMask = (this._dimension == AxisMode.TwoD) ? new Vector3(1, 1, 0) : Vector3.one;
			this._lastDirection = (this._dimension == AxisMode.TwoD) ? new Vector3(0f, -1f, 0f) : Vector3.forward;
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

		protected virtual void SetDefaultMovementSpeed(float speed) => this.defaultMovementSpeed = speed;

		#endregion

		#region Movement Logic

		public virtual void SetMovementIntent(MovementIntent intent) {
			// Ignore new intents while stunned
			if (this._stunTimer.IsRunning) return;

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
			if (this._stunTimer.IsRunning) {
				this._stunTimer.Tick(Time.deltaTime);
			}
		}

		public virtual void ApplyKnockback(Vector3 hitPoint, float duration, float impulse = 1f, bool isPulling = false) {
			// to convert the Vector3 hitPoint to Vector2 for 2D calculations, ignoring the z-axis, at once than casting
			// it multiple times later on.
			// for 3d just remove the Vector2 conversion and use the hitPoint directly.
			Vector2 vector2 = hitPoint;
			Vector3 direction = isPulling ? (this.rb.position - vector2) : (vector2 - this.rb.position);
			Vector3 forceVector = direction.normalized * impulse;
			ForceInstance instance = new(forceVector, duration, HandleForceExpiration);
			this._activeForces.Add(instance);
			RecalculateNetForce();
		}

		private void TickForceInstances(float deltaTime) {
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
			for (int i = 0; i < this._activeForces.Count; i++) {
				this._cachedNetForce += this._activeForces[i].Force;
			}
			// Apply dimension mask to the cached force
			_cachedNetForce = Vector3.Scale(_cachedNetForce, this._dimMask);
		}

		/// <summary>
		/// Applies the integrated Physics and Intent layers.
		/// Should be called from FixedUpdate via a Physics Controller or directly if this component manages its own ticks.
		/// </summary>
		public virtual void ApplyPhysics(float stateSpeedMultiplier = 1f) {
			// 1. Physics Layer (Always active, pre-masked in RecalculateNetForce)
			Vector3 physicsPart = this._cachedNetForce;

			// 2. Intent Layer (Evaluates to zero if stunned or stopping)
			bool canMove = !this.IsStunned && this._currentIntent.IntentType != MovementIntentType.Stop;

			Vector3 intentPart = canMove
				? this._currentIntent.Direction * (this.defaultMovementSpeed * stateSpeedMultiplier)
				: Vector3.zero;

			// 3. Blend Logic (Consistent weighting for smooth feel)
			Vector3 targetVelocity = intentPart + physicsPart;

			// 4. The Final Cast for Rigidbody2D
			// (0.3 * current) allows for momentum conservation
			// (0.7 * target) ensures snappy response to input or new forces
			this.rb.linearVelocity = (this.rb.linearVelocity * 0.3f) + (Vector2)(targetVelocity * 0.7f);
		}

		public void StopMovementIntent() {
			this._currentIntent = new MovementIntent(
				Vector3.zero,
				MovementIntentType.Stop,
				MovementIntentPriority.Normal
			);
		}

		public virtual Vector3 GetLookingAtDirection() {
			return this._dimension == AxisMode.TwoD
				? new Vector3(this._lastDirection.x, this._lastDirection.y, 0f)
				: this.rb.transform.forward;
		}

		#endregion

		#region Persistence & Stun

		public ISaveData GetSaveData() => new MovementComponentSaveData(this.rb.position);

		public void LoadFromSaveData(ISaveData data) {
			if (data is MovementComponentSaveData saveData) {
				this.rb.position = saveData.Position.ToVector3();
			}
		}

		public void Stun(float duration) {
			this._stunTimer.Reset(duration);
			this._stunTimer.Start();
		}
		/// <summary>
		/// A more severe form of stun that not only applies the stun duration but also immediately clears 
		/// all active forces and prevents new movement intents from taking effect during the stun. 
		/// This can be used for things like heavy crowd control effects, environmental hazards, or 
		/// powerful enemy attacks that should feel more impactful than a regular stun.
		/// </summary>
		public void SuperStun(float duration) {
			//1. Clear all active forces immediately
			for (int i = 0; i < this._activeForces.Count; i++) {
				this._activeForces[i].Dispose();
			}
			this._activeForces.Clear();
			RecalculateNetForce();
			// "force" a stop intent to ensure no movement from 
			// input during the stun duration, even if the intent is still technically active.
			StopMovementIntent();
			// 2. Apply the stun duration (can be longer than a regular stun to reflect the harsher effect)
			this._stunTimer.Reset(duration * 1.5f);
			this._stunTimer.Start();
		}
		public void ForceCancellStun() => this._stunTimer?.Stop();

		void IMovementComponent.SetMovementIntent(MovementIntent intent) {
			throw new NotImplementedException();
		}

		Vector3 IMovementComponent.GetLookingAtDirection() {
			throw new NotImplementedException();
		}

		void IMovementComponent.StopMovementIntent() {
			throw new NotImplementedException();
		}

		#endregion
	}
}