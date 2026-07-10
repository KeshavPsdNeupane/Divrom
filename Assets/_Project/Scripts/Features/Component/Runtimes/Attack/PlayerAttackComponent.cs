using UnityEngine.InputSystem;
using Kope.Core.ServiceLocator;
using UnityEngine;

namespace Kope.Component.Attack {
	public interface ILockablePlayerAttack {
		bool IsEventLocked { get; }
		void SetEventLock(bool isLocked);
	}

	public class PlayerAttackComponent : AttackComponentBase, ILockablePlayerAttack {
		private InputManager _inputManager;
		private bool _isEventSubscribed;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _fireSubscription;


		public bool IsEventLocked { get; protected set; } = false;
		public void SetEventLock(bool isLocked) => this.IsEventLocked = isLocked;


		protected override bool OnInit() {
			// Call base.OnInit() first to initialize stats and animation references.
			// on base class AttackComponentBase, we need to initialize the reference to
			//  CharacterStatsSystem and AnimationComponentBase, so we call base.OnInit() first to 
			// ensure those references are set up before we subscribe to input and potentially use those references
			//  in our attack logic.
			// Returns false if any required component is missing, short-circuiting this init.
			if (!base.OnInit()) return false;
			if (GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager)) {
				this._inputManager = inputManager;
			} else {
				Debug.LogError($"{this.gameObject.name}Controller: InputManager service not found!");
				return false;
			}
			this._fireSubscription = new InputActionSubscriptionLifetime<PlayerInputActionKey>(
				PlayerInputActionCollection.Player,
				PlayerInputActionKey.Fire,
				AttackForInputSystem
			);
			return true;
		}

		protected override void OnEnable() {
			base.OnEnable();
			Subscribe();
		}

		private void Subscribe() {
			// always subscribe to event when enabled, but only once. 
			if (!this.IsInitialized || this._isEventSubscribed) return;
			this._inputManager.Subscribe(this._fireSubscription);
			this._isEventSubscribed = true;

		}

		protected override void OnDisable() {
			base.OnDisable();
			Unsubscribe();
		}

		private void Unsubscribe() {
			this._inputManager.UnSubscribe(this._fireSubscription);
		}

		private void AttackForInputSystem(InputAction.CallbackContext context) {
			// this will prevent the attack to be performed by this component and 
			// and abilitycaster component at the same time, which will cause some weird bugs.
			// why we are blocking this specific input event instead of event on abilitycaster component?
			// because we want to make sure the player can still use the attack input to
			// trigger the attack animation, but when these is abilitycaster component on the player,
			// we want to handover the control of attack input to abilitycaster component, so the player
			// can use the attack input to trigger the ability instead of normal attack.
			if (context.performed && !this.IsEventLocked) {
				PerformAttack();
			}
		}

		protected override float PerformAttackInternal() {
			float damage = GetDamageValue();
			return damage;
		}
	}
}