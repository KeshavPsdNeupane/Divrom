using Kope.Component.Movement;
using Kope.Core.Type.EnumAsset;
using UnityEngine;
namespace Kope.Actor.New {

	[CreateAssetMenu(menuName = "Scriptable Objects/Entity States/Idle", fileName = "New Idle State")]
	public class EntityIdleState : EntityStateBaseSO {
		[SerializeField] private EnumPicker movementStateEnum;

		public override void TickUpdate() {
			if (this._movementComponent.Direction.sqrMagnitude >= MovementComponentBase.MOVEMENT_EPSILON) {
				long movementEnumId = this.movementStateEnum.GetSelectedEnumId();

				// already in Idle; we don't need to 'fallback' to it if Move is briefly Busy.
				_ = this._stateManagement.ChangeState(movementEnumId, false);
			}
		}
		public override void TickFixedUpdate() {
			// lets see.
			this._movementComponent.ApplyPhysics();
		}
	}
}

