using System;
using Kope.Component.Combat.Interface;
using Kope.Core;
using Kope.Core.ObjectPooling;
using Kope.Core.ServiceLocator;
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
		/*
    Why does this Strategy relinquish control immediately after spawning?
    
    1. The "Fire and Forget" Pattern: Projectiles are ballistic entities. Once the 
       LMB click resolution occurs, the 'targeting phase' is technically over. By 
       handing the lifetime management to the ProjectileController, the Strategy 
       can finish immediately. This keeps the TargetingManager's active strategy 
       list clean and prevents "Update-bloat."

    2. State Encapsulation: A projectile's death is often reactive (hitting a wall, 
       piercing a limit, or timing out). If the Strategy managed this, it would 
       have to maintain a persistent link to the projectile instance, creating 
       unnecessary coupling. Instead, the Strategy acts as a "Launcher"—it sets 
       the initial conditions (velocity, layer, logic) and then steps aside.

    3. Resource Efficiency: Since the strategy is cleared from the manager upon 
       resolution, it loses its ability to Tick() a timer. Letting the Projectile 
       (which is a persistent MonoBehavior in the scene) tick its own CountdownTimer 
       ensures that the "Return to Pool" logic is guaranteed to execute without 
       requiring a separate management task.
*/
		private readonly GameObject _projectilePrefab;
		private readonly float _projectileSpeed;
		private readonly float _projectileLifetime;
		private readonly Vector3 _spawnOffset;
		private readonly int _pierceCount;

		private ObjectPooler _universalPooler;

		public ProjectileTargetingStrategy(GameObject projectilePrefab, float projectileSpeed,
			float projectileLifetime, Vector3 spawnOffset, int pierceCount) {
			this._projectilePrefab = projectilePrefab;
			this._projectileSpeed = projectileSpeed;
			this._projectileLifetime = projectileLifetime;
			this._spawnOffset = spawnOffset;
			this._pierceCount = pierceCount;
		}

		public override void Start(TargetingManager targetingManager, TargetContext casterContext, EffectContext effectContext, ITargetingReceiver onTargetResolved) {
			Begin(targetingManager, casterContext, effectContext, onTargetResolved);
			// Get the pooler service
			if (this._universalPooler == null) {
				GlobalServiceLocator.Instance.TryGetService(out this._universalPooler);
			}

			if (this._projectilePrefab == null || this.targetingManager == null) {
				FinishTheStrategy();
			}
		}


		protected override bool ExecuteResolution(Vector3 clickPoint) {
			Vector3 origin = this.casterContext.HitBox.Transform.position;
			Vector3 direction = GetDirectionToClickPoint(clickPoint, origin);

			var rotation = CalculateSpawnRotation(direction, this.effectContext.Dimension);
			var spawnPosition = CalculateSpawnPosition(origin, direction, this._spawnOffset, this.effectContext.Dimension);

			if (this._universalPooler != null && this._projectilePrefab != null) {
				var go = this._universalPooler.Rent(this._projectilePrefab);
				go.transform.SetPositionAndRotation(spawnPosition, rotation);

				if (go.TryGetComponent<AbilityProjectileController>(out var controller)) {
					controller.OnProjectileRelease += CleanupProjectile;
					controller.Initialize(this._onTargetResolved, this.effectContext,
										  direction, this._projectileSpeed, this._projectileLifetime, this._pierceCount);

					return true;
				}
			}
			return true;
		}


		private void CleanupProjectile(GameObject obj) {
			if (this._universalPooler != null) {
				obj.SetActive(false);
				this._universalPooler.Release(this._projectilePrefab, obj);
			}
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

			Vector3 side = Vector3.Cross(Mathf.Abs(fwd.y) > 0.9f ? Vector3.right : Vector3.up, fwd).normalized;
			Vector3 up = Vector3.Cross(fwd, side);
			return position + (fwd * offset.z) + (side * offset.x) + (up * offset.y);
		}
	}
}
