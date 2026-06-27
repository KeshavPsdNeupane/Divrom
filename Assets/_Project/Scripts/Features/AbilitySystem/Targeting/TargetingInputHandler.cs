using System;
using Kope.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public class TargetingInputHandler {
		[Header("Gamepad Momentum Settings")]
		[SerializeField, Range(0f, 100f)] private float gamepadCursorSpeed = 15f;
		[SerializeField, Range(0f, 100f)] private float gamePadCursorReturnSpeed = 8f;
		[SerializeField] private float gamepadReturnDelay = 0.25f;
		[SerializeField] private float gamepadSnappyHoldDuration = 1f;

		private InputManager _inputManager;
		private AxisMode _axisMode = AxisMode.TwoD;

		private Vector2 _lastLookInputPosition = Vector2.zero;
		private bool _isUsingGamepad = false;

		// Anti-flicker locking mechanisms
		private bool _hasLockedDeviceForSession = false;
		private bool _sessionLockedToGamepad = false;

		private Vector3 _gamepadAimOffset = Vector3.zero;
		private Vector3 _lastValidSnappyOffset = Vector3.zero;
		private float _timeStickReleased = 0f;
		private bool _isStickReleased = true;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _fireInput;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _cancelInput;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _lookingAt;

		private bool _isSubscribed;
		private bool _hasActiveStrategy;

		public Vector2 MouseScreenPosition => Mouse.current != null
			? Mouse.current.position.ReadValue()
			: this._lastLookInputPosition;

		public bool IsUsingGamepad => this._isUsingGamepad;
		public Vector3 GamepadAimOffset => this._gamepadAimOffset;

		public void Initialize(
			InputManager inputManager,
			AxisMode axisMode,
			Action<InputAction.CallbackContext> onConfirm,
			Action<InputAction.CallbackContext> onCancel) {

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
			this._lastValidSnappyOffset = Vector3.zero;
			this._hasLockedDeviceForSession = false;

			// Preserve the currently active device instead of forcing mouse mode.
			this._isUsingGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
			this._lastLookInputPosition = Vector2.zero;
			this._isStickReleased = true;
		}

		public void UpdateTick() {
			if (!this._isUsingGamepad || !this._hasActiveStrategy) return;

			if (this._lastLookInputPosition.sqrMagnitude > 0.1f) {
				// Moving the stick pushes the offset outwards continuously
				this._gamepadAimOffset += this.gamepadCursorSpeed * Time.deltaTime
					* BuildInputVelocity(this._lastLookInputPosition).normalized;

			} else if (this._isStickReleased &&
					   Time.unscaledTime - this._timeStickReleased >= this.gamepadReturnDelay) {
				// If the stick is unpressed, evaluate the delay grace window before executing return lerp
				this._gamepadAimOffset = Vector3.Lerp(
					this._gamepadAimOffset,
					Vector3.zero,
					this.gamePadCursorReturnSpeed * Time.deltaTime);
			}
		}

		/// <summary>
		/// Returns the world-space point on the ground that the player is currently aiming at, based on the current input device.
		/// If the player is using a gamepad, the aim point is calculated based on the gamepad's 
		/// stick input and the caster's position, with optional snapping behavior.
		/// </summary>
		/// <param name="casterPosition"> The position of the caster. </param>
		/// <param name="cam"> The camera used for mouse aiming. </param>
		/// <param name="groundLayerMask"> The layer mask for the ground. </param>
		/// <param name="maxRayDistance"> The maximum distance for the raycast. </param>
		/// <param name="maxTargetingDistance"> The maximum targeting distance.only for gamepad aiming. </param>
		/// <param name="isSnappy"> Whether to use snapping behavior. </param>
		/// <returns></returns>
		public Vector3 GetAimGroundPoint(
			Vector3 casterPosition,
			Camera cam,
			LayerMask groundLayerMask,
			float maxRayDistance,
			float maxTargetingDistance,
			bool isSnappy = false) {

			if (this._isUsingGamepad)
				return GetGamepadAimPoint(casterPosition, groundLayerMask, maxRayDistance, maxTargetingDistance, isSnappy);

			return GetMouseAimPoint(casterPosition, cam, groundLayerMask, maxRayDistance);
		}

		// =================================================================================
		// DETECTED GAMEPAD CONTROLLER EXECUTION BRANCH
		// =================================================================================

		private Vector3 GetGamepadAimPoint(
		Vector3 casterPosition,
		LayerMask groundLayerMask,
		float maxRayDistance,
		float maxTargetingDistance,
		bool isSnappy) {

			Vector3 aimOffset = ResolveGamepadOffset(maxRayDistance, isSnappy);

			// Stick in deadzone and grace window expired — stay at caster
			if (aimOffset == Vector3.zero)
				return casterPosition;

			// Clamp offset so the aim point never exceeds the targeting range from caster
			if (aimOffset.sqrMagnitude > maxTargetingDistance * maxTargetingDistance)
				aimOffset = aimOffset.normalized * maxTargetingDistance;

			if (this._axisMode == AxisMode.TwoD) {
				return new Vector3(
					casterPosition.x + aimOffset.x,
					casterPosition.y + aimOffset.y,
					0f);
			}

			// Project offset onto the physical ground geometry
			Vector3 projectedPosition = casterPosition + aimOffset;
			Vector3 rayOrigin = projectedPosition + Vector3.up * 10f;

			if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit,
					maxRayDistance, groundLayerMask))
				return groundHit.point;

			return projectedPosition;
		}

		private Vector3 ResolveGamepadOffset(float maxRayDistance, bool isSnappy) {
			// Non-snappy uses the accumulated momentum offset
			if (!isSnappy)
				return this._gamepadAimOffset;

			// Snappy returns a flat direction scaled to range — no position, no accumulation
			Vector3 snappyOffset = BuildInputVelocity(this._lastLookInputPosition);

			if (snappyOffset.sqrMagnitude >= 0.01f) {
				// Stick is being pushed — update and return live direction
				this._lastValidSnappyOffset = snappyOffset.normalized * maxRayDistance;
				return this._lastValidSnappyOffset;
			}

			// Stick in deadzone — hold last valid direction for the grace window so the
			// player has time to release the stick and press the attack button without
			// losing their aimed direction.
			if (this._isStickReleased &&
				Time.unscaledTime - this._timeStickReleased < this.gamepadSnappyHoldDuration) {
				return this._lastValidSnappyOffset;
			}

			return Vector3.zero;
		}

		// =================================================================================
		// STANDARD MOUSE AND KEYBOARD MAPPING EXECUTION BRANCH
		// =================================================================================

		private Vector3 GetMouseAimPoint(
			Vector3 casterPosition,
			Camera cam,
			LayerMask groundLayerMask,
			float maxRayDistance) {

			Vector2 mousePos = MouseScreenPosition;

			if (this._axisMode == AxisMode.TwoD) {
				float distanceToPlane = Mathf.Abs(cam.transform.position.z);
				Vector3 point = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, distanceToPlane));
				point.z = 0f;
				return point;
			}

			// 3D Mouse aiming tracking
			Ray ray = cam.ScreenPointToRay(mousePos);

			if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayerMask))
				return hit.point;

			// =================================================================================
			// ROBUST FALLBACK (When 3D Raycast misses physical ground geometry)
			// =================================================================================
			// Creates an infinite flat plane matching the caster's current feet elevation
			Plane fallbackPlane = new(Vector3.up, new Vector3(0f, casterPosition.y, 0f));

			if (fallbackPlane.Raycast(ray, out float enterDistance))
				return ray.GetPoint(enterDistance);

			// Hard fallback if the player aims exactly parallel to or away from the plane
			return ray.origin + ray.direction * maxRayDistance;
		}

		// =================================================================================
		// INPUT EVENT HANDLING
		// =================================================================================

		private void HandleLook(InputAction.CallbackContext context) {
			if (!this._hasActiveStrategy) return;

			// Stick returned to deadzone / control released — explicit reset, don't rely on
			// another `performed` callback firing with a zero value (it won't).
			if (context.canceled) {
				this._lastLookInputPosition = Vector2.zero;
				MarkStickReleased();
				return;
			}

			if (!context.performed) return;

			bool incomingIsGamepad = context.control?.device is Gamepad;

			if (!this._hasLockedDeviceForSession) {
				// Don't lock to gamepad on a resting stick — wait for intentional input
				if (incomingIsGamepad && context.ReadValue<Vector2>().sqrMagnitude < 0.1f)
					return;

				this._sessionLockedToGamepad = incomingIsGamepad;
				this._hasLockedDeviceForSession = true;
			}

			if (incomingIsGamepad != this._sessionLockedToGamepad) return;

			Vector2 inputVal = context.ReadValue<Vector2>();
			this._lastLookInputPosition = inputVal;
			this._isUsingGamepad = incomingIsGamepad;

			if (inputVal.sqrMagnitude > 0.1f)
				this._isStickReleased = false;
			else
				MarkStickReleased();
		}

		private void MarkStickReleased() {
			// Guard against re-stamping the release time if already released
			if (this._isStickReleased) return;
			this._timeStickReleased = Time.unscaledTime;
			this._isStickReleased = true;
		}

		// =================================================================================
		// SHARED AXIS UTILITY
		// =================================================================================

		/// <summary>
		/// Converts a 2D stick/mouse input into a 3D velocity vector respecting the current axis mode.
		/// </summary>
		private Vector3 BuildInputVelocity(Vector2 input) =>
			this._axisMode == AxisMode.TwoD
				? (Vector3)input
				: new Vector3(input.x, 0f, input.y);

		// =================================================================================
		// INPUT SUBSCRIPTION
		// =================================================================================

		public void SubscribeInput() {
			if (this._isSubscribed || this._inputManager == null) return;
			this._inputManager.Subscribe(this._fireInput);
			this._inputManager.Subscribe(this._cancelInput);
			this._inputManager.Subscribe(this._lookingAt);
			this._isSubscribed = true;
		}

		public void UnsubscribeInput() {
			if (!this._isSubscribed || this._inputManager == null) return;
			this._inputManager.UnSubscribe(this._fireInput);
			this._inputManager.UnSubscribe(this._cancelInput);
			this._inputManager.UnSubscribe(this._lookingAt);
			this._isSubscribed = false;
		}
	}
}