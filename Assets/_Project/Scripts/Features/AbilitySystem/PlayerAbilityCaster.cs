using System;
using System.Collections.Generic;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Attack;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using Kope.Core.ServiceLocator;
using UnityEngine;
using UnityEngine.InputSystem;
using Kope.Component.HitBox.Interface;

namespace Kope.Component.Ability {

	public class PlayerAbilityCaster : InitializableBase {
		private const int MAX_HOTBAR_SLOT = 9;
		[Header("Settings")]
		[SerializeField, Range(1, 9)] private int abilityCount = 4;
		[SerializeField] private AbilityBase[] abilityScriptableObjects = Array.Empty<AbilityBase>();
		[SerializeField] private EntityComponentsRegistry ecr;

		private readonly List<AbilityBase> _hotbar = new();
		private readonly List<Action<InputAction.CallbackContext>> _inputCallbacks = new();

		private int _selectedSlotIndex = -1;

		// Dependencies
		private InputManager _inputManager;
		private TargetingManager _targetingManager;
		private ILockablePlayerAttack _attackLock;

		// Contexts
		private TargetContext _casterContext;
		private EffectContext _masterEffectContext;

		private bool _isSubscribed;

		protected override bool OnInit() {
			this.abilityCount = Mathf.Min(this.abilityCount, MAX_HOTBAR_SLOT);

			if (!GlobalServiceLocator.Instance.TryGetService(out this._inputManager)) return false;

			var registry = ecr.ComponentRegistry;
			if (registry == null) return false;

			// Resolve required components from the entity registry
			if (!registry.TryGetReadOnlyComponent(out _targetingManager, false) ||
				!registry.TryGetReadOnlyComponent(out IAttackComponent casterAttack, false) ||
				!registry.TryGetReadOnlyComponent(out IHealthComponent casterHealth, false) ||
				!registry.TryGetReadOnlyComponent(out IHitBoxComponent casterHitBox, false) ||
				!registry.TryGetReadOnlyComponent(out _attackLock, false)) {
				Debug.LogError("PlayerAbilityCaster failed to initialize due to missing components in the" +
				$" EntityComponentsRegistry.{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}

			this._casterContext = new TargetContext(casterHitBox);

			this._masterEffectContext = new EffectContext {
				Dimension = registry.Dimension,
				Caster = registry.EntityTransform.gameObject,
				CasterAttack = casterAttack,
				CasterHealth = casterHealth,
				CasterLevel = 0
			};

			InitializeHotbar();
			SubscribeEvents();
			return true;
		}

		private void OnValidate() {
			this.abilityCount = Mathf.Clamp(this.abilityCount, 1, MAX_HOTBAR_SLOT);

			if (this.abilityScriptableObjects.Length != this.abilityCount) {
				Array.Resize(ref this.abilityScriptableObjects, this.abilityCount);
			}
		}

		private void InitializeHotbar() {
			this._hotbar.Clear();
			int count = Mathf.Min(this.abilityCount, this.abilityScriptableObjects.Length);

			for (int i = 0; i < count; i++) {
				if (this.abilityScriptableObjects[i] == null) {
					this._hotbar.Add(null);
					continue;
				}
				var instance = Instantiate(abilityScriptableObjects[i]);
				instance.InjectAbilityUsedCount(0);
				this._hotbar.Add(instance);
			}
		}

		private void SubscribeEvents() {
			if (this._inputManager == null || this._isSubscribed) return;

			for (int i = 0; i < this._hotbar.Count; i++) {
				int index = i;
				void callback(InputAction.CallbackContext ctx) => OnAbilityKeyPressed(index, ctx);
				this._inputCallbacks.Add(callback);

				this._inputManager.Subscribe(new InputActionSubscriptionLifetime<PlayerInputActionKey>(
					PlayerInputActionCollection.Player,
					PlayerInputActionKey.Ability1 + i,
					callback)
				);
			}

			// Listen for the manager to signal that targeting has ended (Fire, Dodge, or Auto-Finish)
			this._targetingManager.OnTargetingCleanupRequested += this.ClearSelectionAndUnlockInput;
			this._isSubscribed = true;
		}

		private void UnsubscribeEvents() {
			if (this._inputManager == null || !this._isSubscribed) return;

			for (int i = 0; i < this._inputCallbacks.Count; i++) {
				this._inputManager.UnSubscribe(new InputActionSubscriptionLifetime<PlayerInputActionKey>(
					PlayerInputActionCollection.Player,
					PlayerInputActionKey.Ability1 + i,
					this._inputCallbacks[i]));
			}

			if (this._targetingManager != null) {
				this._targetingManager.OnTargetingCleanupRequested -= this.ClearSelectionAndUnlockInput;
			}

			this._inputCallbacks.Clear();
			this._isSubscribed = false;
		}

		private void OnAbilityKeyPressed(int index, InputAction.CallbackContext context) {
			if (!context.performed) return;

			// If already targeting a different ability, ignore new input
			if (this._targetingManager.IsTargeting && this._selectedSlotIndex != index) return;

			HandleAbilityActivation(index);
		}

		/// <summary>
		/// Selects a slot and initiates the ability's casting/targeting sequence.
		/// </summary>
		public void HandleAbilityActivation(int index) {
			if (index < 0 || index >= this._hotbar.Count) return;

			// If re-pressing the same active ability, we do nothing (Dodge is for cancelling)
			if (this._selectedSlotIndex == index && this._targetingManager.IsTargeting) return;

			var ability = this._hotbar[index];
			if (ability == null) {
				ClearSelectionAndUnlockInput();
				return;
			}

			// Lock basic attacks and mark selection
			this._attackLock.SetEventLock(true);
			this._selectedSlotIndex = index;

			// Execute ability cast
			ability.Cast(this._targetingManager, this._casterContext, this._masterEffectContext);

			// If the ability finished instantly (e.g. Self-Cast), clean up immediately.
			// Otherwise, we stay locked until TargetingManager triggers OnTargetingCleanupRequested.
			// here instant cast abilities are those that don't require any additional input after pressing the key,
			// so if the manager is not in targeting mode after casting, we can assume the ability resolved immediately
			// and we can clean up right away.
			if (!this._targetingManager.IsTargeting || ability.IsInstantCast) {
				ClearSelectionAndUnlockInput();
			}
		}

		private void ClearSelectionAndUnlockInput() {
			this._selectedSlotIndex = -1;
			this._attackLock.SetEventLock(false);
		}

		protected override void OnShutdown() => UnsubscribeEvents();
		private void OnEnable() { if (IsInitialized) SubscribeEvents(); }
		private void OnDisable() { if (IsInitialized) UnsubscribeEvents(); }
	}
}