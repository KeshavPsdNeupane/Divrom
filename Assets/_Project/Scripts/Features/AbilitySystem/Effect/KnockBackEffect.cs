using UnityEngine;
using Kope.Component.HurtBox.Interface;
using System;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class KnockbackEffectFactory : IEffectFactory<IDamageable> {
		[Tooltip("If true the target will be pulled toward the point of origin. " +
		"Not a full blackhole pull, but more of a directional pull that still respects the direction vector, " +
		"just in the opposite direction. Useful for things like a hookshot or a tornado lift that pulls enemies up into the air. " +
		"If false, the target will be pushed away from the point of origin like a traditional knockback.")]
		public bool isPulling = false;
		public float duration = 0.2f;
		// strength of the knockback, to be multiplied with the direction vector.
		// allows for more flexible knockback forces without needing to change the direction vector computation 
		// in the ability.
		public float impulse = 1f;

		public IEffect<IDamageable> Create(EffectContext context = default) => new KnockbackEffect(
			context.KnockbackDirection, duration, impulse, isPulling
		);
	}

	[Serializable]
	public struct KnockbackEffect : IEffect<IDamageable> {
		/// <summary>
		/// dir must be computed on the ability side since it often depends on the caster's 
		/// position relative to the target. or if it is a bomb explosion, the center of the explosion 
		/// relative to the target.
		/// that why the caster position is removed from the context and replaced with a knockback
		/// direction that the ability can compute and pass in when creating the effect.
		/// </summary>
		public readonly Vector3 knockbackDirection;
		public readonly float duration;
		public readonly float impulse;
		public readonly bool isPulling;
		public event Action<IEffect<IDamageable>> OnCompleted;
		public KnockbackEffect(Vector3 knockbackDirection, float duration, float impulse, bool isPulling) {
			this.knockbackDirection = knockbackDirection;
			this.duration = duration;
			this.impulse = impulse;
			this.isPulling = isPulling;
			this.OnCompleted = null;
		}

		public readonly float Apply(IDamageable target) {
			var dir = isPulling ? -knockbackDirection.normalized : knockbackDirection.normalized;
			target.ApplyKnockback(dir, this.duration, this.impulse);
			// the life cycle is managed by the target's Movement component, so we just invoke completion here
			// since we have no timers to manage.
			this.OnCompleted?.Invoke(this);
			return 0f;
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}
}