namespace Kope.Actor.States {
	public enum StateChangeResult : short {
		Success = 0,
		Denied_Locked = 1,      // Current state is non-receptive (Input Locked)
		Denied_Busy = 2,        // Underlying animation is still playing
		Error_NotFound = 3,     // State Hash missing in Lookup Table
		Error_LogicMissing = 4, // ScriptableObject reference is null
		NoAction_AlreadyActive = 5,
		Internal_Fallback = 6,     // Used internally to indicate a fallback transition was triggered
		Failed = 99
	}
	public class EntityStateMachine {
		public EntityStateBaseSO CurrentState { get; private set; }

		private EntityStateBaseSO _nextState;
		private bool _isPendingChange;
		private bool _isNextChangeFallback;

		public void Initialize(EntityStateBaseSO initialState) {
			this.CurrentState = null;
			this._nextState = initialState;
			this._isPendingChange = true;
			this._isNextChangeFallback = false;
		}

		public StateChangeResult ScheduleTransition(EntityStateBaseSO newState, bool isFallback = false) {
			if (newState == null) return StateChangeResult.Error_LogicMissing;
			if (this.CurrentState == newState) return StateChangeResult.NoAction_AlreadyActive;

			// Fallbacks bypass all 'Is Busy' or 'Is Locked' checks.
			if (!isFallback) {
				var feasibility = this.CurrentState.CheckStateChangeFeasibility(newState.ProfileData);

				if (feasibility != StateChangeResult.Success) {
					return feasibility;
				}
			}
			this._nextState = newState;
			this._isNextChangeFallback = isFallback;
			this._isPendingChange = true;

			return isFallback ? StateChangeResult.Internal_Fallback : StateChangeResult.Success;
		}
		public StateChangeResult ProcessStateChanges() {
			if (!this._isPendingChange || this._nextState == null)
				return StateChangeResult.NoAction_AlreadyActive;

			if (this.CurrentState != null) this.CurrentState.ExitState();

			this.CurrentState = this._nextState;
			// Capture the result of entering (e.g. animator.Play result)
			StateChangeResult enterResult = this.CurrentState.EnterState();

			this._nextState = null;
			this._isPendingChange = false;

			if (this._isNextChangeFallback) {
				this._isNextChangeFallback = false;
				return StateChangeResult.Internal_Fallback;
			}

			return enterResult;
		}
	}
}

