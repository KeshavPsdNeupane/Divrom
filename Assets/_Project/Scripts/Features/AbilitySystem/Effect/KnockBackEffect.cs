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
	public class KnockbackEffect : IEffect<ICombatable> {

		public readonly KnockbackDetail Detail;

		public KnockbackEffect(KnockbackDetail detail) {
			this.Detail = detail;
		}
		public float Apply(ICombatable target) {
			var dir = this.Detail.IsPulling ? -this.Detail.KnockbackDirection.normalized : this.Detail.KnockbackDirection.normalized;
			target.ApplyKnockback(dir, this.Detail.Duration, this.Detail.KnockbackStrength);
			return 0f;
		}
	}
}