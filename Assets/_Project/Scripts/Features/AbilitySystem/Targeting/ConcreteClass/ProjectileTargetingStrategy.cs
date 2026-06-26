using System;
using Kope.Component.Combat.Interface;
using Kope.AbilitySystem;
using Kope.Core;
using Kope.Core.ObjectPooling;
using Kope.Core.ServiceLocator;
using UnityEngine;
using Kope.Core.Mathfx;
namespace Kope.Component.Ability.Targeting {
	[Serializable]
	public sealed class ProjectileTargetingStrategyFactory : ITargetingFactory {
		[SerializeField] private GameObject projectilePrefab;
		[SerializeField] private GameObject projectileLinePreviewPrefab;
		[SerializeField] private float projectileSpeed = 18f;
		[SerializeField] private float projectileLifetime = 2f;
		[SerializeField] private Vector3 spawnOffset = new(0f, 1f, 0f);
		[SerializeField, Tooltip("Number of enemies the projectile can pierce through.")]
		private int pierceCount = 0;

		public TargetingStrategy Create() {
			return new ProjectileTargetingStrategy(
				this.projectilePrefab,
				this.projectileLinePreviewPrefab,
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
		private readonly GameObject _projectileLinePreviewObject;
		private readonly float _projectileSpeed;
		private readonly float _projectileLifetime;
		private readonly Vector3 _spawnOffset;
		private readonly int _pierceCount;

		private ObjectPooler _universalPooler;

		public ProjectileTargetingStrategy(GameObject projectilePrefab, GameObject projectileLinePreviewPrefab,
		float projectileSpeed,
			float projectileLifetime, Vector3 spawnOffset, int pierceCount) {
			this._projectilePrefab = projectilePrefab;
			this._projectileSpeed = projectileSpeed;
			this._projectileLifetime = projectileLifetime;
			this._spawnOffset = spawnOffset;
			this._pierceCount = pierceCount;
			if (projectileLinePreviewPrefab != null) {
				this._projectileLinePreviewObject = UnityEngine.Object.Instantiate(projectileLinePreviewPrefab);
				this._projectileLinePreviewObject.SetActive(false);
			}
		}
		public override void Start(TargetingManager targetingManager, TargetContext casterContext, EffectContext effectContext, ITargetingReceiver onTargetResolved) {
			Begin(targetingManager, casterContext, effectContext, onTargetResolved);
			// Get the pooler service
			if (this._universalPooler == null) {
				GlobalServiceLocator.Instance.TryGetService(out this._universalPooler);
			}
			// this line thingy is optinal so we don't need to throw an error if it's not assigned.
			if (this._projectileLinePreviewObject != null) {
				this._projectileLinePreviewObject.SetActive(true);
			}

			if (this._projectilePrefab == null || this.targetingManager == null) {
				FinishTheStrategy();
			}
		}

		public override void FinishTheStrategy(bool canClearOnTargetResolved = true) {
			if (this._projectileLinePreviewObject != null) {
				this._projectileLinePreviewObject.SetActive(false);
			}
			base.FinishTheStrategy(canClearOnTargetResolved);
		}

		protected override bool ExecuteResolution(Vector3 clickPoint) {
			// this is a special case for the projectile strategy, where we want to use
			Vector3 origin = this.casterContext.HitBox.Transform.position;

			if (clickPoint == this.CasterPosition) {
				Debug.Log($"Caster and click point are the same. Using fallback targeting for ProjectileTargetingStrategy." +
				$"casterPosition: {this.CasterPosition}, clickPoint: {clickPoint}");
				clickPoint = FindFallBackTargetPosition(origin, this.effectContext.CasterMovement.GetLookingAtDirection());
			}
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
		private Vector3 FindFallBackTargetPosition(Vector3 origin, Vector3 lookingDirection) {
			var fallback = origin + lookingDirection.normalized * 5f;
			if (this.effectContext.Dimension == AxisMode.TwoD) fallback.z = 0f;
			return fallback;
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
				return Mathfx.GetRelativePosition2D(position, fwd, offset);
			}
			return Mathfx.GetRelativePosition3D(position, fwd, offset);
		}
	}
}
