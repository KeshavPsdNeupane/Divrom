using System;
using Kope.Component.Combat.Interface;
using Kope.Core;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class ProjectileTargetingStrategy : TargetingStrategy, ITargetingFactory {
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField] private float projectileSpeed = 18f;
		[SerializeField] private float projectileLifetime = 2f;
		[SerializeField] private Vector3 spawnOffset = new(0f, 1f, 0f);
		[SerializeField, Tooltip("Number of enemies the projectile can pierce through.")]
		private int pierceCount = 0;
		public TargetingStrategy Create() {
			return new ProjectileTargetingStrategy {
				projectilePrefab = this.projectilePrefab,
				projectileSpeed = this.projectileSpeed,
				projectileLifetime = this.projectileLifetime,
				spawnOffset = this.spawnOffset,
				pierceCount = this.pierceCount
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
		}


		protected override bool ExecuteResolution(Vector3 clickPoint) {
			Vector3 origin = this.casterContext.HitBox.Transform.position;

			Vector3 direction = GetDirectionToClickPoint(clickPoint, origin);

			var rotation = CalculateSpawnRotation(direction, this.effectContext.Dimension);
			var spawnPosition = CalculateSpawnPosition(origin, direction, this.spawnOffset, this.effectContext.Dimension);

			var projectileObject = UnityEngine.Object.Instantiate(this.projectilePrefab, spawnPosition, rotation);

			if (projectileObject.TryGetComponent<AbilityProjectileController>(out var controller)) {
				controller.Initialize(this._onTargetResolved, () => FinishTheStratrgy(), this.effectContext,
									  direction, this.projectileSpeed, this.projectileLifetime, this.pierceCount);
				return false;
			}

			UnityEngine.Object.Destroy(projectileObject);
			return true;
		}
		private Vector3 GetDirectionToClickPoint(Vector3 clickPoint, Vector3 origin) {
			Vector3 direction = clickPoint - origin;
			if (this.effectContext.Dimension == AxisMode.TwoD) direction.z = 0;
			return direction.normalized;
		}

		private Quaternion CalculateSpawnRotation(Vector3 direction, AxisMode dimension) {
			// direction is already normalized and flattened
			return dimension == AxisMode.TwoD
				? Quaternion.LookRotation(Vector3.forward, direction)
				: Quaternion.LookRotation(direction);
		}

		private Vector3 CalculateSpawnPosition(Vector3 position, Vector3 fwd, Vector3 offset, AxisMode dimension) {
			if (dimension == AxisMode.TwoD) {
				// Pre-calculate shifted offsets to save a few operations
				// offset.y is "Forward" distance, offset.x is "Side" distance
				float offsetX = (fwd.x * offset.y) + (fwd.y * offset.x);
				float offsetY = (fwd.y * offset.y) - (fwd.x * offset.x);

				return new Vector3(position.x + offsetX, position.y + offsetY, 0f);
			}

			// 3D logic remains efficient
			Vector3 side = Vector3.Cross(Mathf.Abs(fwd.y) > 0.9f ? Vector3.right : Vector3.up, fwd).normalized;
			Vector3 up = Vector3.Cross(fwd, side);

			return position + (fwd * offset.z) + (side * offset.x) + (up * offset.y);
		}
	}
}