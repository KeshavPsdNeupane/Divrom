using UnityEngine;

namespace Kope.Actor.New {

	[CreateAssetMenu(menuName = "Scriptable Objects/Entity States/Attack", fileName = "New Attack State")]
	public class EntityAttackState : EntityStateBaseSO {
		[Header("Attack Settings")]
		[SerializeField, Range(0f, 1f)]
		private float movementSpeedMultiplier = 0.5f;
		public override StateChangeResult EnterState() {
			var res = this._animationComponent.PlayAnimation(this._profileData, true).ToStateChangeResult();
			return res;
		}
		public override void TickUpdate() {
			if (this._animationComponent.IsAnimationFinished(this._profileData)) {
				RequestTransitionToIdle();
			}
		}
		public override void TickFixedUpdate() {
			this._movementComponent.ApplyPhysics(this.movementSpeedMultiplier);
		}
	}
}