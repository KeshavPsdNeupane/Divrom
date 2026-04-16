using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.Component.Ability.Targeting {
	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(Rigidbody))]
	public sealed class AbilityProjectileController : MonoBehaviour {
		[SerializeField] private bool destroyOnAnyHit = true;
		[SerializeField] private float defaultLifetime = 5f;

		private AbilityBase ability;
		private EffectContext effectContext;
		private Rigidbody body;
		private GameObject caster;
		private bool isInitialized;

		private void Awake() {
			this.body = GetComponent<Rigidbody>();
			var collider = GetComponent<Collider>();
			collider.isTrigger = true;
		}

		public void Initialize(AbilityBase ability, EffectContext effectContext, Vector3 direction, float speed, float lifetime) {
			this.ability = ability;
			this.effectContext = effectContext;
			this.caster = effectContext.Caster;
			this.isInitialized = true;
			var resolvedLifetime = lifetime > 0f ? lifetime : this.defaultLifetime;

			if (this.body == null) {
				this.body = GetComponent<Rigidbody>();
			}

			this.body.useGravity = false;
			this.body.linearVelocity = direction.normalized * speed;
			Destroy(gameObject, resolvedLifetime);
		}

		private void OnTriggerEnter(Collider other) {
			if (!this.isInitialized || other == null) return;
			if (this.caster != null && other.transform.root.gameObject == this.caster.transform.root.gameObject) return;

			var targetContext = TargetContext.Create(other);
			if (targetContext.DamageTarger != null) {
				this.ability.Execute(targetContext, this.effectContext);
			}
			if (this.destroyOnAnyHit) {
				Destroy(gameObject);
			}
		}
	}
}