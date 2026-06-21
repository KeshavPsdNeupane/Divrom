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
		[SerializeField, Range(10f, 1000f), Tooltip("The maximum distance for raycasting during targeting."
		+ "Only for 3D targeting. Ignored for 2D targeting.")]
		private float maxRayDistance = 200f;
		[Header("Aggregated Processing System")]
		[SerializeField] private TargetingInputHandler inputHandler;

		private InputManager _inputManager;
		private TargetingStrategy _currentStrategy;
		private AxisMode _axisMode = AxisMode.TwoD;
		private int _cleanupFrame = -1;

		// Events
		public event Action OnTargetingCleanupRequested;

		public bool IsTargeting => this._currentStrategy != null && this._currentStrategy.IsTargeting;
		public Camera Camera => cam;

		protected override bool OnInit() {
			if (this.cam == null) {
				Debug.LogError($"[{this.GetType().Name}] Initialization failed: Camera reference is missing." +
				$"on{GetParentGameObjectHeirarchyMessage()}", this);
				return false;
			}
			if (!GlobalServiceLocator.Instance.TryGetService(out this._inputManager)) return false;
			this._axisMode = GlobalServiceLocator.Dimension;

			this.inputHandler.Initialize(this._inputManager, this._axisMode, HandleConfirmInput, HandleCancelInput);

			return true;
		}

		private void HandleConfirmInput(InputAction.CallbackContext context) {
			if (!context.performed || this._currentStrategy == null) return;

			if (TryGetAimGroundPoint(this._currentStrategy.CasterPosition, out var groundPoint)) {
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

		public void SetCurrentStrategy(TargetingStrategy strategy) {
			this._currentStrategy = strategy;
			this.inputHandler.ResetSession(this._currentStrategy != null);
		}

		public void CancelTargeting() {
			if (this._currentStrategy == null) return;

			var strategyToClear = this._currentStrategy;
			this._currentStrategy = null;
			strategyToClear.FinishTheStrategy();
			this.inputHandler.ResetSession(false);
		}

		public void NotifyStrategyFinished(TargetingStrategy strategy) {
			if (this._currentStrategy != strategy) return;
			this._currentStrategy = null;
			this.inputHandler.ResetSession(false);
		}
		public bool TryGetAimGroundPoint(Vector3 casterPosition, out Vector3 point) {
			point = Vector3.zero;

			// =================================================================================
			// DETECTED GAMEPAD CONTROLLER EXECUTION BRANCH
			// =================================================================================
			if (this.inputHandler.IsUsingGamepad) {
				Vector3 aimOffset = this.inputHandler.GamepadAimOffset;

				if (this._axisMode == AxisMode.TwoD) {
					point = casterPosition + aimOffset;
					point.z = 0f;
					return true;
				} else {
					Vector3 presidentialOffset = casterPosition + aimOffset;

					Vector3 rayOrigin = presidentialOffset + Vector3.up * 10f;

					if (Physics.Raycast(
						rayOrigin,
						Vector3.down,
						out RaycastHit groundHit,
						this.maxRayDistance,
						this.groundLayerMask)) {
						point = groundHit.point;
						return true;
					}

					point = presidentialOffset;
					return true;
				}
			}

			// =================================================================================
			// STANDARD MOUSE AND KEYBOARD MAPPING EXECUTION BRANCH
			// =================================================================================
			Vector2 mousePos = this.inputHandler.LastLookInputPosition;

			if (this._axisMode == AxisMode.TwoD) {
				float distanceToPlane = Mathf.Abs(cam.transform.position.z);

				Vector3 mouseWithDepth = new(
					mousePos.x,
					mousePos.y,
					distanceToPlane);

				point = cam.ScreenToWorldPoint(mouseWithDepth);
				point.z = 0f;

				return true;
			}

			Ray ray = cam.ScreenPointToRay(mousePos);

			if (Physics.Raycast(
				ray,
				out RaycastHit hit,
				maxRayDistance,
				this.groundLayerMask)) {
				point = hit.point;
				return true;
			}

			return false;
		}
		private void OnEnable() => this.inputHandler.SubscribeInput();
		private void OnDisable() { this.inputHandler.UnsubscribeInput(); CancelTargeting(); }
		protected override void OnShutdown() { base.OnShutdown(); OnDisable(); }

		protected override void OnUpdate() {
			if (this._cleanupFrame != -1 && Time.frameCount >= this._cleanupFrame) {
				this._cleanupFrame = -1;
				this.OnTargetingCleanupRequested?.Invoke();
			}

			this.inputHandler.UpdateTick();
			this._currentStrategy?.Update();
		}
	}
}