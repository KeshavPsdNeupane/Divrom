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

	public interface IMovementComponent {
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