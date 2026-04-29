using Kope.Core;
using UnityEngine;

namespace Kope.Component.Movement {

	public enum MovementIntentPriority {
		Ambient = 0,  // Passive/Background movement
		Normal = 10,   // Standard AI/Player Input
		High = 20,     // Special state movement (e.g., focused movement during an ability)
		Override = 30  // Hard locks (e.g., scripted sequences)
	}

	public enum MovementIntentType {
		Stop = 0,
		Move = 10,
		Attacking = 20
		// Knockback removed: now handled by ForceInstance
	}

	public struct MovementIntent {
		public Vector3 Direction;
		public MovementIntentType IntentType;
		public MovementIntentPriority Priority;

		public MovementIntent(Vector3 direction,
							MovementIntentType intentType = MovementIntentType.Stop,
							MovementIntentPriority priority = MovementIntentPriority.Normal) {
			this.Direction = direction;
			this.IntentType = intentType;
			this.Priority = priority;
		}

		/// <summary>
		/// Helper to quickly create a stop intent.
		/// </summary>
		public static MovementIntent Default => new(Vector3.zero, MovementIntentType.Stop, MovementIntentPriority.Normal);
	}

	/// <summary>
	/// Provides a orientation vector representing the actor's most recent "facing" or "heading."
	/// This is context-agnostic: it could be the last movement vector or the last look-at target.
	/// </summary>
	public interface ILastDirectionProvider {
		/// <summary>
		/// The last valid direction registered by the provider. 
		/// Ensures systems (like Animations) have a non-zero fallback when current movement stops.
		/// </summary>
		Vector3 LastDirection { get; }
	}

	/// <summary>
	/// Defines core locomotion capabilities. Inherits from ILastDirectionProvider to ensure 
	/// that any entity capable of movement also provides a persistent heading for secondary systems.
	/// </summary>
	public interface IMovementComponent : ILastDirectionProvider {
		Vector3 Direction { get; }
		Vector3 Position { get; }
		AxisMode Dimension { get; }

		void SetMovementIntent(MovementIntent intent);
		Vector3 GetLookingAtDirection();
		void StopMovementIntent();
	}



	public interface IKnockbackable {
		void ApplyKnockback(Vector3 direction, float duration, float impulse = 2.5f, bool isPulling = false);
	}
}