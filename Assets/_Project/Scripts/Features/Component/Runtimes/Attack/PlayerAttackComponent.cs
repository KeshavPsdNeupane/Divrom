using UnityEngine.InputSystem;
using ServiceLocatorPattern;
using Kope.Core.CompilerServices;

namespace Kope.Component.Attack {
	public class PlayerAttackComponent : AttackComponentBase {
		private InputManager _inputManager;

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
				MyLogger.Error($"{this.gameObject.name}Controller: InputManager service not found!");
				return false;
			}
			return true;
		}

		protected override void OnEnable() {
			base.OnEnable();
			Subscribe();
		}

		private void Subscribe() {
			if (this._inputManager == null) return;

			this._inputManager.SubscribeToInputAction(
				PlayerInputActionMap.Player,
				PlayerInputActionKey.Fire.ToString(),
				AttackForInputSystem
		   );

		}

		protected override void OnDisable() {
			base.OnDisable();
			Unsubscribe();
		}

		private void Unsubscribe() {
			if (this._inputManager == null) return;

			this._inputManager.UnsubscribeFromInputAction(
				PlayerInputActionMap.Player,
				PlayerInputActionKey.Fire.ToString(),
				AttackForInputSystem
			);

		}

		private void AttackForInputSystem(InputAction.CallbackContext context) {
			if (context.performed) GetAttackDamage();
		}

		protected override float PerformAttackInternal() {

			float damage = CalculateDamage();
			// just a placeholder for now, we will implement the actual attack logic later
			//Debug.Log($"Attack performed! Damage: {damage}, Base attack: {this._attack}");
			return damage;
		}
	}
}