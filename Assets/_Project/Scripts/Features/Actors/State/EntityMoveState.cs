using Kope.Actor.States;
using Kope.Core.Mathfx;
using UnityEngine;

namespace Kope.Actor {
	[CreateAssetMenu(menuName = "Scriptable Objects/Entity States/Move", fileName = "New Move State")]
	public class EntityMoveState : EntityStateBaseSO {
		[SerializeField, Tooltip("Multiplier for the movement speed during this state." +
		"Use this to create variations like Walk vs Run without needing separate logic SOs." +
		"1 = normal speed, <1 = slower, >1 = faster.")]
		private float moveSpeedMultiplier = 1f;


		public override void TickUpdate() {
			if (this._movementComponent.Direction.sqrMagnitude <= Mathfx.SQUARE_DIRECTION_UPPER_EPSILON) {
				this._stateManagement.TransitionToIdle();
				return;
			}
			// looking direction is handled by the animation component, so just pass movement direction.
			this._animationComponent.SetDirection(this._movementComponent.Direction);
		}
		public override void TickFixedUpdate() {
			this._movementComponent.ApplyPhysics(this.moveSpeedMultiplier);
		}
	}
}
