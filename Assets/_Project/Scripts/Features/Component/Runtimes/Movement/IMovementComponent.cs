using System;
using Kope.Core;
using UnityEngine;

namespace Kope.Component.Movement {

	public enum MovementIntentPriority {
		Ambient = 0,  // Passive/Background movement
		Normal = 10,   // Standard AI/Player Input
		High = 20,     // Special state movement (e.g., focused movement during an ability)
		Scripted = 30, // Hard locks (e.g., scripted sequences)

		// 'UnlockNext' is a special priority that allows the next intent to bypass 
		// priority checks, this allows for scripted scene to release control back 
		// to the player after a cutscene or special ability without needing 
		// to worry about intent priority conflicts.
		UnlockNext = 40,
	}

	public enum MovementIntentType {
		Stop = 0,
		Move = 10,
		Attacking = 20
		// Knockback removed: now handled by ForceInstance
	}

	public struct MovementIntent : IEquatable<MovementIntent> {
		public Vector3 Direction;
		public MovementIntentType IntentType;
		public MovementIntentPriority Priority;
		public MovementIntent(Vector3 direction,
					MovementIntentType intentType = MovementIntentType.Stop,
					MovementIntentPriority priority = MovementIntentPriority.Normal
					) {
			this.Direction = direction;
			this.IntentType = intentType;
			this.Priority = priority;

		}
		public static MovementIntent Default =>
		new(Vector3.zero, MovementIntentType.Stop, MovementIntentPriority.Normal);
		public static MovementIntent UnlockNext =>
		new(Vector3.zero, MovementIntentType.Stop, MovementIntentPriority.UnlockNext);



		public readonly bool Equals(MovementIntent other) {
			return this.Direction == other.Direction
				&& this.IntentType == other.IntentType
				&& this.Priority == other.Priority;
		}

	}

	/// <summary>
	/// Provides a orientation vector representing the actor's most recent "facing" or "heading."
	/// This is context-agnostic: it could be the last movement vector or the last look-at target.
	/// </summary>
	public interface IDirectionProvider {
		/// <summary>
		/// The last valid direction registered by the provider. 
		/// Ensures systems (like Animations) have a non-zero fallback when current movement stops.
		/// </summary>
		Vector3 Direction { get; }
	}

	/// <summary>
	/// Defines core locomotion capabilities. Inherits from ILastDirectionProvider to ensure 
	/// that any entity capable of movement also provides a persistent heading for secondary systems.
	/// </summary>
	public interface IMovementComponent : IDirectionProvider {
		Vector3 Position { get; }
		AxisMode Dimension { get; }

		void SetResponsiveness(float responsiveness);
		void SetMovementIntent(MovementIntent intent);
		Vector3 GetLookingAtDirection();
		void ApplyPhysics(float stateSpeedMultiplier = 1f);
	}



	public interface IKnockbackable {
		void ApplyKnockback(Vector3 direction, float duration, float impulse = 2.5f, bool isPulling = false);
	}
}