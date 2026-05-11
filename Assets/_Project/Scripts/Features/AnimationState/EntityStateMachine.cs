using UnityEngine;

namespace Kope.Feature.AnimationState {

	public class EntityStateMachine {
		private EntityStateBaseSO currentState;
		public EntityStateBaseSO CurrentState => this.currentState;
		public void Initialize(EntityStateBaseSO state) {
			if (state == null) {
				Debug.LogError("[Kope.State] Cannot initialize EntityStateMachine with a null state.");
				return;
			}
			this.currentState = state;
			this.currentState.EnterState();
		}
		public void ChangeState(EntityStateBaseSO state) {
			if (state == null) {
				Debug.LogError("[Kope.State] Cannot change to a null state.");
				return;
			}
			if (this.currentState != null) this.currentState.ExitState();
			this.currentState = state;
			this.currentState.EnterState();
		}

	}
}