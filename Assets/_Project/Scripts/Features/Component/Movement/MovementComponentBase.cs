using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentSystem;

namespace Kope.Component.Movement {

	public enum Dimension {
		TwoD,
		ThreeD,
	}
	/// <summary>
	/// Defines the type of movement intent.
	/// Just a simple enum to indicate what kind of movement is intended.
	/// </summary>
	public enum MovementIntentType {
		Stop = 0,
		Move = 10,
		Attacking = 20,
	}


	public struct MovementIntent {
		public Vector3 Direction;
		public MovementIntentType IntentType;
		public MovementIntent(Vector3 direction, MovementIntentType intentType = MovementIntentType.Stop) {
			this.Direction = direction;
			this.IntentType = intentType;
		}
	}



	public class MovementComponentBase : InitializableBase {
		[SerializeField] protected Dimension dimension = Dimension.TwoD;
		[SerializeField] protected Rigidbody2D rb;
		[SerializeField] protected EntityComponentsRegistry ecr;
		[SerializeField] protected float defaultMovementSpeed = 2f;

		private CharacterStatsSystem characterStatsSystem;

		/// <summary>
		/// this is universal threshold to determine if direction is significant enough to consider.
		/// so no need to square it every time.
		/// </summary>
		public const float MOVEMENT_EPSILON = 0.1f;

		protected MovementIntent currentIntent;
		public float Mass => this.rb.mass;
		public Vector3 Direction => this.currentIntent.Direction;
		public Vector3 Position => this.rb.position;
		private float speedMultiplier = 1f;

		private Vector3 lastDirection = Vector3.right;

		/// <summary>
		/// Gets the current looking direction of the entity based on its movement intent and dimension.
		/// For 2D movement, it projects the last movement direction onto the XY plane.
		/// For 3D movement, it uses the Rigidbody's forward direction as the looking direction.
		/// If we implement strafing or other movement mechanics in the future, we may
		///  need to adjust this logic to account for those cases.
		/// </summary>
		/// <returns></returns>
		public virtual Vector3 GetLookingAtDirection() {
			if (this.dimension == Dimension.TwoD) {
				// we only care about x and y for 2D movement, so we project the
				//  lastDirection onto the XY plane.
				return new Vector3(this.lastDirection.x, this.lastDirection.y, 0f);
			} else {
				// for 3D movement, we can use the Rigidbody's forward direction as the looking direction.
				// we could change this if we are implementing some kind of strafing movement, 
				// but for now we will just assume the looking direction is the same as the movement direction.
				return this.rb.transform.forward;
			}
		}

		/// <summary>
		/// Gets the Rigidbody2D associated with this movement component.
		/// Highly discouraged to use this reference to manipulate movement directly.
		/// Use SetMovementIntent instead to ensure proper movement handling.
		/// </summary>
		public Rigidbody2D Rigidbody => this.rb;
		protected override bool OnInit() {
			if (this.ecr == null) {
				MyLogger.Error($"MovementComponentBase ({gameObject.name}): " +
			   $"EntityComponentStore not assigned. Movement will remain uninitialized.\n{GetParentGameObjectHeirarchyMessage()}");
				return false;
			}

			if (this.rb == null) {
				MyLogger.Error($"MovementComponentBase ({gameObject.name}): " +
			   $"Rigidbody2D not assigned. Movement will remain uninitialized.\n{GetParentGameObjectHeirarchyMessage()}");
				return false;
			}
			// MovementComponentBase is not muating the CharacterStatsSystem, so TryGetComponent is sufficient here.
			// we are only subscribing to stat changes, not modifying the stats directly, 
			// so we don't need mutatable access. so using TryGetComponent for semantic clarity
			if (this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out CharacterStatsSystem statsSystem)) {
				this.characterStatsSystem = statsSystem;
			} else {
				MyLogger.Warn($"MovementComponentBase ({gameObject.name}): " +
			   $"CharacterStatsSystem not found in {this.ecr.name}. Stats-based movement speed will be unavailable.\n{GetParentGameObjectHeirarchyMessage()}");
				return false;
			}
			return true;
		}

		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable() {
			UnsubscribeFromStats();
			StopMovement();
		}

		private void SubscribeToStats() {
			if (this.characterStatsSystem != null &&
				this.characterStatsSystem.CurrentStats != null) {
				this.characterStatsSystem.StatsSubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
				// initial fetch
				SetDefaultMovementSpeed(this.characterStatsSystem.CurrentStats[CharacterStatType.AGI].GetValue());
			}
		}
		private void UnsubscribeFromStats() {
			if (this.characterStatsSystem != null &&
				this.characterStatsSystem.CurrentStats != null) {
				this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
			}
		}

		protected override void OnFixedUpdate() {
			ApplyPhysics();
		}

		/// <summary>
		/// Sets the speed multiplier for this movement component.
		/// This multiplier scales the default movement speed, allowing for temporary speed boosts or reductions.
		/// Like when attacking, we can set it to 0.5 to reduce movement speed, 
		/// or when using a speed boost, we can set it to 1.5 to increase movement speed.
		/// this is mainly used to restrict movement during certain actions (like attacking) 
		/// or to apply temporary speed for for dodge or so on.
		/// </summary>
		/// <param name="multiplier"></param>
		public void SetSpeedMultiplier(float multiplier = 1f) {
			this.speedMultiplier = multiplier;
		}

		/// <summary>
		/// Sets the default movement speed.
		/// This is usually called by the CharacterStatsSystem when the SPD stat changes.
		/// </summary>
		public virtual void SetDefaultMovementSpeed(float speed) {
			this.defaultMovementSpeed = speed;
		}

		/// <summary>
		/// Sets the movement intent for this component.
		/// The intent direction will be normalized if its magnitude is greater than the direction epsilon.
		/// if u wanna do some fancy with direction, just lerp or slerp or whatever before passing it here.
		/// this function will just assign the direction to velocity after normalization. 
		/// it does not do any smoothing or interpolation.
		/// </summary>
		/// <param name="intent"></param>
		public virtual void SetMovementIntent(MovementIntent intent) {
			if (intent.Direction.sqrMagnitude > MOVEMENT_EPSILON) {
				intent.Direction.Normalize();
				// we only update lastDirection when we have a significant movement intent,
				//  to avoid jittery lastDirection when we are trying to stop or have very minor movement.
				this.lastDirection = intent.Direction;
			} else {
				intent.Direction = Vector3.zero;
			}
			this.currentIntent = intent;

		}

		public void StopMovement() {
			this.currentIntent = default;
		}


		protected virtual void ApplyPhysics() {
			Vector3 targetVelocity = Vector3.zero;
			if (this.currentIntent.IntentType != MovementIntentType.Stop) {
				targetVelocity = this.speedMultiplier * this.defaultMovementSpeed * this.currentIntent.Direction;
			}

			// Blend physics velocity (from collisions) with desired velocity
			// This allows entities to push each other while maintaining responsive control
			Vector3 physicsInfluence = this.rb.linearVelocity * 0.3f; // Preserve collision response
			Vector3 intentInfluence = targetVelocity * 0.7f;

			this.rb.linearVelocity = physicsInfluence + intentInfluence;
		}
	}

}