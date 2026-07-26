using System.Collections.Generic;
using Kope.Component;
using Kope.Component.Attack;
using Kope.Component.Movement;
using Kope.Core.Attribute;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.LifeTimeManagement;
using Kope.Core.Type.EnumAsset;
using Kope.EntityComponentSystem;
using UnityEngine;

namespace Kope.Actor.States {

	/// <summary>
	/// Default fallback logic for purely data-driven states that do not require custom C# logic files.
	/// </summary>


	public interface IEntityStateManagement {
		/// <summary>
		/// Forces a transition to a specific state by ID. 
		/// </summary>
		/// <remarks>
		/// <para><b>External Use:</b> Universally available for external systems to dictate entity behavior.</para>
		/// <para><b>Internal Use:</b> Reserved exclusively for the <b>Idle State</b> to route to specific behaviors. 
		/// All other states must use <see cref="TransitionToIdle"/> to relinquish control rather than self-selecting
		/// a successor.</para>
		/// </remarks>
		/// <param name="enumId">The unique identifier of the target state.</param>
		/// <param name="handleFallbackInternally">If true, automatically returns to Idle on a failed transition attempt.</param>
		/// <returns>The result of the transition attempt.</returns>
		StateChangeResult ChangeState(int enumId, bool handleFallbackInternally = false,
		 System.Action<EntityStateBaseSO> preStateChange = null,
		 System.Action<EntityStateBaseSO> postStateChange = null
		);

		/// <summary>
		/// The standard internal exit path for active states to return the entity to a neutral baseline.
		/// </summary>
		void TransitionToIdle();
	}

