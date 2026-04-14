using Kope.Component.Combat.Interface;
using System;
using Kope.Component.Combat;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class KnockbackEffectFactory : IEffectFactory<ICombatable> {
		public KnockbackDetail Detail;

		public IEffect<ICombatable> Create(EffectContext context = default)
		=> new KnockbackEffect(this.Detail);
	}

	[Serializable]
	public struct KnockbackEffect : IEffect<ICombatable> {

		public readonly KnockbackDetail Detail;
		public event Action<IEffect<ICombatable>> OnCompleted;
		public KnockbackEffect(KnockbackDetail detail) {
			this.Detail = detail;
			this.OnCompleted = null;

		}
		public readonly float Apply(ICombatable target) {
			var dir = this.Detail.IsPulling ? -this.Detail.KnockbackDirection.normalized : this.Detail.KnockbackDirection.normalized;
			target.ApplyKnockback(dir, this.Detail.Duration, this.Detail.KnockbackStrength);
			// the life cycle is managed by the target's Movement component, so we just invoke completion here
			// since we have no timers to manage.
			this.OnCompleted?.Invoke(this);
			return 0f;
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}
}