using Kope.Component.Combat.Interface;
using Kope.Core.CompilerServices;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using ServiceLocatorPattern;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kope.Component.Ability.Targeting {

	public class TargetingManager : InitializableBase {
		[SerializeField] private Camera cam;
		[SerializeField] private LayerMask targetLayerMask = -1;
		[SerializeField] private LayerMask groundLayerMask = -1;

		private InputManager inputManager;
		private TargetingStrategy currentStrategy;

		public Camera Cam => this.cam;
		public InputManager InputManager {
			get {
				if (this.inputManager == null && GlobalServiceLocator.Instance != null) {
					GlobalServiceLocator.Instance.TryGetService(out this.inputManager);
				}
				return this.inputManager;
			}
		}
		public bool IsTargeting => this.currentStrategy != null && this.currentStrategy.IsTargeting;
		public LayerMask TargetLayerMask => this.targetLayerMask;
		public LayerMask GroundLayerMask => this.groundLayerMask;

		protected override bool OnInit() {
			if (this.cam == null) {
				this.cam = Camera.main;
			}

			if (this.cam == null) {
				MyLogger.Error($"TargetingManager on {gameObject.name} has no Camera assigned.");
				return false;
			}

			if (!GlobalServiceLocator.Instance.TryGetService(out this.inputManager)) {
				MyLogger.Error($"TargetingManager on {gameObject.name} could not resolve InputManager.");
				return false;
			}

			return true;
		}

		protected override void OnUpdate() {
			if (this.currentStrategy == null || !this.currentStrategy.IsTargeting) return;
			this.currentStrategy.Update();
		}

		protected override void OnShutdown() {
			CancelCurrentTargeting();
		}

		public void SetCurrentStrategy(TargetingStrategy strategy) {
			this.currentStrategy = strategy;
		}

		public void ClearCurrentStrategy(TargetingStrategy strategy = null) {
			if (strategy != null && this.currentStrategy != strategy) return;
			this.currentStrategy = null;
		}

		public void CancelCurrentTargeting() {
			this.currentStrategy?.Cancel();
			this.currentStrategy = null;
		}

		public bool TryGetMouseRaycast(out RaycastHit hit, LayerMask? maskOverride = null) {
			hit = default;
			if (this.cam == null || Mouse.current == null) return false;

			var ray = this.cam.ScreenPointToRay(Mouse.current.position.ReadValue());
			var mask = maskOverride ?? this.targetLayerMask;
			return Physics.Raycast(ray, out hit, 200f, mask);
		}

		public bool TryGetMouseGroundPoint(out Vector3 point) {
			point = default;
			if (this.cam == null || Mouse.current == null) return false;

			var ray = this.cam.ScreenPointToRay(Mouse.current.position.ReadValue());
			if (!Physics.Raycast(ray, out var hit, 200f, this.groundLayerMask)) return false;

			point = hit.point;
			return true;
		}

		public bool TryResolveCombatTarget(Collider collider, out IDamageProcessor target) {
			target = null;
			if (collider == null) return false;

			var registry = collider.GetComponentInParent<EntityComponentsRegistry>();
			if (registry == null || registry.ComponentRegistry == null) return false;

			return registry.ComponentRegistry.TryGetReadOnlyComponent(out target, false);
		}

		public bool TryResolveCombatTarget(RaycastHit hit, out IDamageProcessor target) {
			return TryResolveCombatTarget(hit.collider, out target);
		}
	}
}