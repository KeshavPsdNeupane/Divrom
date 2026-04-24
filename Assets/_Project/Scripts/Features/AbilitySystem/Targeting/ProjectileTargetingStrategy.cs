// ProjectileTargetingStrategy.cs
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

		public override void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved) {
			Begin(targetingManager, casterContext, effectContext, onTargetResolved);

			if (this.projectilePrefab == null || this.targetingManager == null || this.targetingManager.Camera == null) {
				FinishTheStratrgy();
				return;
			}

			var spawnPosition = this.targetingManager.transform.position + this.spawnOffset;
			var direction = ResolveLaunchDirection();
			var rotation = direction.sqrMagnitude > 0.0001f
				? Quaternion.LookRotation(direction.normalized)
				: Quaternion.identity;

			var projectileObject = UnityEngine.Object.Instantiate(
				this.projectilePrefab, spawnPosition, rotation);

			if (projectileObject.TryGetComponent<AbilityProjectileController>(out var controller)) {
				// Projectile strategy hands off the callback to the controller —
				// the controller resolves the target on hit, then signals completion so the strategy can cancel.
				controller.Initialize(
					onTargetResolved,
					FinishTheStratrgy,
					this.effectContext,
					direction,
					this.projectileSpeed,
					this.projectileLifetime);
				return;
			}

			UnityEngine.Object.Destroy(projectileObject);
			FinishTheStratrgy();
		}

		private Vector3 ResolveLaunchDirection() {
			if (this.targetingManager == null || this.targetingManager.Camera == null) return Vector3.forward;

			if (this.targetingManager.TryGetMouseGroundPoint(out var hitPoint)) {
				var direction = hitPoint - this.targetingManager.transform.position;
				return direction.sqrMagnitude > 0.0001f ? direction.normalized : this.targetingManager.Camera.transform.forward;
			}

			return this.targetingManager.Camera.transform.forward;
		}
		protected override void ExecuteResolution(Vector3 clickPoint) {
			// no op for now.
			// but later this stragity will manage resolutution of target by using mouse click position.
			// by itself.
		}
	}
}