using System;
using System.Collections.Generic;
using UnityEngine;
using Kope.Core;
using Kope.Core.Init;
using Kope.Core.EntityComponentRegistry;
using Kope.Character.Stats;
using Kope.SaveSystem;
using Newtonsoft.Json;
using ThirdParty;

namespace Kope.Component.Movement {

	#region Supporting Data Types

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

	#endregion

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

		// Specialized Handlers
		private MovementIntentHandler _intentHandler;
		private MovementForceHandler _forceHandler;

		// State
		private CharacterStatsSystem _readOnlycharacterStatsSystem;
		private AxisMode _dimension;
		private BasicCountDownTimer _stunTimer;

		// Public API
		public float Mass => this.rb.mass;
		public Vector3 Direction => this._intentHandler.Current.Direction;
		public Vector3 Position => this.rb.position;
		public AxisMode Dimension => this._dimension;
		public Rigidbody2D Rigidbody => this.rb;
		public bool IsStunned => this._stunTimer != null && this._stunTimer.IsRunning;

		#region Initialization & Stats

		protected override bool OnInit() {
			if (this.ecr == null || this.rb == null) return false;

			if (this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out CharacterStatsSystem statsSystem)) {
				this._readOnlycharacterStatsSystem = statsSystem;
			}

			this._dimension = this.ecr.ComponentRegistry.Dimension;
			Vector3 dimMask = (this._dimension == AxisMode.TwoD) ? new Vector3(1, 1, 0) : Vector3.one;
			Vector3 initialFacing = (this._dimension == AxisMode.TwoD) ? Vector3.down : Vector3.forward;

			// Initialize Handlers
			this._intentHandler = new MovementIntentHandler(initialFacing);
			this._forceHandler = new MovementForceHandler(dimMask);

			this._stunTimer = new BasicCountDownTimer(0f);
			return true;
		}

		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable() {
			UnsubscribeFromStats();
			SetMovementIntent(MovementIntent.Default);
		}

		private void SubscribeToStats() {
			if (this._readOnlycharacterStatsSystem.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsSubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
				SetDefaultMovementSpeed(this._readOnlycharacterStatsSystem.CurrentStats[CharacterStatType.AGI].GetValue());
			}
		}

		private void UnsubscribeFromStats() {
			if (this._readOnlycharacterStatsSystem.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsUnsubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
			}
		}

		protected virtual void SetDefaultMovementSpeed(float speed) => this.defaultMovementSpeed = speed;

		#endregion

		#region Movement Logic

		/// <summary>
		/// Sets the current movement intent based on input or AI decisions.
		/// No need to normalize the direction vector on caller side, as the intent 
		/// handler will handle that and also update the last facing direction accordingly.
		/// </summary>
		/// <param name="intent"></param>
		public virtual void SetMovementIntent(MovementIntent intent) {
			this._intentHandler.TrySetIntent(intent, IsStunned);
		}

		protected override void OnUpdate() {
			this._forceHandler.Tick(Time.deltaTime);
			if (this._stunTimer.IsRunning) this._stunTimer.Tick(Time.deltaTime);
		}

		/// <summary>
		/// Applies the integrated Physics and Intent layers.
		/// Should be called from FixedUpdate via a Physics Controller or directly if this component manages its own ticks.
		/// </summary>
		public virtual void ApplyPhysics(float stateSpeedMultiplier = 1f) {
			// 1. Physics Layer (Always active, pre-masked in force handler)
			Vector3 physicsPart = _forceHandler.NetForce;

			// 2. Intent Layer (Evaluates to zero if stunned or stopping)
			bool canMove = !this.IsStunned && this._intentHandler.Current.IntentType != MovementIntentType.Stop;

			Vector3 intentPart = canMove
				? this._intentHandler.Current.Direction * (this.defaultMovementSpeed * stateSpeedMultiplier)
				: Vector3.zero;

			// 3. Blend Logic (Consistent weighting for smooth feel)
			Vector3 targetVelocity = intentPart + physicsPart;

			// 4. The Final Cast for Rigidbody2D
			// (0.3 * current) allows for momentum conservation
			// (0.7 * target) ensures snappy response to input or new forces
			this.rb.linearVelocity = (this.rb.linearVelocity * 0.3f) + (Vector2)(targetVelocity * 0.7f);
		}

		public virtual void ApplyKnockback(Vector3 hitPoint, float duration, float impulse = 1f, bool isPulling = false) {
			Vector2 origin = this.rb.position;
			Vector3 direction = isPulling ? ((Vector3)origin - hitPoint) : (hitPoint - (Vector3)origin);
			this._forceHandler.AddForce(direction.normalized * impulse, duration);
		}

		public virtual Vector3 GetLookingAtDirection() {
			return this._dimension == AxisMode.TwoD
				? new Vector3(this._intentHandler.LastDirection.x, this._intentHandler.LastDirection.y, 0f)
				: this.rb.transform.forward;
		}

		#endregion

		#region Persistence & Stun

		public ISaveData GetSaveData() => new MovementComponentSaveData(this.rb.position);
		public void LoadFromSaveData(ISaveData data) {
			if (data is MovementComponentSaveData s) this.rb.position = s.Position.ToVector3();
		}

		public void Stun(float duration) {
			this._stunTimer.Reset(duration);
			this._stunTimer.Start();
		}

		/// <summary>
		/// A more severe form of stun that not only applies the stun duration but also immediately clears 
		/// all active forces and prevents new movement intents from taking effect during the stun. 
		/// </summary>
		public void SuperStun(float duration) {
			this._forceHandler.ClearAllForces();
			// bypass the intent handler's normal priority checks to ensure the actor is fully incapacitated
			// but after the stun duration, the intent handler will resume normal operation 
			// and accept new intents as usual
			this._intentHandler.ForceIntent(MovementIntent.Default);

			this._stunTimer.Reset(duration * 1.5f);
			this._stunTimer.Start();
		}

		public void ForceCancellStun() => this._stunTimer?.Stop();

		#endregion
	}







	#region  Intent Handling & Force Management
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

	/// <summary>
	/// Encapsulates movement intent state and priority-based filtering logic.
	/// </summary>
	public class MovementIntentHandler {
		private MovementIntent _currentIntent;
		private Vector3 _lastDirection;

		public MovementIntent Current => _currentIntent;
		public Vector3 LastDirection => _lastDirection;

		public MovementIntentHandler(Vector3 initialFacing) {
			this._currentIntent = MovementIntent.Default;
			this._lastDirection = initialFacing;
		}

		public bool TrySetIntent(MovementIntent intent, bool isStunned) {
			if (isStunned) return false;

			if (this._currentIntent.Priority != MovementIntentPriority.UnlockNext) {
				if (intent.Priority < this._currentIntent.Priority) return false;
			}

			if (intent.Direction.sqrMagnitude > MovementComponentBase.MOVEMENT_EPSILON) {
				intent.Direction.Normalize();
				this._lastDirection = intent.Direction;
			} else {
				intent.Direction = Vector3.zero;
			}

			this._currentIntent = intent;
			return true;
		}

		public void ForceIntent(MovementIntent intent) {
			this._currentIntent = intent;
			if (intent.Direction.sqrMagnitude > MovementComponentBase.MOVEMENT_EPSILON) {
				this._lastDirection = intent.Direction.normalized;
			}
		}
	}
}
	#endregion