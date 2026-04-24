using System;
using Kope.Core.Init;
using ServiceLocatorPattern;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kope.Component.Ability.Targeting {

	public class TargetingManager : InitializableBase {
		[Header("Detection Settings")]
		[SerializeField] private Camera cam;
		[SerializeField] private LayerMask groundLayerMask = -1;

		private InputManager _inputManager;
		private TargetingStrategy _currentStrategy;
		private bool _isSubscribed;

		// Input Lifetimes
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _fireInput;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _cancelInput;

		// Events
		public event Action OnTargetingCleanupRequested;

		// Properties
		public LayerMask TargetLayerMask => groundLayerMask;
		public bool IsTargeting => this._currentStrategy != null && this._currentStrategy.IsTargeting;
		public Camera Camera => cam;

		protected override bool OnInit() {
			if (this.cam == null) cam = Camera.main;
			if (!GlobalServiceLocator.Instance.TryGetService(out _inputManager)) return false;

			this._fireInput = new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			   PlayerInputActionCollection.Player, PlayerInputActionKey.Fire, HandleConfirmInput);

			this._cancelInput = new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			   PlayerInputActionCollection.Player, PlayerInputActionKey.Dodge, HandleCancelInput);

			return true;
		}

		private void HandleConfirmInput(InputAction.CallbackContext context) {
			if (!context.performed || _currentStrategy == null) return;

			if (TryGetMouseGroundPoint(out var groundPoint)) {
				this._currentStrategy.ProcessInput(groundPoint);
			} else {
				CancelTargeting();
			}

			// Signal the Caster to unlock input as the strategy has been processed
			this.OnTargetingCleanupRequested?.Invoke();
		}

		private void HandleCancelInput(InputAction.CallbackContext context) {
			if (!context.performed || _currentStrategy == null) return;

			CancelTargeting();
			this.OnTargetingCleanupRequested?.Invoke();
		}

		/// <summary>
		/// Sets a new active targeting strategy. Called by Ability.Cast().
		/// </summary>
		public void SetCurrentStrategy(TargetingStrategy strategy) {
			this._currentStrategy = strategy;
		}

		/// <summary>
		/// Forcefully aborts the current targeting process.
		/// </summary>
		public void CancelTargeting() {
			if (this._currentStrategy == null) return;

			var strategyToClear = this._currentStrategy;
			this._currentStrategy = null;
			strategyToClear.FinishTheStratrgy();
		}

		/// <summary>
		/// Called by the Strategy itself when it naturally completes its lifecycle.
		/// </summary>
		public void NotifyStrategyFinished(TargetingStrategy strategy) {
			if (this._currentStrategy != strategy) return;
			this._currentStrategy = null;
			// Note: We don't invoke CleanupRequested here because if it's an auto-cast, 
			// the Caster handles the cleanup immediately after calling Cast().
		}

		public bool TryGetMouseGroundPoint(out Vector3 point) {
			point = default;
			var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

			if (Physics.Raycast(ray, out var hit, 200f, groundLayerMask)) {
				point = hit.point;
				return true;
			}
			return false;
		}

		private void SubscribeInput() {
			if (this._isSubscribed) return;
			this._inputManager.Subscribe(this._fireInput);
			this._inputManager.Subscribe(this._cancelInput);
			this._isSubscribed = true;
		}

		private void UnsubscribeInput() {
			if (!this._isSubscribed) return;
			this._inputManager.UnSubscribe(this._fireInput);
			this._inputManager.UnSubscribe(this._cancelInput);
			this._isSubscribed = false;
		}

		protected override void OnUpdate() => this._currentStrategy?.Update();
		private void OnEnable() => SubscribeInput();
		private void OnDisable() { UnsubscribeInput(); CancelTargeting(); }
		protected override void OnShutdown() { base.OnShutdown(); OnDisable(); }
	}
}