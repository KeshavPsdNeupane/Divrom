using UnityEngine;

namespace Kope.Actor.New {
	public enum StateChangeResult : short {
		Success = 0,
		Denied_Locked = 1,      // Current state is non-receptive (Input Locked)
		Denied_Busy = 2,        // Underlying animation is still playing
		Error_NotFound = 3,     // State Hash missing in Lookup Table
		Error_LogicMissing = 4, // ScriptableObject reference is null
		NoAction_AlreadyActive = 5,
		Failed = 99
	}
	public class EntityStateMachine {
		public EntityStateBaseSO CurrentState { get; private set; }

		public void Initialize(EntityStateBaseSO initialState) {
			if (initialState == null) {
				Debug.LogError("[Kope.State] Cannot initialize with a null state.");
				return;
			}
			CurrentState = initialState;
			CurrentState.EnterState();
		}

		public StateChangeResult ExecuteTransition(EntityStateBaseSO newState) {
			if (newState == null) return StateChangeResult.Error_LogicMissing;
			if (CurrentState == newState) return StateChangeResult.NoAction_AlreadyActive;

			// Check if current state allows interruption (e.g., aren't stunned/locked)
			if (CurrentState != null && !CurrentState.CanStateAcceptExternalCommand) {
				return StateChangeResult.Denied_Locked;
			}

			if (CurrentState != null) CurrentState.ExitState();
			CurrentState = newState;
			// Delegate status to the new state's entry logic
			return CurrentState.EnterState();
		}
	}
}
