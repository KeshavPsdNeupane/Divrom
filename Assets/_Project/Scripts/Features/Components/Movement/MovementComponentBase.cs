using System;
using UnityEngine;
using Kope.Core;
using Kope.Core.LifeTimeManagement;
using Kope.Core.EntityComponentRegistry;
using Kope.Character.Stats;
using Kope.SaveSystem;
using Newtonsoft.Json;
using ThirdParty;
using Kope.Core.Mathfx;

namespace Kope.Component.Movement {

	#region ISavable Supporting datatype

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
	public class MovementComponentBase : InitializableBase, IUpdatable,
	IMovementComponent, ISaveable, IStunnable, IKnockbackable {
		[Header("References")]
		[SerializeField] protected Rigidbody2D rb;
		[SerializeField] protected EntityComponentsRegistry ecr;

		[Header("Settings")]
		[SerializeField] protected float defaultMovementSpeed = 2f;

		private MovementIntentHandler _intentHandler;
		private MovementForceHandler _forceHandler;

		// State
		private CharacterStatsSystemBase _readOnlycharacterStatsSystem;
		private AxisMode _dimension;
		private BasicCountDownTimer _stunTimer;
		private float _currentResponsiveness = Mathfx.DEFAULT_RESPONSIVENESS;


		#region Public Properties
		public float Mass => this.rb.mass;
		public Vector3 Direction => this._intentHandler.Current.Direction;
		public Vector3 Position => this.rb.position;
		public AxisMode Dimension => this._dimension;
		public Rigidbody2D Rigidbody => this.rb;
		public bool IsStunned => this._stunTimer != null && this._stunTimer.IsRunning;

		#endregion

		#region Init and Unity Lifecycle
		protected override bool OnInit() {
			if (this.ecr == null || this.rb == null) return false;

			if (this.ecr.TryFetchReadOnly(this, this.HieararchyPath, out CharacterStatsSystemBase statsSystem)) {
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
		public void OnUpdate() {
			this._forceHandler.Tick(Time.deltaTime);
			if (this._stunTimer.IsRunning) this._stunTimer.Tick(Time.deltaTime);
		}
		#endregion

		#region IMovementComponent Implementation
		/// <summary>
		/// Provides a context-agnostic "looking at" direction for the actor. In 2D, 
		/// this is derived from the last movement intent; in 3D, it defaults to 
		/// the Rigidbody's forward vector.
		/// </summary>
		/// <returns></returns>
		public virtual Vector3 GetLookingAtDirection() {
			return this._dimension == AxisMode.TwoD
				? new Vector3(this._intentHandler.LastDirection.x, this._intentHandler.LastDirection.y, 0f)
				: this.rb.transform.forward;
		}

		/// <summary>
		/// Sets the current movement intent based on input or AI decisions.
		/// No need to normalize the direction vector on caller side, as the intent 
		/// handler will handle that and also update the last facing direction accordingly.
		/// </summary>
		/// <param name="intent"></param>
		public virtual void SetMovementIntent(MovementIntent intent) {
			this._intentHandler.TrySetIntent(intent, IsStunned);
		}
		public virtual void SetResponsiveness(float responsiveness = Mathfx.DEFAULT_RESPONSIVENESS) {
			this._currentResponsiveness = responsiveness;
		}
		/// <summary>
		/// Applies the integrated Physics and Intent layers.
		/// Should be called from FixedUpdate via a Physics Controller or directly if this component manages its own ticks.
		/// </summary>
		public virtual void ApplyPhysics(float stateSpeedMultiplier = 1f) {
			// 1. Physics Layer (Always active, pre-masked in force handler)
			Vector3 physicsPart = this._forceHandler.NetForce;

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
			this.rb.linearVelocity = (this.rb.linearVelocity * (1f - this._currentResponsiveness)) +
			(Vector2)(targetVelocity * this._currentResponsiveness);
		}
		#endregion

		#region ISaveable Implementation 
		public ISaveData GetSaveData() => new MovementComponentSaveData(this.rb.position);
		public void LoadFromSaveData(ISaveData data) {
			if (data is MovementComponentSaveData s) this.rb.position = s.Position.ToVector3();
		}
		#endregion

		#region IStunnable Implementation
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

		#region  IKnockbackable Implementation
		/// <summary>
		/// Applies a directional knockback or pull effect to the actor. The direction is calculated based on the hit point relative to the actor's current position. If isPulling is true, the actor will be pulled towards the hit point; otherwise, it will be pushed away.
		/// The impulse parameter controls the strength of the force applied, and the duration parameter determines
		/// </summary>
		/// <param name="hitPoint">The point of impact relative to the actor's position.</param>
		/// <param name="duration">The duration of the knockback effect.</param>
		/// <param name="impulse">The strength of the force applied.</param>
		/// <param name="isPulling">Indicates whether the actor should be pulled towards the hit point.</param>
		public virtual void ApplyKnockback(Vector3 hitPoint, float duration, float impulse = 1f, bool isPulling = false) {
			Vector2 origin = this.rb.position;
			Vector3 direction = isPulling ? ((Vector3)origin - hitPoint) : (hitPoint - (Vector3)origin);
			this._forceHandler.AddForce(direction.normalized * impulse, duration);
		}

		#endregion

		#region Private Helpers
		private void SubscribeToStats() {
			if (this._readOnlycharacterStatsSystem.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsSubscribe(CharacterStatType.SPD, SetDefaultMovementSpeed);
				SetDefaultMovementSpeed(this._readOnlycharacterStatsSystem.CurrentStats[CharacterStatType.SPD].GetValue());
			}
		}

		private void UnsubscribeFromStats() {
			if (this._readOnlycharacterStatsSystem.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsUnsubscribe(CharacterStatType.SPD, SetDefaultMovementSpeed);
			}
		}

		protected virtual void SetDefaultMovementSpeed(float speed) => this.defaultMovementSpeed = speed;
		#endregion
	}

}
