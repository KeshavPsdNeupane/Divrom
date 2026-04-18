// AreaTargetingStrategy.cs
using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using UnityComponent = UnityEngine.Component;
using UnityEngine;
using UnityEngine.InputSystem;
using Kope.Component.HitBox.Interface;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class AreaTargetingStrategy : TargetingStrategy, ITargetingFactory {
		[SerializeField] private GameObject previewPrefab;
		[SerializeField] private float radius = 5f;
		[SerializeField] private float previewHeightOffset = 0.1f;

		private readonly List<TargetContext> resolvedTargets = new();
		private GameObject previewInstance;
		private Vector3 currentPoint;

		public TargetingStrategy Create() {
			return new AreaTargetingStrategy {
				previewPrefab = this.previewPrefab,
				radius = this.radius,
				previewHeightOffset = this.previewHeightOffset
			};
		}

		public override void Start(
			TargetingManager targetingManager,
			in TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved) {
			Begin(targetingManager, casterContext, effectContext, onTargetResolved);

			if (this.previewPrefab != null && this.targetingManager != null) {
				this.previewInstance = UnityEngine.Object.Instantiate(
					this.previewPrefab, Vector3.zero, Quaternion.identity);
			}

			this.targetingManager.InputManager?.SubscribeToInputAction(
				PlayerInputActionMap.Player,
				PlayerInputActionKey.Fire.ToString(),
				OnConfirm
			);
		}

		public override void Update() {
			if (!this.isTargeting || this.targetingManager == null) return;
			if (!this.targetingManager.TryGetMouseGroundPoint(out this.currentPoint)) return;

			if (this.previewInstance != null) {
				this.previewInstance.transform.position = this.currentPoint + Vector3.up * this.previewHeightOffset;
			}
		}

		public override void Cancel() {
			this.targetingManager?.InputManager?.UnsubscribeFromInputAction(
				PlayerInputActionMap.Player,
				PlayerInputActionKey.Fire.ToString(),
				OnConfirm
			);

			if (this.previewInstance != null) {
				UnityEngine.Object.Destroy(this.previewInstance);
				this.previewInstance = null;
			}

			this.resolvedTargets.Clear();
			base.Cancel();
		}

		private void OnConfirm(InputAction.CallbackContext context) {
			if (!context.performed || !this.isTargeting || this.targetingManager == null) return;
			if (!this.targetingManager.TryGetMouseGroundPoint(out var hitPoint)) return;

			ResolveTargets(hitPoint);
			if (this.resolvedTargets.Count == 0) return;

			// Area targeting resolves multiple targets — invoke callback for each
			for (int i = 0; i < this.resolvedTargets.Count; i++) {
				var target = this.resolvedTargets[i];
				if (target.HitBox == null) continue;

				var targetPosition = target.HitBox is UnityComponent c
					? c.transform.position
					: Vector3.zero;

				ResolveTarget(target, targetPosition);
			}
			Cancel();
		}

		private void ResolveTargets(Vector3 point) {
			this.resolvedTargets.Clear();
			var colliders = Physics2D.OverlapCircleAll(point, this.radius, this.targetingManager.TargetLayerMask);
			var uniqueTargets = new HashSet<IHurtBoxComponent>();

			for (int i = 0; i < colliders.Length; i++) {
				var targetContext = TargetContext.Create(colliders[i]);
				if (targetContext == null || targetContext.HitBox == null) continue;
				if (!uniqueTargets.Add(targetContext.HitBox)) continue;
				this.resolvedTargets.Add(targetContext);
			}
		}
	}
}