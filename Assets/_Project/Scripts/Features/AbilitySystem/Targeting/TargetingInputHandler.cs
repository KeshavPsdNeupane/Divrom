using System;
using Kope.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kope.Component.Ability.Targeting {

	[System.Serializable]
	public class TargetingInputHandler {
		[Header("Gamepad Momentum Settings")]
		[SerializeField, Range(0f, 100f)] private float gamepadCursorSpeed = 15f;
		[SerializeField, Range(0f, 100f)] private float gamePadCursorReturnSpeed = 8f;
		[SerializeField] private float gamepadReturnDelay = 0.25f;
		private InputManager _inputManager;
		private AxisMode _axisMode = AxisMode.TwoD;

		private Vector2 _lastLookInputPosition = Vector2.zero;
		private bool _isUsingGamepad = false;

		// Anti-flicker locking mechanisms
		private bool _hasLockedDeviceForSession = false;
		private bool _sessionLockedToGamepad = false;

		private Vector3 _gamepadAimOffset = Vector3.zero;
		private float _timeStickReleased = 0f;
		private bool _isStickReleased = true; // Tracks if the stick is currently considered released

		// Input Lifetimes
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _fireInput;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _cancelInput;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _lookingAt;

		private bool _isSubscribed;
		private bool _hasActiveStrategy;

		public Vector2 LastLookInputPosition => _lastMousePositionFallback();
		public bool IsUsingGamepad => _isUsingGamepad;
		public Vector3 GamepadAimOffset => _gamepadAimOffset;

		public void Initialize(InputManager inputManager, AxisMode axisMode, Action<InputAction.CallbackContext> onConfirm, Action<InputAction.CallbackContext> onCancel) {
			this._inputManager = inputManager;
			this._axisMode = axisMode;

			this._fireInput = new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			   PlayerInputActionCollection.Player, PlayerInputActionKey.Fire, onConfirm);

			this._cancelInput = new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			   PlayerInputActionCollection.Player, PlayerInputActionKey.Dodge, onCancel);

			this._lookingAt = new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			   PlayerInputActionCollection.Player, PlayerInputActionKey.Look, HandleLook, true);
		}

		public void ResetSession(bool activeStrategyState) {
			this._hasActiveStrategy = activeStrategyState;
			this._gamepadAimOffset = Vector3.zero;
			this._hasLockedDeviceForSession = false;
			// Preserve the currently active device instead of forcing mouse mode.
			this._isUsingGamepad = Gamepad.current != null &&
								   Gamepad.current.wasUpdatedThisFrame;

			this._lastLookInputPosition = Vector2.zero;
			this._isStickReleased = true;
		}

		public void UpdateTick() {
			if (!this._isUsingGamepad || !this._hasActiveStrategy) return;
			Vector3 inputVelocity;
			if (this._axisMode == AxisMode.TwoD) {
				inputVelocity = new Vector3(this._lastLookInputPosition.x, this._lastLookInputPosition.y, 0f);
			} else {
				inputVelocity = new Vector3(this._lastLookInputPosition.x, 0f, this._lastLookInputPosition.y);
			}

			// Hardware Stick Drift Gate check using absolute threshold values
			if (this._lastLookInputPosition.sqrMagnitude > 0.1f) {
				// Moving the stick pushes the offset outwards continuously
				this._gamepadAimOffset += this.gamepadCursorSpeed * Time.deltaTime * inputVelocity.normalized;
			} else {
				// If the stick is unpressed, evaluate the 1-second delay grace window before executing return lerp
				if (this._isStickReleased && (Time.unscaledTime - this._timeStickReleased >= this.gamepadReturnDelay)) {
					this._gamepadAimOffset = Vector3.Lerp(this._gamepadAimOffset, Vector3.zero, this.gamePadCursorReturnSpeed * Time.deltaTime);
				}
			}
		}

		private void HandleLook(InputAction.CallbackContext context) {
			if (!this._hasActiveStrategy) return;

			// Early exit for non-look inputs or if the device is not determined yet
			// Stick returned to deadzone / control released — explicit reset, don't rely on
			// another `performed` callback firing with a zero value (it won't).
			if (context.canceled) {
				this._lastLookInputPosition = Vector2.zero;
				if (!this._isStickReleased) {
					this._timeStickReleased = Time.unscaledTime;
					this._isStickReleased = true;
				}
				return;
			}

			if (!context.performed) return;

			bool incomingIsGamepad = false;
			if (context.control != null && context.control.device != null) {
				incomingIsGamepad = context.control.device is Gamepad;
			}

			if (!this._hasLockedDeviceForSession) {
				if (incomingIsGamepad && context.ReadValue<Vector2>().sqrMagnitude < 0.1f) return;

				this._sessionLockedToGamepad = incomingIsGamepad;
				this._hasLockedDeviceForSession = true;
			}

			if (incomingIsGamepad != this._sessionLockedToGamepad) return;

			Vector2 inputVal = context.ReadValue<Vector2>();

			if (inputVal.sqrMagnitude > 0.1f) {
				this._isStickReleased = false;
			} else {
				if (!this._isStickReleased) {
					this._timeStickReleased = Time.unscaledTime;
					this._isStickReleased = true;
				}
			}

			this._lastLookInputPosition = inputVal;
			this._isUsingGamepad = incomingIsGamepad;
		}

		private Vector2 _lastMousePositionFallback() {
			return Mouse.current != null ? Mouse.current.position.ReadValue() : this._lastLookInputPosition;
		}

		public void SubscribeInput() {
			if (this._isSubscribed || _inputManager == null) return;
			this._inputManager.Subscribe(this._fireInput);
			this._inputManager.Subscribe(this._cancelInput);
			this._inputManager.Subscribe(this._lookingAt);
			this._isSubscribed = true;
		}

		public void UnsubscribeInput() {
			if (!this._isSubscribed || _inputManager == null) return;
			this._inputManager.UnSubscribe(this._fireInput);
			this._inputManager.UnSubscribe(this._cancelInput);
			this._inputManager.UnSubscribe(this._lookingAt);
			this._isSubscribed = false;
		}
	}
}