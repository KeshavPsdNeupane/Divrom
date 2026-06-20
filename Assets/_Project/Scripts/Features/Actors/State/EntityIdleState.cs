using Kope.Component.Movement;
using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Actor.New {

	/// <summary>
	/// The foundational routing hub for an entity when at rest.
	/// </summary>
	/// <remarks>
	/// <para><b>Stateful Routing vs. Passive Fallback:</b> While this state may appear simple, it behaves completely 
	/// differently than a passive baseline like <see cref="DefaultAnimationStateSO"/>. Instead of relying strictly on an 
	/// animation clip's duration to run down, this is an active, stateful controller.</para>
	/// <para><b>Responsibility Matrix:</b> It continuously observes structural physics parameters (<see cref="IMovementComponent.Direction"/>) 
	/// on the update frame loop, transforming into an internal traffic router that schedules transitions into target 
	/// locomotion states the microsecond inputs cross threshold tolerances.</para>
	/// </remarks>
	[CreateAssetMenu(menuName = "Scriptable Objects/Entity States/Idle", fileName = "New Idle State")]
	public class EntityIdleState : EntityStateBaseSO {

		[SerializeField, Tooltip("The structural Enum Asset pointer corresponding to the primary locomotion/movement state.")]
		private EnumPicker movementStateEnum;

		/// <summary>
		/// Monitors directional physics changes to dynamically break out of the idle loop.
		/// </summary>
		public override void TickUpdate() {
			// MOVEMENT_EPSILON prevents precision jitter from accidentally triggering state changes
			if (this._movementComponent.Direction.sqrMagnitude >= MovementComponentBase.MOVEMENT_EPSILON) {
				int movementEnumId = this.movementStateEnum.GetSelectedEnumId();

				// CRITICAL ARCHITECTURAL CHOICE: Because Idle is the root baseline,
				// handleFallbackInternally is set to 'false'.
				// If the targeted Movement state is temporarily busy or locked out, 
				// we simply remain in Idle for another frame 
				// rather than triggering an infinite fallback recursion loop.
				_ = this._stateManagement.ChangeState(movementEnumId, handleFallbackInternally: false);
			}
		}

		/// <summary>
		/// Executes continuous rigid body calculation and deceleration anchoring while the entity remains static.
		/// </summary>
		public override void TickFixedUpdate() {
			this._movementComponent.ApplyPhysics();
		}
	}
}