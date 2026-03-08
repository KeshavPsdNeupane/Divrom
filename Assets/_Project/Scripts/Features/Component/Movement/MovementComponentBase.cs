using Kope.Core.CompilerServices;
using UnityEngine;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentSystem;

namespace Kope.Component.Movement
{

	/// <summary>
	/// Defines the type of movement intent.
	/// Just a simple enum to indicate what kind of movement is intended.
	/// </summary>
	public enum MovementIntentType
	{
		Stop = 0,
		Move = 10,
		Attacking = 20,
	}


	public struct MovementIntent
	{
		public Vector2 Direction;

		/// <summary>
		/// Must be between 0 and 1.
		/// Speed boost should be handled on Stats level, not here.
		/// This mult is used to scale the movement speed as needed.
		/// For example, a value of 1.0 is normal speed for movement,
		/// we can use like 0.5 for movement speed when we are attacking,
		/// and so on.
		/// </summary>
		public MovementIntentType IntentType;
		public MovementIntent(Vector2 direction, MovementIntentType intentType = MovementIntentType.Stop)
		{
			this.Direction = direction;
			this.IntentType = intentType;
		}
	}



	public class MovementComponentBase : InitializableBase
	{
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
		public Vector2 Direction => this.currentIntent.Direction;
		public Vector2 Position => this.rb.position;
		private float speedMultiplier = 1f;
		/// <summary>
		/// Gets the Rigidbody2D associated with this movement component.
		/// Highly discouraged to use this reference to manipulate movement directly.
		/// Use SetMovementIntent instead to ensure proper movement handling.
		/// </summary>
		public Rigidbody2D Rigidbody => this.rb;
		public override void OnInit()
		{
			base.OnInit();
			if (this.ecr == null)
			{
				MyLogger.Error($"MovementComponentBase ({gameObject.name}): " +
			   $"EntityComponentStore not assigned. Movement will remain uninitialized.\n{GetParentGameObjectStackTraceMessage()}");
				return;
			}

			if (this.rb == null)
			{
				MyLogger.Error($"MovementComponentBase ({gameObject.name}): " +
			   $"Rigidbody2D not assigned. Movement will remain uninitialized.\n{GetParentGameObjectStackTraceMessage()}");
				return;
			}

			if (this.ecr.ComponentRegistry.TryGetComponent(out CharacterStatsSystem statsSystem))
			{
				this.characterStatsSystem = statsSystem;
			}
			else
			{
				MyLogger.Warn($"MovementComponentBase ({gameObject.name}): " +
			   $"CharacterStatsSystem not found in {this.ecr.name}. Stats-based movement speed will be unavailable.\n{GetParentGameObjectStackTraceMessage()}");
			}
		}

		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable()
		{
			UnsubscribeFromStats();
			StopMovement();
		}

		private void SubscribeToStats()
		{
			if (this.characterStatsSystem != null &&
				this.characterStatsSystem.CurrentStats != null)
			{
				this.characterStatsSystem.StatsSubscribe(CharacterStatType.SPD, SetDefaultMovementSpeed);
				// Initial fetch 
				SetDefaultMovementSpeed(this.characterStatsSystem.CurrentStats[CharacterStatType.SPD].GetValue());
			}
		}
		private void UnsubscribeFromStats()
		{
			if (this.characterStatsSystem != null &&
				this.characterStatsSystem.CurrentStats != null)
			{
				this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.SPD, SetDefaultMovementSpeed);
			}
		}

		protected override void OnFixedUpdate()
		{
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
		public void SetSpeedMultiplier(float multiplier = 1f)
		{
			this.speedMultiplier = multiplier;
		}

		/// <summary>
		/// Sets the default movement speed.
		/// This is usually called by the CharacterStatsSystem when the SPD stat changes.
		/// </summary>
		public virtual void SetDefaultMovementSpeed(float speed)
		=> this.defaultMovementSpeed = speed;

		/// <summary>
		/// Sets the movement intent for this component.
		/// The intent direction will be normalized if its magnitude is greater than the direction epsilon.
		/// if u wanna do some fancy with direction, just lerp or slerp or whatever before passing it here.
		/// this function will just assign the direction to velocity after normalization. 
		/// it does not do any smoothing or interpolation.
		/// </summary>
		/// <param name="intent"></param>
		public virtual void SetMovementIntent(MovementIntent intent)
		{
			if (intent.Direction.sqrMagnitude > MOVEMENT_EPSILON)
			{
				intent.Direction.Normalize();
			}
			else
			{
				intent.Direction = Vector2.zero;
			}
			this.currentIntent = intent;
		}

		public void StopMovement()
		{
			this.currentIntent = default;
		}


		protected virtual void ApplyPhysics()
		{
			Vector2 targetVelocity = Vector2.zero;
			if (this.currentIntent.IntentType != MovementIntentType.Stop)
			{
				targetVelocity = this.speedMultiplier * this.defaultMovementSpeed * this.currentIntent.Direction;
			}

			// Blend physics velocity (from collisions) with desired velocity
			// This allows entities to push each other while maintaining responsive control
			Vector2 physicsInfluence = this.rb.linearVelocity * 0.3f; // Preserve collision response
			Vector2 intentInfluence = targetVelocity * 0.7f;

			this.rb.linearVelocity = physicsInfluence + intentInfluence;
		}
	}

}