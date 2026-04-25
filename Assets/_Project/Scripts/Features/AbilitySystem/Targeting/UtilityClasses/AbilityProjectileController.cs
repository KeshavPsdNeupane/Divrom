using System;
using Kope.Component.Combat.Interface;
using Kope.Core.Extensions;
using Kope.Core.Sensor;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	[RequireComponent(typeof(Collider2D))]
	[RequireComponent(typeof(Rigidbody2D))]
	public sealed class AbilityProjectileController : SensorBase {
		[SerializeField] private Rigidbody2D projectileRigidbody;
		[SerializeField] private bool destroyOnAnyHit = true;
		[SerializeField, Tooltip("The default lifetime of the projectile in seconds.")]
		private float defaultLifetime = 5f;
		private int _pierceCount = 0;

		private int _piercesRemaining;
		private Action<TargetContext, EffectContext> _onTargetResolved;
		private Action _onProjectileFinished;
		private EffectContext _effectContext;
		private Rigidbody2D _body;
		private bool _isInitialized;
		private bool _hasFinished;

		public override void OnStart() {
			var hiearchyMessage = this.GetFullHierarchyPath();
			if (this.projectileRigidbody == null) {
				Debug.LogError($"AbilityProjectileController on {gameObject.name} has no Rigidbody2D assigned. {hiearchyMessage}");
				Destroy(gameObject);
				return;
			}
			this._body = this.projectileRigidbody;
			this._body.gravityScale = 0f;
		}

		public void Initialize(
			Action<TargetContext, EffectContext> onTargetResolved,
			Action onProjectileFinished,
			EffectContext effectContext,
			Vector3 direction,
			float speed,
			float lifetime,
			int pierceCount = 0
			) {
			this._onTargetResolved = onTargetResolved;
			this._onProjectileFinished = onProjectileFinished;
			this._effectContext = effectContext;
			this._isInitialized = true;
			this._hasFinished = false;
			this._pierceCount = pierceCount;

			if (this._body == null) this._body = GetComponent<Rigidbody2D>();
			this._body.gravityScale = 0f;
			this._body.linearVelocity = direction.normalized * speed;
			this._piercesRemaining = this._pierceCount;
			Destroy(gameObject, lifetime > 0f ? lifetime : this.defaultLifetime);
		}
		private void OnDestroy() {
			if (this._hasFinished) return;
			this._hasFinished = true;
			this._onTargetResolved = null;
			// so we wont try to call the callback on a destroyed projectile if it hits something 
			// at the same frame it's destroyed, which can happen with fast projectiles.
			if (gameObject.scene.isLoaded) {
				this._onProjectileFinished?.Invoke();
			}
			this._onProjectileFinished = null;
		}

		public override void OnDetect(Collider2D other) {
			if (!this._isInitialized || other == null) return;

			var caster = this._effectContext.Caster;
			if (caster != null && other.transform.root.gameObject == caster.transform.root.gameObject) return;

			var targetContext = TargetContext.Create(other);
			if (targetContext == null || targetContext.HitBox == null) return;

			this._onTargetResolved?.Invoke(targetContext, this._effectContext);

			if (this._piercesRemaining > 0) {
				this._piercesRemaining--;
			} else if (this.destroyOnAnyHit) {
				Destroy(gameObject);
			}
		}
	}
}