using System;
using Kope.Component.Combat.Interface;
using Kope.Core.Sensor;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	public sealed class AbilityProjectileController : SensorBase {
		// Why does this controller handle the pooler release rather than actual strategy that spawns it?
		// 
		// Projectiles are "Autonomous Objects." Once fired, they travel independently of the 
		// caster or the strategy that created them. They possess their own internal state 
		// (velocity, pierce count, and lifetime). 
		//
		// By encapsulating the 'Release' logic here, we ensure that no matter how the projectile 
		// ends—whether by timing out, hitting a wall, or impacting a target—it is always 
		// responsible for its own cleanup. This prevents the Strategy from having to "track" 
		// dozens of active projectiles, significantly reducing memory overhead and complexity.
		[SerializeField] private Rigidbody2D projectileRigidbody;
		[SerializeField] private bool destroyOnAnyHit = true;

		private int _piercesRemaining;
		private ITargetingReceiver _onTargetResolved;
		private EffectContext _effectContext;
		private Rigidbody2D _body;
		private bool _isInitialized;
		private bool _hasFinished;

		float _timer;
		float _lifetime;

		public event Action<GameObject> OnProjectileRelease;

		public override void OnStart() {
			this._body = this.projectileRigidbody ?? GetComponent<Rigidbody2D>();
			if (this._body != null) this._body.gravityScale = 0f;
		}

		public void Initialize(
			ITargetingReceiver onTargetResolved,
			EffectContext effectContext,
			Vector3 direction,
			float speed,
			float lifetime,
			int pierceCount) {

			this._onTargetResolved = onTargetResolved;
			this._effectContext = effectContext;
			this._isInitialized = true;
			this._hasFinished = false;
			this._piercesRemaining = pierceCount;

			if (this._body == null) this._body = GetComponent<Rigidbody2D>();
			this._body.linearVelocity = direction.normalized * speed;

			this._lifetime = lifetime;
			this._timer = 0f;
		}

		private void Update() {
			if (!this._isInitialized || this._hasFinished) return;

			this._timer += Time.deltaTime;
			if (this._timer >= this._lifetime) {
				ReleaseProjectile();
			}
		}

		public override void OnDetect(Collider2D other) {
			if (!this._isInitialized || this._hasFinished || other == null) return;

			var caster = this._effectContext.Caster;
			if (caster != null && other.transform.root == caster.transform.root) return;

			var targetContext = TargetContext.Create(other);
			if (targetContext == null || targetContext.HitBox == null) return;

			this._onTargetResolved?.OnTargetingResolved(targetContext, this._effectContext);

			if (this._piercesRemaining > 0) {
				this._piercesRemaining--;
			} else if (this.destroyOnAnyHit) {
				ReleaseProjectile();
			}
		}

		private void ReleaseProjectile() {
			if (this._hasFinished) return;
			this._hasFinished = true;
			this._timer = 0f;
			this._body.linearVelocity = Vector2.zero;
			this.OnProjectileRelease?.Invoke(this.gameObject);
		}
	}
}