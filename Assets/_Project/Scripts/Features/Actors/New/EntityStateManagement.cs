using System.Collections.Generic;
using Kope.Component;
using Kope.Component.Attack;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Actor.New {
	public interface IEntityStateManagement {
		/// <summary>
		/// Forces a transition to a specific state by ID. 
		/// </summary>
		/// <remarks>
		/// <para><b>External Use:</b> Universally available for external systems to dictate entity behavior.</para>
		/// <para><b>Internal Use:</b> Reserved exclusively for the <b>Idle State</b> to route to specific behaviors. 
		/// All other states must use <see cref="TransitionToIdle"/> to relinquish control rather than self-selecting a successor.</para>
		/// </remarks>
		/// <param name="enumId">The unique identifier of the target state.</param>
		/// <returns>The result of the transition attempt.</returns>
		StateChangeResult ChangeState(int enumId, bool handleFallbackInternally = false);

		/// <summary>
		/// The standard internal exit path for active states to return the entity to a neutral baseline.
		/// </summary>
		/// <remarks>
		/// This method allows states to terminate without needing knowledge of the state lookup table.
		/// Once the transition to Idle is complete, the Idle state takes over as the central router.
		/// </remarks>
		void TransitionToIdle();
	}
	/// <summary>
	/// Bridges enum-keyed animation states with their runtime logic (EntityStateBaseSO)
	/// and data profiles. Populates an O(1) enumId-lookup table on init.
	/// </summary>
	public class EntityStateManagement : InitializableBase, IEntityStateManagement {

		[Header("Core")]
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private bool loadOnStart = true;

		[Header("States")]
		[Header("No need to worry about shared logic SOs here \n each entry is instantiated on init, \nso they can be reused as templates without risk of shared state.")]
		// Idle is kept separate — it's the universal fallback, always registered first.
		[SerializeField] private AnimationStateData<AnimationStateProfile> defaultIdleStateData;
		// All non-idle states. Idle entries here are silently skipped.
		[SerializeField] private EnumTable<AnimationStateData<AnimationStateMappedProfile>> animationStateMap;

		// Cached component refs resolved from the ECR on init.
		private IMovementComponent _movementComponent;
		private IAttackComponent _attackComponent;
		private IAnimationComponentNew _animationComponent;

		// EnumId of the idle state — stored separately so TransitionToIdle never needs a lookup key from outside.
		private int _idleEnumId;
		private readonly EntityStateMachine _stateMachine = new();

		// Primary lookup: enumId → logic SO.
		// Null values are valid — purely data-driven states have no logic.
		private readonly Dictionary<int, EntityStateBaseSO> _stateLookUp = new();

		private void Awake() { if (this.loadOnStart) Init(); }

		protected override bool OnInit() {
			if (!ValidateDependencies()) return false;


			// --- Register idle first so it's always available as a fallback ---
			var idleData = this.defaultIdleStateData.Profile.ToData();
			int idleID = this.defaultIdleStateData.Profile.StatePicker.GetSelectedEnumId();
			this._idleEnumId = idleID;

			EntityStateBaseSO idleLogic = null;
			if (this.defaultIdleStateData.StateSO != null) {
				idleLogic = Instantiate(this.defaultIdleStateData.StateSO);
				idleLogic.Init(this, idleData, this._movementComponent, this._animationComponent);
				this._stateMachine.Initialize(idleLogic);

			} else {
				Debug.LogWarning("[Kope.State] No logic for Idle — entity may behave unexpectedly.");
			}
			this._stateLookUp[this._idleEnumId] = idleLogic;
			Debug.Log($"[Kope.State] Registered Baseline: Idle (EnumId: {idleID})");

			// --- Register all mapped states, skipping any that duplicate idle ---
			foreach (var (instance, profile) in this.animationStateMap.BindLookup) {
				if (instance.InternalValue == idleID) continue;

				var profileData = profile.Profile.ToData(instance.Alias);
				EntityStateBaseSO profileLogic = null;
				if (profile.StateSO != null) {
					profileLogic = Instantiate(profile.StateSO);
					profileLogic.Init(this, profileData, this._movementComponent, this._animationComponent);
				}

				// Collisions here mean two enum entries share the same InternalValue — check the EnumAsset.
				if (!_stateLookUp.TryAdd(instance.InternalValue, profileLogic)) {
					Debug.LogWarning($"[Kope.State] EnumId collision on '{instance.Alias}' — check for duplicate enum values.");
					continue;
				}
				Debug.Log($"[Kope.State] Registered: {instance.Alias} (EnumId: {instance.InternalValue}) | {profileData}");
			}
			return true;
		}

		protected override void OnUpdate() {
			// 1. Flush any pending transitions at the start of the frame.
			// This captures the EnterState() result (Success, Busy, etc.)
			var processResult = this._stateMachine.ProcessStateChanges();
			// Optional: Handle critical failures if a state fails to Enter() even after validation
			if (processResult == StateChangeResult.Failed) {
				Debug.LogError($"[Kope.State] Critical failure: {this._stateMachine.CurrentState.name} failed Enter logic.");
			}
			if (this._stateMachine.CurrentState != null) {
				this._stateMachine.CurrentState.TickUpdate();
			}
		}
		protected override void OnFixedUpdate() {
			if (this._stateMachine.CurrentState != null)
				this._stateMachine.CurrentState.TickFixedUpdate();
		}

		public StateChangeResult ChangeState(int enumId, bool handleFallbackInternally = false) {
			// Attempt to find and schedule the transition.
			var result = this._stateLookUp.TryGetValue(enumId, out var logic)
				? this._stateMachine.ScheduleTransition(logic)
				: StateChangeResult.Error_NotFound;

			if (handleFallbackInternally && result != StateChangeResult.Success && result != StateChangeResult.Internal_Fallback) {
				var stateName = logic.ProfileData.Name ?? $"EnumId {enumId}";
				Debug.LogWarning($"[Kope.State] Transition to '{stateName}' failed with result: {result}. " +
								 "Self-correcting to Idle baseline. \n" +
								 "TIP: If this is unintended behavior, check the Inspector definition for this state. " +
								 "Ensure 'IsLooping' is correct and look if any ProfileData or Hash values are missing/incorrect.");

				return ScheduleTransitionToIdleInternal();
			}

			return result;
		}

		/// <summary>
		/// Public entry point to force the entity back to its baseline.
		/// </summary>
		public void TransitionToIdle() {
			this.ScheduleTransitionToIdleInternal();
		}

		/// <summary>
		/// Internal helper to schedule the Idle fallback with the priority flag.
		/// </summary>
		private StateChangeResult ScheduleTransitionToIdleInternal() {
			if (this._stateLookUp.TryGetValue(this._idleEnumId, out var idle)) {
				// 'true' ensures we bypass feasibility/lock checks.
				return this._stateMachine.ScheduleTransition(idle, true);
			}
			Debug.LogError("[Kope.State] Critical: Idle state missing from lookup table.");
			return StateChangeResult.Error_LogicMissing;
		}
		private bool ValidateDependencies() {
			if (ecr == null) {
				Debug.LogError($"[Kope] Registry missing on {gameObject.name}" + GetParentGameObjectHeirarchyMessage());
				return false;
			}
			var reg = ecr.ComponentRegistry;
			if (!reg.TryGetMutatableComponent(out this._movementComponent) ||
				!reg.TryGetMutatableComponent(out this._attackComponent) ||
				!reg.TryGetMutatableComponent(out this._animationComponent)) {
				Debug.LogError($"[Kope] Missing required component(s) in registry on {gameObject.name}" + GetParentGameObjectHeirarchyMessage());
				return false;
			}
			return true;
		}

#if UNITY_EDITOR
		private void OnValidate() {
			if (this.defaultIdleStateData.Profile.StatePicker.Source == null) {
				Debug.LogWarning("State Management: Idle has no EnumAsset source." + GetParentGameObjectHeirarchyMessage());
				return;
			}
			if (this.defaultIdleStateData.Profile.StatePicker.GetSelectedEnumId() == -1) {
				Debug.LogWarning("State Management: No Idle state selected." + GetParentGameObjectHeirarchyMessage());
				return;
			}
			// Both the idle slot and the map must reference the same EnumAsset or enumId lookups will diverge.
			if (this.animationStateMap.Source != null &&
				this.defaultIdleStateData.Profile.StatePicker.Source != this.animationStateMap.Source) {
				Debug.LogError("[Kope] Asset mismatch: Idle and State Map must share the same EnumAsset."
					+ GetParentGameObjectHeirarchyMessage());
			}
		}
#endif

		/// <summary>
		/// Pairs a state's data profile with its optional logic SO.
		/// Logic can be null for purely data-driven (no-code) states.
		/// TProfile allows idle and mapped states to carry different profile shapes.
		/// </summary>
		[System.Serializable]
		internal struct AnimationStateData<TProfile> {
			public TProfile Profile;
			public EntityStateBaseSO StateSO;
		}
	}
}