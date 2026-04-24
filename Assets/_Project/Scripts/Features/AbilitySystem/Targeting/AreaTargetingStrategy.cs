// AreaTargetingStrategy.cs
using System;
using System.Collections.Generic;
using Kope.Component.Combat.Interface;
using UnityComponent = UnityEngine.Component;
using UnityEngine;
using UnityEngine.InputSystem;
using Kope.Component.HitBox.Interface;
using System.Linq;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class AreaTargetingStrategy : TargetingStrategy, ITargetingFactory {
		[SerializeField] private GameObject previewPrefab;
		[SerializeField] private float radius = 5f;
		[SerializeField] private float previewHeightOffset = 0.1f;

		private GameObject previewInstance;
		private Vector3 currentPoint;
		private InputActionSubscriptionLifetime<PlayerInputActionKey> inputSubscription;

		public TargetingStrategy Create() {
			return new AreaTargetingStrategy {
				previewPrefab = this.previewPrefab,
				radius = this.radius,
				previewHeightOffset = this.previewHeightOffset
			};
		}

		public override void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved) {
			// Begin(targetingManager, casterContext, effectContext, onTargetResolved);

			// if (this.previewPrefab != null && this.targetingManager != null) {
			// 	this.previewInstance = UnityEngine.Object.Instantiate(
			// 		this.previewPrefab, Vector3.zero, Quaternion.identity);
			// }

			// if (this.targetingManager != null && this.targetingManager.InputManager != null) {
			// 	this.inputSubscription = new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			// 		PlayerInputActionCollection.Player,
			// 		PlayerInputActionKey.Fire,
			// 		OnConfirm
			// 	);
			// 	this.targetingManager.InputManager.Subscribe(this.inputSubscription);
			// }
		}

		public override void Update() {
			if (!this._isTargeting || this.targetingManager == null) return;
			if (!this.targetingManager.TryGetMouseGroundPoint(out this.currentPoint)) return;

			if (this.previewInstance != null) {
				this.previewInstance.transform.position = this.currentPoint + Vector3.up * this.previewHeightOffset;
			}
		}

		public override void FinishTheStratrgy() {
			base.FinishTheStratrgy();

			if (this.previewInstance != null) {
				UnityEngine.Object.Destroy(this.previewInstance);
				this.previewInstance = null;
			}
		}


		protected override void ExecuteResolution(Vector3 clickPoint) {
			var targets = GetTargetsInArea(clickPoint);
			ResolveGroupOfTargets(targets);
		}
		private TargetContext[] GetTargetsInArea(Vector3 point) {
			var colliders = Physics2D.OverlapCircleAll(point, this.radius, this.targetingManager.TargetLayerMask);
			var uniqueTargets = new HashSet<IHitBoxComponent>();
			var resolvedTargets = new TargetContext[colliders.Length];

			for (int i = 0; i < colliders.Length; i++) {
				var targetContext = TargetContext.Create(colliders[i]);
				if (targetContext == null || targetContext.HitBox == null) continue;
				if (!uniqueTargets.Add(targetContext.HitBox)) continue;
				resolvedTargets[i] = targetContext;
			}
			return resolvedTargets;
		}
	}
}
