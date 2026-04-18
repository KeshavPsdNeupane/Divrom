// PlayerAbilityCaster.cs
using System;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Attack;
using Kope.Core.CompilerServices;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using ServiceLocatorPattern;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Kope.Component.HitBox.Interface;

namespace Kope.Component.Ability {
	[Serializable]
	public class AbilityCastSlot {
		public string displayName;
		public AbilityBase ability;
	}


	[RequireComponent(typeof(TargetingManager))]
	[RequireComponent(typeof(EntityComponentsRegistry))]
	public class PlayerAbilityCaster : InitializableBase {
		[SerializeField] private GameObject baseGameObject;
		[SerializeField] private AbilityCastSlot[] hotbar = Array.Empty<AbilityCastSlot>();
		[SerializeField] private int selectedSlotIndex;
		[SerializeField] private EntityComponentsRegistry ecr;

		private InputManager _inputManager;
		private TargetingManager _targetingManager;
		private IHealthComponent _casterHealth;
		private IAttackComponent _casterAttack;
		private TargetContext _casterContext;
		private EffectContext _effectContext;
		private bool _isSubscribed;

		protected override bool OnInit() {
			this._targetingManager = GetComponent<TargetingManager>();
			this.ecr = GetComponent<EntityComponentsRegistry>();

			if (this._targetingManager == null) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} is missing a TargetingManager.");
				return false;
			}

			if (this.ecr == null || this.ecr.ComponentRegistry == null) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} is missing an EntityComponentsRegistry.");
				return false;
			}

			if (!GlobalServiceLocator.Instance.TryGetService(out this._inputManager)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not resolve InputManager.");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out this._casterAttack, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not find an IAttackComponent.");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out this._casterHealth, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not find an IHealthComponent.");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out IHurtBoxComponent casterHurtBox, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not resolve its own IHurtBoxComponent.");
				return false;
			}

			this._casterContext = new TargetContext(casterHurtBox);
			// just creating once here and reusing since the caster context info won't change and 
			// it's more efficient to reuse than create new ones every cast
			this._effectContext = new EffectContext {
				Caster = this.baseGameObject != null ? this.baseGameObject : this.gameObject,
				CasterAttack = this._casterAttack,
				CasterHealth = this._casterHealth,
				CasterLevel = 0
			};
			SubscribeToInput();
			return true;
		}

		protected override void OnShutdown() {
			UnsubscribeFromInput();
			if (this._targetingManager != null && this._targetingManager.IsTargeting) {
				this._targetingManager.CancelCurrentTargeting();
			}
		}

		private void OnEnable() {
			if (this.IsInitialized) SubscribeToInput();
		}

		private void OnDisable() {
			UnsubscribeFromInput();
			if (this._targetingManager != null && this._targetingManager.IsTargeting) {
				this._targetingManager.CancelCurrentTargeting();
			}
		}

		protected override void OnUpdate() {
			HandleHotbarSelectionInput();
		}

		private void SubscribeToInput() {
			if (this._inputManager == null || this._isSubscribed) return;
			this._inputManager.SubscribeToInputAction(
				PlayerInputActionMap.Player,
				PlayerInputActionKey.Fire.ToString(),
				HandleFire);
			this._isSubscribed = true;
		}

		private void UnsubscribeFromInput() {
			if (this._inputManager == null || !this._isSubscribed) return;
			this._inputManager.UnsubscribeFromInputAction(
				PlayerInputActionMap.Player,
				PlayerInputActionKey.Fire.ToString(),
				HandleFire);
			this._isSubscribed = false;
		}

		private void HandleHotbarSelectionInput() {
			if (Keyboard.current == null || this.hotbar == null || this.hotbar.Length == 0) return;

			for (int i = 0; i < this.hotbar.Length && i < 9; i++) {
				var key = GetSelectionKey(i);
				if (key == null || !key.wasPressedThisFrame) continue;
				SelectSlot(i);
				break;
			}
		}

		private static KeyControl GetSelectionKey(int index) {
			return index switch {
				0 => Keyboard.current.digit1Key,
				1 => Keyboard.current.digit2Key,
				2 => Keyboard.current.digit3Key,
				3 => Keyboard.current.digit4Key,
				4 => Keyboard.current.digit5Key,
				5 => Keyboard.current.digit6Key,
				6 => Keyboard.current.digit7Key,
				7 => Keyboard.current.digit8Key,
				8 => Keyboard.current.digit9Key,
				_ => null
			};
		}

		private void HandleFire(InputAction.CallbackContext context) {
			if (!context.performed) return;
			if (this._targetingManager != null && this._targetingManager.IsTargeting) return;
			CastSelectedAbility();
		}

		public void SelectSlot(int index) {
			if (this.hotbar == null || index < 0 || index >= this.hotbar.Length) return;
			this.selectedSlotIndex = index;
			this._targetingManager?.CancelCurrentTargeting();
		}

		public void CastSelectedAbility() {
			if (this.hotbar == null || this.hotbar.Length == 0) return;
			if (this.selectedSlotIndex < 0 || this.selectedSlotIndex >= this.hotbar.Length) return;

			var slot = this.hotbar[this.selectedSlotIndex];
			if (slot == null || slot.ability == null) return;
			slot.ability.Cast(this._targetingManager, this._casterContext, this._effectContext);
		}
	}
}