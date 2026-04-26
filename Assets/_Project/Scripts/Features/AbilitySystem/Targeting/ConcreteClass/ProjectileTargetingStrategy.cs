using System;
using Kope.Component.Combat.Interface;
using Kope.Core;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {
	[Serializable]
	public sealed class ProjectileTargetingStrategyFactory : ITargetingFactory {
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField] private float projectileSpeed = 18f;
		[SerializeField] private float projectileLifetime = 2f;
		[SerializeField] private Vector3 spawnOffset = new(0f, 1f, 0f);
		[SerializeField, Tooltip("Number of enemies the projectile can pierce through.")]
		private int pierceCount = 0;

		public TargetingStrategy Create() {
			return new ProjectileTargetingStrategy(
				this.projectilePrefab,
				this.projectileSpeed,
				this.projectileLifetime,
				this.spawnOffset,
				this.pierceCount
			);
		}
	}

	public sealed class ProjectileTargetingStrategy : TargetingStrategy {
		private readonly GameObject _projectilePrefab;
		private readonly float _projectileSpeed;
		private readonly float _projectileLifetime;
		private readonly Vector3 _spawnOffset;
		private readonly int _pierceCount;

		public ProjectileTargetingStrategy(GameObject projectilePrefab, float projectileSpeed,
			float projectileLifetime, Vector3 spawnOffset, int pierceCount) {
			this._projectilePrefab = projectilePrefab;
			this._projectileSpeed = projectileSpeed;
			this._projectileLifetime = projectileLifetime;
			this._spawnOffset = spawnOffset;
			this._pierceCount = pierceCount;
		}

		public override void Start(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			ITargetingReceiver onTargetResolved) {

			Begin(targetingManager, casterContext, effectContext, onTargetResolved);

			if (this._projectilePrefab == null || this.targetingManager == null) {
				FinishTheStratrgy();
			}
		}

		protected override bool ExecuteResolution(Vector3 clickPoint) {
			Vector3 origin = this.casterContext.HitBox.Transform.position;
			Vector3 direction = GetDirectionToClickPoint(clickPoint, origin);

			var rotation = CalculateSpawnRotation(direction, this.effectContext.Dimension);
			var spawnPosition = CalculateSpawnPosition(origin, direction, this._spawnOffset, this.effectContext.Dimension);

			var projectileObject = UnityEngine.Object.Instantiate(this._projectilePrefab, spawnPosition, rotation);

			if (projectileObject.TryGetComponent<AbilityProjectileController>(out var controller)) {
				// We pass a callback to FinishTheStratrgy so the strategy ends when the projectile is done
				controller.Initialize(this._onTargetResolved, FinishTheStratrgy, this.effectContext,
									  direction, this._projectileSpeed, this._projectileLifetime, this._pierceCount);
				return false; // False because we handle resolution inside the controller
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
			return dimension == AxisMode.TwoD
				? Quaternion.LookRotation(Vector3.forward, direction)
				: Quaternion.LookRotation(direction);
		}

		private Vector3 CalculateSpawnPosition(Vector3 position, Vector3 fwd, Vector3 offset, AxisMode dimension) {
			if (dimension == AxisMode.TwoD) {
				/*
					Using projection the spawn point around the caster rather than using the Trignometic approch
					since arcTan2 is expensive and this method is more performant, and the slight inaccuracy 
					in spawn position is not noticeable for most cases, and can be adjusted with the offset values if needed.
					Formula Derivation:
					Let the forward direction be represented as a 2D vector (v.x, v.y) and the desired spawn offset
					 as (o.x, o.y) where o.x is the perpendicular offset and o.y is the forward offset. The spawn 
					 position can be calculated as:
					 "In a sense we are using 2d "cross product" to get the perpendicular offset direction"
					 spawn.x = position.x + (v.x * o.y) + (v.y * o.x) 
					 spawn.y = position.y + (v.y * o.y) - (v.x * o.x)
					 This formula effectively rotates the offset vector by the angle of 
					 the forward direction and then translates it to the caster's position, giving us 
					 the correct spawn point around the caster based on the forward direction and the specified offsets.
				*/
				float offsetX = (fwd.x * offset.y) + (fwd.y * offset.x);
				float offsetY = (fwd.y * offset.y) - (fwd.x * offset.x);
				return new Vector3(position.x + offsetX, position.y + offsetY, 0f);
			}
			// just using normal crossproduct to get the right and up directions for the offset, since we are 
			// in 3D and can have any forward direction
			Vector3 side = Vector3.Cross(Mathf.Abs(fwd.y) > 0.9f ? Vector3.right : Vector3.up, fwd).normalized;
			Vector3 up = Vector3.Cross(fwd, side);
			return position + (fwd * offset.z) + (side * offset.x) + (up * offset.y);
		}
	}
}