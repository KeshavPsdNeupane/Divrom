using System;
using Kope.Component.Combat.Interface;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityComponent = UnityEngine.Component;
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

		public override void Start(AbilityBase ability, TargetingManager targetingManager,
		in TargetContext casterContext, EffectContext effectContext) {
			Begin(ability, targetingManager, casterContext, effectContext);
			if (this.previewPrefab != null && this.targetingManager != null) {
				this.previewInstance = UnityEngine.Object.Instantiate(this.previewPrefab, Vector3.zero, Quaternion.identity);
			}

			if (this.targetingManager.InputManager != null) {
				this.targetingManager.InputManager.SubscribeToInputAction(
					PlayerInputActionMap.Player,
					PlayerInputActionKey.Fire.ToString(),
					OnConfirm
				);
			}
		}

		public override void Update() {
			if (!this.isTargeting || this.previewInstance == null || this.targetingManager == null) return;

			if (!this.targetingManager.TryGetMouseRaycast(out var hit, this.targetingManager.TargetLayerMask)) return;

			this.previewInstance.transform.position = hit.point + Vector3.up * this.previewHeightOffset;
		}

		public override void Cancel() {
			if (this.targetingManager != null && this.targetingManager.InputManager != null) {
				this.targetingManager.InputManager.UnsubscribeFromInputAction(
					PlayerInputActionMap.Player,
					PlayerInputActionKey.Fire.ToString(),
					OnConfirm
				);
			}
			if (this.previewInstance != null) {
				UnityEngine.Object.Destroy(this.previewInstance);
				this.previewInstance = null;
			}

			base.Cancel();
		}

		private void OnConfirm(InputAction.CallbackContext context) {
			if (!context.performed || !this.isTargeting || this.targetingManager == null) return;

			if (!this.targetingManager.TryGetMouseRaycast(out var hit, this.targetingManager.TargetLayerMask)) return;
			var targetContext = TargetContext.Create(hit.collider);
			if (targetContext.HitBox == null) return;

			var direction = targetContext.HitBox is UnityComponent targetComponent
				? (targetComponent.transform.position - this.targetingManager.transform.position).normalized
				: this.targetingManager.transform.forward;

			ExecuteOnTarget(targetContext, direction);
			Cancel();
		}
	}
}