using System.Collections.Generic;
using Kope.Actor.New;
using Kope.Component;
using Kope.Component.Attack;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Feature.AnimationState {
	/// <summary>
	/// Manages the registration and lookup of entity animation states.
	/// Bridges Enum-based state selection with runtime Profile and ScriptableObject logic.
	/// </summary>
	public class EntityStateManagement : InitializableBase {
		[Header("--- CORE DEPENDENCIES ---")]
		[SerializeField]
		[Tooltip("Reference to the central registry containing all entity components.")]
		private EntityComponentsRegistry ecr;

		[SerializeField]
		private bool loadOnStart = true;

		[Header("--- IDLE CONFIGURATION ---")]
		[Space(10)]
		[SerializeField]
		[Tooltip("Primary fallback state. This state is prioritized over the general map below.")]
		private AnimationStateData<AnimationStateProfile> defaultIdleStateData;

		[Header("--- STATE MAPPING ---")]
		[Space(10)]
		[SerializeField]
		[Tooltip("Maps Animation State Enums to their respective logic and data profiles.\n\n" +
				 "Note: If the 'Idle' state is mapped here, it will be ignored in favor of the Dedicated Idle slot.")]
		private EnumTable<AnimationStateData<AnimationStateMappedProfile>> animationStateMap;

		// --- PRIVATE RUNTIME DATA ---
		private IMovementComponent _movementComponent;
		private IAttackComponent _attackComponent;
		private IAnimationComponentNew _animationComponent;

		private int _idleStateHash;

		private EntityStateMachine _stateMachine;
		/// <summary>
		/// Optimized O(1) lookup: Animator Hash -> Profile Data.
		/// </summary>
		private readonly Dictionary<int, EntityStateBaseSO> _animationStateHashLookup = new();

		private void Awake() {
			if (this.loadOnStart) Init();
		}

		protected override bool OnInit() {
			// 1. Dependency Validation: Ensure the registry and its components exist
			if (!ValidateDependencies()) return false;

			// 2. Hydrate Idle State: Always the baseline for state machine fallback
			var idleStateData = this.defaultIdleStateData.Profile.ToData();
			this._idleStateHash = idleStateData.Hash;
			int idleID = this.defaultIdleStateData.Profile.AnimationState.GetSelectedEnumId();
			EntityStateBaseSO idleLogic = null;
			Debug.Log($"[Kope.State] Registered Baseline: Idle (ID: {idleID})");

			if (this.defaultIdleStateData.StateSO != null) {
				idleLogic = Instantiate(this.defaultIdleStateData.StateSO);
				idleLogic.Init(idleStateData, this._movementComponent, this._attackComponent, this._animationComponent);
			} else {
				Debug.LogWarning("[Kope.State] No logic assigned for Idle state. Ensure the Entity can still function without it.");
			}
			this._animationStateHashLookup[this._idleStateHash] = idleLogic;




			// 3. Hydrate Lookup Table: Build the hash-table for O(1) runtime state switching
			foreach (var (instance, profile) in this.animationStateMap.BindLookup) {
				// Ensure we don't double-register Idle logic if found in the table
				if (instance.InternalValue == idleID) continue;

				var profileData = profile.Profile.ToData(instance.Alias);
				// null if no logic assigned, which is valid for purely data-driven states
				EntityStateBaseSO profileLogic = null;
				if (profile.StateSO != null) {
					// if not null init a new instance of the ScriptableObject for runtime use (to avoid shared state issues)
					// and inject dependencies via the Init method (could be extended to use a context 
					// struct if more dependencies are needed in the future), but for 3 dependency types this 
					// function parameter list is still manageable and more straightforward than a full context object.
					profileLogic = Instantiate(profile.StateSO);
					profileLogic.Init(profileData, this._movementComponent, this._attackComponent, this._animationComponent);
				}

				if (!_animationStateHashLookup.TryAdd(profileData.Hash, profileLogic)) {
					Debug.LogWarning($"[Kope.State] Duplicate hash detected for {instance.Alias}. Check for naming collisions.");
					continue;
				}

				Debug.Log($"[Kope.State] Registered: {instance.Alias} | Profile: {profileData}");
			}

			return true;
		}


		public AnimationStatus ChangeState(int animationHash) {
			if (!this._animationStateHashLookup.TryGetValue(animationHash, out var stateLogic)) {
				Debug.LogWarning($"[Kope.State] Animation hash {animationHash} not found in state lookup.");
				return AnimationStatus.AnimationDoesNotExist;
			}



			if (stateLogic != null && this._stateMachine != null) {
				this._stateMachine.ChangeState(stateLogic);
				Debug.Log($"[Kope.State] Transitioning to state with hash {animationHash}.");
				return AnimationStatus.Success;
			} else {
				Debug.LogWarning($"[Kope.State] No logic assigned for animation hash {animationHash}. State change aborted.");
				return AnimationStatus.AnimationDoesNotExist;
			}
		}



		private void OnEnable() {
			// Subscribe to the Idle transition trigger from all states (including Idle itself for self-transitions)
			foreach (var logic in this._animationStateHashLookup.Values) {
				if (logic != null) logic.SubscribeToStateTrigger(OnTransitionToIdleRequested);
			}
		}
		private void OnDisable() {
			// Unsubscribe to prevent memory leaks and unintended behavior when disabled
			foreach (var logic in this._animationStateHashLookup.Values) {
				if (logic != null) logic.UnsubscribeFromStateTrigger(OnTransitionToIdleRequested);
			}
		}
		private void OnTransitionToIdleRequested() {
			if (this._stateMachine == null) return;
			var idleLogic = this._animationStateHashLookup[this._idleStateHash];
			this._stateMachine.ChangeState(idleLogic);
			Debug.Log($"[Kope.State] Transition to Idle requested. Switching to Idle state.");
		}








		private bool ValidateDependencies() {
			if (this.ecr == null) {
				Debug.LogError($"[Kope] Registry missing on {gameObject.name}");
				return false;
			}
			var registry = this.ecr.ComponentRegistry;
			if (!registry.TryGetMutatableComponent(out this._movementComponent) ||
				!registry.TryGetMutatableComponent(out this._attackComponent) ||
				!registry.TryGetMutatableComponent(out this._animationComponent)) {
				Debug.LogError($"[Kope] One or more required components are missing in the registry on {gameObject.name}");
				return false;
			}
			return true;
		}

#if UNITY_EDITOR
		private void OnValidate() {
			// Immediate feedback in the Inspector to prevent runtime NULL-refs.
			if (this.defaultIdleStateData.Profile.AnimationState.Source == null) {
				Debug.LogWarning("State Management: Idle state has no EnumAsset source." + GetParentGameObjectHeirarchyMessage());
				return;
			}

			int idleID = this.defaultIdleStateData.Profile.AnimationState.GetSelectedEnumId();

			if (idleID == -1) {
				Debug.LogWarning("State Management: No Idle state selected." + GetParentGameObjectHeirarchyMessage());
				return;
			}

			if (this.animationStateMap.Source != null &&
				this.defaultIdleStateData.Profile.AnimationState.Source != this.animationStateMap.Source) {
				Debug.LogError("[Kope] Asset Mismatch: Idle state and State Map must share the same EnumAsset."
				+ GetParentGameObjectHeirarchyMessage());
			}
		}
#endif

		/// <summary>
		/// Pairs a Data Profile with its Logic (StateSO).
		/// Generic TProfile allows for specific 'Mapped' vs 'Idle' profile variations.
		/// </summary>
		[System.Serializable]
		internal struct AnimationStateData<TProfile> {
			[Tooltip("Configuration data for this specific state.")]
			public TProfile Profile;

			[Tooltip("The Logic implementation (ScriptableObject) for this state.")]
			public EntityStateBaseSO StateSO;
		}

	}
}