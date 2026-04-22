using System;
using System.Collections.Generic;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Attack;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using ServiceLocatorPattern;
using UnityEngine;
using UnityEngine.InputSystem;
using Kope.Component.HitBox.Interface;

namespace Kope.Component.Ability {

	public class PlayerAbilityCaster : InitializableBase {

		const int MAX_HOT_BAR_SLOT = 9;

		[SerializeField] private int abilityCount = 4;

		/// <summary>
		/// Editor-only source. Runtime uses _hotbar.
		/// </summary>
		[SerializeField] private AbilityBase[] abilityScriptableObjects = Array.Empty<AbilityBase>();

		[SerializeField] private EntityComponentsRegistry ecr;

		private readonly List<AbilityBase> _hotbar = new();
		private readonly List<Action<InputAction.CallbackContext>> _abilityInputCallbacks = new();

		private int _selectedSlotIndex = -1;

		private InputManager _inputManager;
		private TargetingManager _targetingManager;
		private IHealthComponent _casterHealth;
		private IAttackComponent _casterAttack;

		private TargetContext _casterContext;
		private EffectContext _effectContext;

		private bool _isSubscribed;

		protected override bool OnInit() {

			// enforce hard limit
			this.abilityCount = Mathf.Min(this.abilityCount, MAX_HOT_BAR_SLOT);

			if (!GlobalServiceLocator.Instance.TryGetService(out this._inputManager)) {
				Debug.LogError($"PlayerAbilityCaster on {gameObject.name} could not resolve InputManager.");
				return false;
			}

			if (this.ecr == null || this.ecr.ComponentRegistry == null) {
				Debug.LogError($"PlayerAbilityCaster on {gameObject.name} missing EntityComponentsRegistry.");
				return false;
			}

			var registry = this.ecr.ComponentRegistry;
			var baseGO = registry.EntityTransform.gameObject;

			if (!registry.TryGetReadOnlyComponent(out this._targetingManager, false) ||
				!registry.TryGetReadOnlyComponent(out this._casterAttack, false) ||
				!registry.TryGetReadOnlyComponent(out this._casterHealth, false) ||
				!registry.TryGetReadOnlyComponent(out IHitBoxComponent casterHurtBox, false)) {

				Debug.LogError($"PlayerAbilityCaster on {gameObject.name} missing required components.{GetParentGameObjectHeirarchyMessage()}");
				return false;
			}

			this._casterContext = new TargetContext(casterHurtBox);

			this._effectContext = new EffectContext {
				Caster = baseGO,
				CasterAttack = this._casterAttack,
				CasterHealth = this._casterHealth,
				CasterLevel = 0
			};

			BuildHotbar();
			SubscribeToInput();

			return true;
		}

		private void BuildHotbar() {
			this._hotbar.Clear();

			int count = Mathf.Min(this.abilityCount, this.abilityScriptableObjects.Length);

			for (int i = 0; i < count; i++) {
				var so = this.abilityScriptableObjects[i];

				if (so == null) {
					this._hotbar.Add(null);
					continue;
				}

				var ability = Instantiate(so);
				ability.InjectAbilityUsedCount(0);
				this._hotbar.Add(ability);
			}
		}

		void OnValidate() {
			if (this.abilityScriptableObjects == null) return;

			this.abilityCount = Mathf.Min(this.abilityCount, MAX_HOT_BAR_SLOT);

			if (this.abilityScriptableObjects.Length != this.abilityCount) {
				Array.Resize(ref this.abilityScriptableObjects, this.abilityCount);
			}
		}

		protected override void OnShutdown() {
			if (!this.IsInitialized) return;
			UnsubscribeFromInput();
			this._targetingManager.CancelCurrentTargeting();
		}

		private void OnEnable() {
			if (this.IsInitialized) SubscribeToInput();
		}

		private void OnDisable() {
			if (!this.IsInitialized) return;
			UnsubscribeFromInput();
			this._targetingManager.CancelCurrentTargeting();
		}

		private void SubscribeToInput() {
			if (this._inputManager == null || this._isSubscribed) return;

			this._inputManager.Subscribe(
				new InputActionSubscriptionLifetime<PlayerInputActionKey>(
					PlayerInputActionCollection.Player,
					PlayerInputActionKey.Fire,
					HandleFire));


			this._abilityInputCallbacks.Clear();

			int usableSlots = Mathf.Min(this._hotbar.Count, MAX_HOT_BAR_SLOT);

			for (int i = 0; i < usableSlots; i++) {
				int index = i;

				void callback(InputAction.CallbackContext ctx) => AbilityCallback(index, ctx);

				this._abilityInputCallbacks.Add(callback);

				this._inputManager.Subscribe(
					new InputActionSubscriptionLifetime<PlayerInputActionKey>(
						PlayerInputActionCollection.Player,
						PlayerInputActionKey.Ability1 + i,
						callback)
					);
			}

			this._isSubscribed = true;
		}

		private void UnsubscribeFromInput() {
			if (this._inputManager == null || !this._isSubscribed) return;

			this._inputManager.UnSubscribe(
				new InputActionSubscriptionLifetime<PlayerInputActionKey>(
					PlayerInputActionCollection.Player,
					PlayerInputActionKey.Fire,
					HandleFire));

			for (int i = 0; i < this._abilityInputCallbacks.Count; i++) {
				this._inputManager.UnSubscribe(
					new InputActionSubscriptionLifetime<PlayerInputActionKey>(
						PlayerInputActionCollection.Player,
						PlayerInputActionKey.Ability1 + i,
						this._abilityInputCallbacks[i]));
			}

			this._abilityInputCallbacks.Clear();
			this._isSubscribed = false;
		}

		private void AbilityCallback(int index, InputAction.CallbackContext context) {
			if (!context.performed) return;

			if (this._targetingManager.IsTargeting &&
				this._selectedSlotIndex != index) return;

			SelectSlot(index);
		}

		// will remove and put this in the ability class itself later, but for now it's here
		// for testing purposes 
		private void HandleFire(InputAction.CallbackContext context) {
			if (!context.performed || !this.IsInitialized) return;

			if (this._targetingManager.IsTargeting) return;

			if (this._selectedSlotIndex >= 0) {
				CastSelectedAbility();
				return;
			}

			if (!this._casterAttack.AlreadySubscribedToAttackEvent) {
				this._casterAttack.PerformAttack();
			}
		}

		public void SelectSlot(int index) {
			if (index < 0 || index >= this._hotbar.Count) return;
			bool isTargetingManagerAvailable = this._targetingManager != null;

			if (this._selectedSlotIndex == index) {
				this._selectedSlotIndex = -1;
				if (isTargetingManagerAvailable) {
					this._targetingManager.CancelCurrentTargeting();
				}
				return;
			}

			if (isTargetingManagerAvailable && this._targetingManager.IsTargeting) return;

			this._selectedSlotIndex = index;

			var ability = this._hotbar[index];
			if (ability == null) return;

			if (ability.IsAutoCast) {
				CastSelectedAbility();
			}
		}

		public void CastSelectedAbility() {
			if (this._selectedSlotIndex < 0 ||
				this._selectedSlotIndex >= this._hotbar.Count) return;

			var ability = this._hotbar[this._selectedSlotIndex];
			if (ability == null) return;

			ability.Cast(this._targetingManager, this._casterContext, this._effectContext);

			this._selectedSlotIndex = -1;
		}
	}
}