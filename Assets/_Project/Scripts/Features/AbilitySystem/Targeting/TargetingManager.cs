using System;
using Kope.Core;
using Kope.Core.Init;
using Kope.Core.ServiceLocator;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kope.Component.Ability.Targeting {

	public class TargetingManager : InitializableBase {
		[Header("Detection Settings")]
		[SerializeField] private Camera cam;
		[SerializeField] private LayerMask groundLayerMask = -1;
		[SerializeField, Range(1, 10)] private int maxFrameDelayBeforeCleanup = 2;
		private InputManager _inputManager;
		private TargetingStrategy _currentStrategy;
		private bool _isSubscribed;
		private AxisMode _axisMode = AxisMode.TwoD;
		private int _cleanupFrame = -1;


		// Input Lifetimes
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _fireInput;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> _cancelInput;

		// Events
		public event Action OnTargetingCleanupRequested;

		public bool IsTargeting => this._currentStrategy != null && this._currentStrategy.IsTargeting;
		public Camera Camera => cam;

		protected override bool OnInit() {
			if (this.cam == null) cam = Camera.main;
			if (!GlobalServiceLocator.Instance.TryGetService(out _inputManager)) return false;
			this._axisMode = GlobalServiceLocator.Dimension;
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

			/* * ARCHITECTURAL DECISION: FRAME-BASED DEFERRAL
			 * We avoid using Coroutines here to prevent unnecessary heap allocations (IEnumerator garbage) 
			 * and to maintain high-performance, deterministic execution within the Kope's framework.
			 *
			 * WHY DEFER?
			 * 1. EVENT RACE CONDITIONS: The Unity InputSystem does not guarantee execution order between 
			 * this TargetingManager and other components (like AttackComponent). If we unlock input 
			 * immediately, the same mouse click could accidentally trigger an attack in the same frame.
			 * * 2. STRATEGY SETTLING: Allows the active TargetingStrategy to complete any late-frame logic 
			 * or state-settling post-input before the Caster cleans up the references.
			 * * 3. SYSTEM STABILITY: By waiting 'n' frames, we ensure the InputManager has finished 
			 * broadcasting to all listeners for this specific Input Event before we signal a cleanup.
			 */
			this._cleanupFrame = Time.frameCount + this.maxFrameDelayBeforeCleanup;
		}

		private void HandleCancelInput(InputAction.CallbackContext context) {
			if (!context.performed || _currentStrategy == null) return;

			CancelTargeting();
			this._cleanupFrame = Time.frameCount + this.maxFrameDelayBeforeCleanup;
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
			strategyToClear.FinishTheStrategy();
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


		/// <summary>
		/// Attempts to acquire a world-space position on the ground plane based on the current mouse position.
		/// </summary>
		/// <remarks>
		/// RATIONALE: 
		/// - In 2D: Resolves via a Camera ScreenToWorldPoint projection. This is a pure algebraic matrix 
		///   transformation (O(1)) and is roughly 100x (2 orders of magnitude) more performant than physics queries.
		/// - In 3D: Resolves via a Physics Raycast against the ground layer. This is necessary to handle 
		///   variable terrain height, slopes, and non-flat geometry where simple projection would fail.
		/// </remarks>
		/// <param name="point">The acquired world-space position if successful; otherwise, Vector3.zero.</param>
		/// <returns>True if a valid point was acquired; false if the 3D raycast missed or the camera is null.</returns>
		public bool TryGetMouseGroundPoint(out Vector3 point) {
			point = Vector3.zero;
			Vector2 mousePos = Mouse.current.position.ReadValue();

			// RATIONALE: 
			// In 2D, we avoid physics entirely for point acquisition. ScreenToWorldPoint is a pure 
			// algebraic projection (Matrix multiplication), making it the most efficient way to 
			// map the cursor to the game plane without requiring 'Ground' colliders.
			if (this._axisMode == AxisMode.TwoD) {
				// RATIONALE: ScreenToWorldPoint requires a Z-depth to resolve the projection. 
				// We use the absolute distance from the camera to the Z=0 world plane.
				float distanceToPlane = Mathf.Abs(cam.transform.position.z);
				Vector3 mouseWithDepth = new(mousePos.x, mousePos.y, distanceToPlane);

				point = cam.ScreenToWorldPoint(mouseWithDepth);

				// Ensure absolute flattening to the XY plane to prevent floating point drift 
				// into the background/foreground.
				point.z = 0;

				return true;
			}
			// RATIONALE: 
			// In 3D, 'Ground' is rarely a flat plane at a fixed distance (terrain, ramps, stairs).
			// A Raycast against a specific LayerMask is the industry standard for finding a 
			// point on a variable-height surface.
			Ray ray = cam.ScreenPointToRay(mousePos);
			if (Physics.Raycast(ray, out RaycastHit hit, 200f, this.groundLayerMask)) {
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

		protected override void OnUpdate() {
			if (this._cleanupFrame != -1 && Time.frameCount >= this._cleanupFrame) {
				this._cleanupFrame = -1;
				this.OnTargetingCleanupRequested?.Invoke();
			}
			this._currentStrategy?.Update();
		}

		private void OnEnable() => SubscribeInput();
		private void OnDisable() { UnsubscribeInput(); CancelTargeting(); }
		protected override void OnShutdown() { base.OnShutdown(); OnDisable(); }
	}
}