	/// <summary>
	/// Bridges enum-keyed animation states with their runtime logic (EntityStateBaseSO)
	/// and data profiles. Populates an O(1) enumId-lookup table on init.
	/// </summary>
	public class EntityStateManagement : ComponentBase, IEntityStateManagement,
	IUpdatable, IFixedUpdatable {

		[Header("Core")]
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private bool loadOnStart = true;

		[Header("States")]
		[Message("No need to worry about shared logic SOs here  each entry is instantiated on init, so they can be reused as templates without risk of shared state.")]
		[SerializeField] private AnimationStateData<AnimationStateProfile> defaultIdleStateData;
		[SerializeField] private EnumTable<AnimationStateData<AnimationStateMappedProfile>> animationStateMap;

		// Cached component refs resolved from the ECR on init.
		private IMovementComponent _movementComponent;
		private IAttackComponent _attackComponent;
		private IAnimationComponent _animationComponent;

		private int _idleEnumId;
		private readonly EntityStateMachine _stateMachine = new();

		// Primary lookup: enumId → runtime logic SO instance. Guaranteed non-null if TryGetValue is true.
		private readonly Dictionary<int, EntityStateBaseSO> _stateLookUp = new();

		public bool CanStateAcceptExternalCommand => this._stateMachine.CurrentState == null
			|| this._stateMachine.CurrentState.CanStateAcceptExternalCommand;

		private void Awake() { if (this.loadOnStart) Init(); }

		protected override bool OnInit() {
			if (!ValidateDependencies()) return false;

			// --- 1. Register Idle State ---
			var idleData = this.defaultIdleStateData.Profile.ToData();
			int idleID = this.defaultIdleStateData.Profile.StatePicker.GetSelectedEnumId();
			this._idleEnumId = idleID;

			EntityStateBaseSO idleLogic;
			if (this.defaultIdleStateData.StateSO != null) {
				idleLogic = Instantiate(this.defaultIdleStateData.StateSO);
			} else {
				idleLogic = ScriptableObject.CreateInstance<DefaultAnimationStateSO>();
				idleLogic.name = "[Fallback] Default_Idle_Logic";
				Debug.LogWarning("[Kope.State] No custom logic for Idle — assigned automatic DefaultAnimationStateSO baseline.");
			}

			idleLogic.Init(this, this._movementComponent, this._animationComponent, idleData);
			this._stateMachine.Initialize(idleLogic);
			this._stateLookUp[this._idleEnumId] = idleLogic;

			// --- 2. Register Mapped States (Using Fallback System for Data-Driven Configurations) ---
			foreach (var (instance, profile) in this.animationStateMap.BindLookup) {
				if (instance.InternalValue == idleID) continue;

				var profileData = profile.Profile.ToData(instance.Alias);

				EntityStateBaseSO profileLogic;

				if (profile.StateSO != null) {
					// Explicit custom behavior script assigned
					profileLogic = Instantiate(profile.StateSO);
				} else {
					// Implicit fallback for purely data-driven configurations
					profileLogic = ScriptableObject.CreateInstance<DefaultAnimationStateSO>();
					profileLogic.name = $"[Fallback] {instance.Alias}_Logic";
				}

				profileLogic.Init(this, this._movementComponent, this._animationComponent, profileData);

				if (!this._stateLookUp.TryAdd(instance.InternalValue, profileLogic)) {
					Debug.LogWarning($"[Kope.State] EnumId collision on '{instance.Alias}' (ID: {instance.InternalValue}) — check for duplicate enum values.", this.gameObject);
					Destroy(profileLogic); // Clear clone out of memory
					continue;
				}
			}

			return true;
		}

		void OnEnable() {
			if (!this.IsInitialized) return;
			this._attackComponent.OnAttackPerformed1 -= PerformAttackAnimation;
			this._attackComponent.OnAttackPerformed1 += PerformAttackAnimation;
		}

		void OnDisable() {
			if (!this.IsInitialized) return;
			this._attackComponent.OnAttackPerformed1 -= PerformAttackAnimation;
		}

		public void OnUpdate() {
			var processResult = this._stateMachine.ProcessStateChanges();
			if (processResult == StateChangeResult.Failed) {
				Debug.LogError($"[Kope.State] Critical failure: {this._stateMachine.CurrentState.name} failed Enter logic.");
			}
			if (this._stateMachine.CurrentState != null) {
				this._stateMachine.CurrentState.TickUpdate();
			}
		}

		public void OnFixedUpdate() {
			if (this._stateMachine.CurrentState != null)
				this._stateMachine.CurrentState.TickFixedUpdate();
		}

		public StateChangeResult ChangeState(
		int enumId,
		bool handleFallbackInternally = false,
		System.Action<EntityStateBaseSO> preStateChange = null,
		System.Action<EntityStateBaseSO> postStateChange = null) {
			// Single lookup pass
			if (!this._stateLookUp.TryGetValue(enumId, out var logic)) {
				if (handleFallbackInternally) return ScheduleTransitionToIdleInternal();
				return StateChangeResult.Error_NotFound;
			}

			// 1. Pre-State Change: Mutate values (like setting speed) before the state machine locks it in
			preStateChange?.Invoke(logic);

			var result = this._stateMachine.ScheduleTransition(logic);

			// 2. Post-State Change: Run side effects after the transition is successfully scheduled
			if (result == StateChangeResult.Success || result == StateChangeResult.Internal_Fallback) {
				postStateChange?.Invoke(logic);
			}

			// Handle structural failures if necessary
			if (handleFallbackInternally && result != StateChangeResult.Success && result != StateChangeResult.Internal_Fallback) {
				string stateName = logic.ProfileData.Name ?? $"EnumId {enumId}";
				Debug.LogWarning($"[Kope.State] Transition to '{stateName}' failed with result: {result}. Self-correcting to Idle baseline.");
				return ScheduleTransitionToIdleInternal();
			}

			return result;
		}

		private void PerformAttackAnimation(WeaponData weaponData1) {
			// Single point of entry. C# lambda caching avoids allocation here if it targets parameters directly.
			_ = ChangeState(
				enumId: weaponData1.AnimationID,
				handleFallbackInternally: true,
				preStateChange: (so) => so.ChangeAnimationSpeed(spd: weaponData1.AttackSpeed)
			);
		}

		public void TransitionToIdle() {
			ScheduleTransitionToIdleInternal();
		}

		private StateChangeResult ScheduleTransitionToIdleInternal() {
			if (this._stateLookUp.TryGetValue(this._idleEnumId, out var idle)) {
				return this._stateMachine.ScheduleTransition(idle, true);
			}
			Debug.LogError("[Kope.State] Critical: Idle state missing from lookup table.");
			return StateChangeResult.Error_LogicMissing;
		}

		private bool ValidateDependencies() {
			if (this.ecr == null) {
				Debug.LogError($"[Kope] Registry missing on {this.gameObject.name}" + this.HieararchyPath);
				return false;
			}
			var reg = this.ecr.ComponentRegistry;
			if (!reg.TryGetMutable(out this._movementComponent) ||
				!reg.TryGetMutable(out this._attackComponent) ||
				!reg.TryGetMutable(out this._animationComponent)) {
				return false;
			}
			return true;
		}

#if UNITY_EDITOR
		private void OnValidate() {
			if (this.defaultIdleStateData.Profile == null) return;
			EnumPicker idleStatePicker = this.defaultIdleStateData.Profile.StatePicker;
			if (idleStatePicker == null) return;

			_ = idleStatePicker.ValidateTheInternal(this);
			if (this.animationStateMap != null) {
				_ = this.animationStateMap.ValidateTheInternal(idleStatePicker.Source, this);
			}
		}
#endif

		[System.Serializable]
		internal struct AnimationStateData<TProfile> {
			public TProfile Profile;
			[Tooltip("Optional custom logic SO. If null, the system will auto-assign a " +
			"default fallback that simply applies the animation and speed without any additional behavior.")]
			public EntityStateBaseSO StateSO;
		}
	}
}