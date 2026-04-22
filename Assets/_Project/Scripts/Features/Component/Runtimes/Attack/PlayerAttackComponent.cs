using UnityEngine.InputSystem;
using ServiceLocatorPattern;
using Kope.Core.CompilerServices;
using UnityEngine;

namespace Kope.Component.Attack {
	public class PlayerAttackComponent : AttackComponentBase {
		[SerializeField]
		[Tooltip("If true, this component subscribes to the 'Fire' input. " +
			 "Set to false if an external system (like PlayerAbilityCaster) " +
			 "is responsible for triggering attacks.")]
		private bool subscribeToInput = true;
		private InputManager _inputManager;
		private bool _isEventSubscribed;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _fireSubscription;
		protected override bool OnInit() {
			// Call base.OnInit() first to initialize stats and animation references.
			// on base class AttackComponentBase, we need to initialize the reference to
			//  CharacterStatsSystem and AnimationComponentBase, so we call base.OnInit() first to 
			// ensure those references are set up before we subscribe to input and potentially use those references
			//  in our attack logic.
			// Returns false if any required component is missing, short-circuiting this init.


			// always update this flag based on the current value of SubScribeToInput, this is 
			// to ensure that if the value is changed in the inspector during runtime, 
			// the component will subscribe or unsubscribe accordingly on enable/disable.
			this.AlreadySubscribedToAttackEvent = this.subscribeToInput;
			if (!base.OnInit()) return false;
			if (GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager)) {
				this._inputManager = inputManager;
			} else {
				MyLogger.Error($"{this.gameObject.name}Controller: InputManager service not found!");
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
			if (!this.IsInitialized || !this.subscribeToInput || this._isEventSubscribed) return;
			Debug.Log("Subscribing to input events.");
			this._inputManager.Subscribe(this._fireSubscription);
			this._isEventSubscribed = true;

		}

		protected override void OnDisable() {
			base.OnDisable();
			Unsubscribe();
		}

		private void Unsubscribe() {
			if (!this._isEventSubscribed) return;
			this._inputManager.UnSubscribe(this._fireSubscription);
			this._isEventSubscribed = false;
		}

		private void AttackForInputSystem(InputAction.CallbackContext context) {
			if (context.performed) PerformAttack();
		}

		protected override float PerformAttackInternal() {

			float damage = CalculateDamage();
			return damage;
		}
	}
}