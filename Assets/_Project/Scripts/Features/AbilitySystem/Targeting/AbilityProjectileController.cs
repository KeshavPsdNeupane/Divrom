// AbilityProjectileController.cs
using System;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {

	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(Rigidbody))]
	public sealed class AbilityProjectileController : MonoBehaviour {
		[SerializeField] private bool destroyOnAnyHit = true;
		[SerializeField] private float defaultLifetime = 5f;

		private Action<TargetContext, EffectContext> _onTargetResolved;
		private Action _onProjectileFinished;
		private EffectContext _effectContext;
		private Rigidbody _body;
		private GameObject _caster;
		private bool _isInitialized;
		private bool _hasFinished;

		private void Awake() {
			this._body = GetComponent<Rigidbody>();
			GetComponent<Collider>().isTrigger = true;
		}

		public void Initialize(
			Action<TargetContext, EffectContext> onTargetResolved,
			Action onProjectileFinished,
			EffectContext effectContext,
			Vector3 direction,
			float speed,
			float lifetime) {
			this._onTargetResolved = onTargetResolved;
			this._onProjectileFinished = onProjectileFinished;
			this._effectContext = effectContext;
			this._caster = effectContext.Caster;
			this._isInitialized = true;
			this._hasFinished = false;

			if (this._body == null) this._body = GetComponent<Rigidbody>();
			this._body.useGravity = false;
			this._body.linearVelocity = direction.normalized * speed;
			Destroy(gameObject, lifetime > 0f ? lifetime : this.defaultLifetime);
		}

		private void OnTriggerEnter(Collider other) {
			if (!this._isInitialized || other == null) return;
			if (this._caster != null &&
				other.transform.root.gameObject == this._caster.transform.root.gameObject) return;

			var targetContext = TargetContext.Create(other);
			if (targetContext == null || targetContext.HitBox == null) return;

			this._onTargetResolved?.Invoke(targetContext, this._effectContext);

			if (this.destroyOnAnyHit) Destroy(gameObject);
		}

		private void OnDestroy() {
			if (this._hasFinished) return;
			this._hasFinished = true;
			this._onProjectileFinished?.Invoke();
			this._onTargetResolved = null;
			this._onProjectileFinished = null;
		}
	}
}