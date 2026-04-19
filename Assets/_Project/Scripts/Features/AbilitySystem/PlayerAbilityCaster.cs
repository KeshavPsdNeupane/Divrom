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
using System.Collections.Generic;

namespace Kope.Component.Ability {


	public class PlayerAbilityCaster : InitializableBase {
		[SerializeField] private int abilityCount = 4;
		/// <summary>
		/// Never use this use _hotbar instead, this is just for serialization and editor assignment.
		///  The hotbar array is resized to match the ability count and populated with the assigned abilities 
		/// from this array on init.
		/// </summaty>
		[SerializeField] private AbilityBase[] abilityScriptableObjects = Array.Empty<AbilityBase>();
		[SerializeField] private EntityComponentsRegistry ecr;

		// -1 means no slot selected, valid indices are 0 to hotbar.Length - 1
		private readonly List<AbilityBase> _hotbar = new();
		private int _selectedSlotIndex = -1;
		private GameObject _baseGameObject;
		private InputManager _inputManager;
		private TargetingManager _targetingManager;
		private IHealthComponent _casterHealth;
		private IAttackComponent _casterAttack;
		private TargetContext _casterContext;
		private EffectContext _effectContext;
		private bool _isSubscribed;

		protected override bool OnInit() {

			if (!GlobalServiceLocator.Instance.TryGetService(out this._inputManager)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not resolve InputManager.");
				return false;
			}

			if (this.ecr == null || this.ecr.ComponentRegistry == null) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} is missing an EntityComponentsRegistry.");
				return false;
			}
			var registry = this.ecr.ComponentRegistry;
			this._baseGameObject = registry.EntityTransform.gameObject;

			if (!registry.TryGetReadOnlyComponent(out this._targetingManager, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} is missing a TargetingManager.");
				return false;
			}

			if (!registry.TryGetReadOnlyComponent(out this._casterAttack, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not find an IAttackComponent.");
				return false;
			}

			if (!registry.TryGetReadOnlyComponent(out this._casterHealth, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not find an IHealthComponent.");
				return false;
			}

			if (!registry.TryGetReadOnlyComponent(out IHitBoxComponent casterHurtBox, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not resolve its own IHurtBoxComponent.");
				return false;
			}

			this._casterContext = new TargetContext(casterHurtBox);
			// just creating once here and reusing since the caster context info won't change and 
			// it's more efficient to reuse than create new ones every cast
			this._effectContext = new EffectContext {
				Caster = this._baseGameObject != null ? this._baseGameObject : this.gameObject,
				CasterAttack = this._casterAttack,
				CasterHealth = this._casterHealth,
				CasterLevel = 0
			};

			for (int i = 0; i < this.abilityScriptableObjects.Length; i++) {
				if (this.abilityScriptableObjects[i] != null) {
					var ability = Instantiate(this.abilityScriptableObjects[i]);
					ability.InjectAbilityUsedCount(0);
					this._hotbar.Add(ability);
				} else {
					this._hotbar.Add(null);
				}
			}
			SubscribeToInput();
			return true;
		}
		void OnValidate() {
			if (this.abilityScriptableObjects == null || this.abilityScriptableObjects.Length == 0) return;
			// forcing the hotbar array to always match the ability count for simplicity,
			if (this.abilityScriptableObjects.Length != this.abilityCount) {
				Array.Resize(ref this.abilityScriptableObjects, this.abilityCount);
			}

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
			if (Keyboard.current == null || this._hotbar == null || this._hotbar.Count == 0) return;

			for (int i = 0; i < this._hotbar.Count && i < 9; i++) {
				var key = GetSelectionKey(i + 1);
				if (key == null || !key.wasPressedThisFrame) continue;
				SelectSlot(i);
				break;
			}
		}

		private static KeyControl GetSelectionKey(int index) {
			// later will make  this to be configurable via the input system,
			//  but for now just hardcoding to number keys 1-5 
			// (0 is usually reserved for something else like "no selection")
			// 
			return index switch {
				1 => Keyboard.current.digit1Key,
				2 => Keyboard.current.digit2Key,
				3 => Keyboard.current.digit3Key,
				4 => Keyboard.current.digit4Key,
				_ => null
			};
		}

		private void HandleFire(InputAction.CallbackContext context) {
			if (!context.performed) return;
			if (this._targetingManager != null && this._targetingManager.IsTargeting) return;
			CastSelectedAbility();
		}

		public void SelectSlot(int index) {
			if (this._hotbar == null || index < 0 || index >= this._hotbar.Count) return;
			this._selectedSlotIndex = index;
			if (this._targetingManager != null && this._targetingManager.IsTargeting)
				this._targetingManager.CancelCurrentTargeting();
		}

		public void CastSelectedAbility() {
			if (this._hotbar == null || this._hotbar.Count == 0) return;
			if (this._selectedSlotIndex < 0 || this._selectedSlotIndex >= this._hotbar.Count) return;
			var slot = this._hotbar[this._selectedSlotIndex];
			if (slot == null) return;

			// here no need to add the ability count in context,
			// ability themself add that internall on the context
			slot.Cast(this._targetingManager, this._casterContext, this._effectContext);

			// after casting, we reset the selected slot index to prevent accidental multiple 
			// casts and to require the player to intentionally select an ability 
			// slot for each cast, which can help prevent miscasts in the heat of combat.
			this._selectedSlotIndex = -1;
		}
	}
}