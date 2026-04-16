using System;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {
	[Serializable]
	public sealed class ProjectileTargetingStrategy : TargetingStrategy, ITargetingFactory {
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField] private float projectileSpeed = 18f;
		[SerializeField] private float projectileLifetime = 5f;
		[SerializeField] private Vector3 spawnOffset = new(0f, 1f, 0f);

		public TargetingStrategy Create() {
			return new ProjectileTargetingStrategy {
				projectilePrefab = this.projectilePrefab,
				projectileSpeed = this.projectileSpeed,
				projectileLifetime = this.projectileLifetime,
				spawnOffset = this.spawnOffset
			};
		}

		public override void Start(AbilityBase ability, TargetingManager targetingManager,
		in TargetContext casterContext, EffectContext effectContext) {
			Begin(ability, targetingManager, casterContext, effectContext);
			if (this.projectilePrefab == null || this.targetingManager == null || this.targetingManager.Cam == null) {
				Cancel();
				return;
			}

			var spawnPosition = this.targetingManager.transform.position + this.spawnOffset;
			var direction = ResolveLaunchDirection();
			var rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
			var projectileObject = UnityEngine.Object.Instantiate(this.projectilePrefab, spawnPosition, rotation);

			if (projectileObject.TryGetComponent<AbilityProjectileController>(out var controller)) {
				controller.Initialize(this.ability, this.effectContext, direction, this.projectileSpeed, this.projectileLifetime);
				Cancel();
				return;
			}

			UnityEngine.Object.Destroy(projectileObject);
			Cancel();
		}

		private Vector3 ResolveLaunchDirection() {
			if (this.targetingManager == null || this.targetingManager.Cam == null) {
				return Vector3.forward;
			}

			if (this.targetingManager.TryGetMouseRaycast(out var hit, this.targetingManager.TargetLayerMask)) {
				var direction = hit.point - this.targetingManager.transform.position;
				return direction.sqrMagnitude > 0.0001f ? direction.normalized : this.targetingManager.Cam.transform.forward;
			}

			return this.targetingManager.Cam.transform.forward;
		}
	}
}