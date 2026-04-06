using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.Entity;
using Kope.SaveSystem;
using Kope.Core;
using Newtonsoft.Json;

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

	public interface IMovementComponent {
		Vector3 Direction { get; }
		Vector3 Position { get; }
		void SetMovementIntent(MovementIntent intent);
		Vector3 GetLookingAtDirection();
	}


	[System.Serializable]
	public class MovementComponentSaveData : ISaveData {
		[JsonProperty("position")]
		public Vec3 Position;
		[JsonProperty("velocity")]
		public Vec3 Velocity;
		[JsonProperty("dimension")]
		public Dimension Dimension;

		public MovementComponentSaveData(Vector3 position, Vector3 velocity, Dimension dimension) {
			this.Position = new Vec3(position);
			this.Velocity = new Vec3(velocity);
			this.Dimension = dimension;
		}
	}

	public class MovementComponentBase : InitializableBase, IMovementComponent, ISaveable {
		[SerializeField] protected Dimension dimension = Dimension.TwoD;
		[SerializeField] protected Rigidbody2D rb;
		[SerializeField] protected EntityComponentsRegistry ecr;
		[SerializeField] protected float defaultMovementSpeed = 2f;

		private CharacterStatsSystem _readOnlycharacterStatsSystem;

		/// <summary>
		/// this is universal threshold to determine if direction is significant enough to consider.
		/// so no need to square it every time.
		/// </summary>
		public const float MOVEMENT_EPSILON = 0.1f;

		private Vector3 _lastDirection = Vector3.right;
		protected MovementIntent _currentIntent;
		public float Mass => this.rb.mass;
		public Vector3 Direction => this._currentIntent.Direction;
		public Vector3 Position => this.rb.position;


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
				return new Vector3(this._lastDirection.x, this._lastDirection.y, 0f);
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
				this._readOnlycharacterStatsSystem = statsSystem;
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
			if (this._readOnlycharacterStatsSystem != null &&
				this._readOnlycharacterStatsSystem.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsSubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
				// initial fetch
				SetDefaultMovementSpeed(this._readOnlycharacterStatsSystem.CurrentStats[CharacterStatType.AGI].GetValue());
			}
		}
		private void UnsubscribeFromStats() {
			if (this._readOnlycharacterStatsSystem != null &&
				this._readOnlycharacterStatsSystem.CurrentStats != null) {
				this._readOnlycharacterStatsSystem.StatsUnsubscribe(CharacterStatType.AGI, SetDefaultMovementSpeed);
			}
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
				this._lastDirection = intent.Direction;
			} else {
				intent.Direction = Vector3.zero;
			}
			this._currentIntent = intent;

		}

		public void StopMovement() {
			this._currentIntent = default;
		}


		/// <summary>
		/// Applies physics-based movement based on the current movement intent and a speed multiplier.
		/// The speed multiplier can be used to implement effects like slowing down the entity during attacks or debuffs.
		/// The method blends the desired velocity from the movement intent with the current physics velocity to allow for
		/// responsive control while still respecting collisions and other physics interactions.
		/// Must be called by State themselves in their TickPhysicUpdate to take effect,
		///  giving them control over when movement is applied during the update cycle.
		/// </summary>
		/// <param name="speedMultiplier"></param>
		public virtual void ApplyPhysics(float speedMultiplier = 1f) {
			Vector3 targetVelocity = Vector3.zero;
			if (this._currentIntent.IntentType != MovementIntentType.Stop) {
				targetVelocity = speedMultiplier * this.defaultMovementSpeed * this._currentIntent.Direction;
			}

			// Blend physics velocity (from collisions) with desired velocity
			// This allows entities to push each other while maintaining responsive control
			Vector3 physicsInfluence = this.rb.linearVelocity * 0.3f; // Preserve collision response
			Vector3 intentInfluence = targetVelocity * 0.7f;

			this.rb.linearVelocity = physicsInfluence + intentInfluence;
		}

		public ISaveData GetSaveData() {
			return new MovementComponentSaveData(this.Position, this.rb.linearVelocity, this.dimension);
		}

		public void LoadFromSaveData(ISaveData data) {
			if (data is MovementComponentSaveData saveData) {
				this.rb.position = saveData.Position.ToVector3();
				this.rb.linearVelocity = saveData.Velocity.ToVector3();
				this.dimension = saveData.Dimension;
			}
		}
	}

}