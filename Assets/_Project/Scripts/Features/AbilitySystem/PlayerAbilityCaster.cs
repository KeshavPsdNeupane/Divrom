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
using Kope.Component.Movement;
using Kope.Component.HitBox.Interface;

namespace Kope.Component.Ability {

	[Serializable]
	public class AbilityCastSlot {
		public string displayName;
		public AbilityBase ability;
		[SerializeReference, Core.Attributes.SubclassSelector]
		public ITargetingFactory targetingFactory;
	}

	[RequireComponent(typeof(TargetingManager))]
	[RequireComponent(typeof(EntityComponentsRegistry))]
	public class PlayerAbilityCaster : InitializableBase {
		[SerializeField] private AbilityCastSlot[] hotbar = Array.Empty<AbilityCastSlot>();
		[SerializeField] private int selectedSlotIndex;

		[SerializeField] private EntityComponentsRegistry ecr;
		private InputManager _inputManager;
		private TargetingManager _targetingManager;
		private IHealthComponent _casterHealth;
		private IAttackComponent _casterAttack;

		private TargetContext _casterContext;
		private bool isSubscribed;

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
				Debug.LogError($"PlayerAbilityCaster on {gameObject.name} could not find an IAttackComponent " +
				"in the EntityComponentsRegistry.");
				return false;
			}
			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out this._casterHealth, false)) {
				Debug.LogError($"PlayerAbilityCaster on {gameObject.name} could not find an IHealthComponent" +
				" in the EntityComponentsRegistry.");
				return false;
			}

			// we are passing the Ihurtbox component as the "combat target" in the caster context, s
			// since hurtbox can be placed on any entity that should be hittable, but combatable is a component that will
			// be only on entity that can actually engage in combat, and there might be some entities in 
			// the game that have hurtboxes but aren't combatable (like a training dummy or something), 
			// so it makes more sense to use the hurtbox as the reference point for the caster's combat-related properties,
			// since if an entity has a combatable component it should also have a hurtbox, but not necessarily the 
			// other way around. so with this distinction we can have a entity like a Pot that give currency but it doesnt need to
			//  have fulldependencies of a combatable entity, but it can still be targeted by abilities
			//  that look for a hurtbox,for that instance the hurtbox will redirect the hit with event to pot other component 
			// that will handle pot specific logic like giving currency and playing break animation, 
			// without needing to have a full combatable component with health, attack, etc.
			//  that would be irrelevant for a pot.

			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out IHurtBoxComponent _casterHitBox, false)) {
				MyLogger.Error($"PlayerAbilityCaster on {gameObject.name} could not resolve its own IHurtBoxComponent.");
				return false;
			}

			// just store the relevant components in the caster context for use
			// by targeting strategies and effects, since some abilities might want to 
			// reference the caster's stats or apply effects to the caster as well.
			this._casterContext = new TargetContext(_casterHitBox);
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
			if (this.IsInitialized) {
				SubscribeToInput();
			}
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
			if (this._inputManager == null || this.isSubscribed) return;
			this._inputManager.SubscribeToInputAction(PlayerInputActionMap.Player, PlayerInputActionKey.Fire.ToString(), HandleFire);
			this.isSubscribed = true;
		}

		private void UnsubscribeFromInput() {
			if (this._inputManager == null || !this.isSubscribed) return;
			this._inputManager.UnsubscribeFromInputAction(PlayerInputActionMap.Player, PlayerInputActionKey.Fire.ToString(), HandleFire);
			this.isSubscribed = false;
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

			if (!TryBuildEffectContext(out var effectContext)) return;

			// The prebuilt effect-style infra works well here: the caster only asks for a factory,
			// gets a fresh runtime strategy, and stays decoupled from the actual targeting behavior.
			var targetingStrategy = slot.targetingFactory?.Create() ?? new SelfTargetingStrategy();
			targetingStrategy.Start(slot.ability, this._targetingManager, this._casterContext, effectContext);
		}

		private bool TryBuildEffectContext(out EffectContext context) {
			context = default;
			if (this._casterContext == null) return false;

			context.Caster = this.gameObject;
			context.CasterAttack = this._casterAttack;
			context.CasterHealth = this._casterHealth;
			// init the caster level from the Level Component if implemented, 
			// otherwise default to 0, since some abilities might want to use 
			// the caster's level for scaling purposes, and it's better to have a
			// default value than to have it be uninitialized.
			context.CasterLevel = 0;
			// the hit point will be set by the targeting strategy if needed,
			// since for some abilities (like self-targeted ones) it might not be relevant 
			// or available at the time of casting, so we can just leave it as default and let
			// the targeting strategy handle it.
			return true;
		}
	}
}