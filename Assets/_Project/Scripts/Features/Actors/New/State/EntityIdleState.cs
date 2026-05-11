using Kope.Component.Movement;
using Kope.Core.Type.EnumAsset;
using UnityEngine;
namespace Kope.Actor.New {

	[CreateAssetMenu(menuName = "Scriptable Objects/Entity States/Idle", fileName = "New Idle State")]
	public class EntityIdleState : EntityStateBaseSO {
		[SerializeField] private EnumPicker movementStateEnum;

		public override StateChangeResult EnterState() {
			// attempt to play idle.
			return this._animationComponent.PlayAnimation(this._profileData).ToStateChangeResult();
		}



		public override void TickUpdate() {
			if (this._movementComponent.Direction.sqrMagnitude >= MovementComponentBase.MOVEMENT_EPSILON) {
				int movementEnumId = this.movementStateEnum.GetSelectedEnumId();

				// Directly transitioning to movement. While other states must call 
				// 'TransitionToIdle' to reset, Idle can explicitly route to specific states.
				_ = this._stateManagement.ChangeState(movementEnumId);
			}
		}
		public override void TickFixedUpdate() {
			// lets see.
			this._movementComponent.ApplyPhysics();
		}
	}
}

