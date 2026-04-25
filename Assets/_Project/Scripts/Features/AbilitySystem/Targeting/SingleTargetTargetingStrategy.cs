// SingleTargetTargetingStrategy.cs
using System;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class SingleTargetTargetingStrategy : TargetingStrategy, ITargetingFactory {
		[SerializeField] private GameObject previewPrefab;
		[SerializeField] private float previewHeightOffset = 0.1f;

		private GameObject previewInstance;

		public TargetingStrategy Create() {
			return new SingleTargetTargetingStrategy {
				previewPrefab = this.previewPrefab,
				previewHeightOffset = this.previewHeightOffset
			};
		}

		public override void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved) {
			Begin(targetingManager, casterContext, effectContext, onTargetResolved);

			if (this.previewPrefab != null && this.targetingManager != null) {
				this.previewInstance = UnityEngine.Object.Instantiate(
					this.previewPrefab, Vector3.zero, Quaternion.identity);
			}
			// if (this.targetingManager != null && this.targetingManager.InputManager != null) {

			// 	this.targetingManager.InputManager.Subscribe(
			// 		new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			// 			PlayerInputActionCollection.Player,
			// 			PlayerInputActionKey.Fire,
			// 			OnConfirm
			// 		)
			// 	);
			// }
		}

		public override void Update() {
			if (!this._isTargeting || this.previewInstance == null || this.targetingManager == null) return;
			if (!this.targetingManager.TryGetMouseGroundPoint(out var point)) return;

			this.previewInstance.transform.position = point + Vector3.up * this.previewHeightOffset;
		}

		public override void FinishTheStratrgy(bool clearOnTargetResolved = true) {
			// if (this.targetingManager != null && this.targetingManager.InputManager != null) {
			// 	this.targetingManager.InputManager.UnSubscribe(
			// 		new InputActionSubscriptionLifetime<PlayerInputActionKey>(
			// 			PlayerInputActionCollection.Player,
			// 			PlayerInputActionKey.Fire,
			// 			OnConfirm
			// 		)
			// 	);
			// }

			if (this.previewInstance != null) {
				UnityEngine.Object.Destroy(this.previewInstance);
				this.previewInstance = null;
			}

			base.FinishTheStratrgy();
		}

		protected override bool ExecuteResolution(Vector3 clickPoint) {
			// no op for now.
			// but later this stragity will manage resolutution of target by using mouse click position.
			// by itself.
			return true;
		}
	}
}