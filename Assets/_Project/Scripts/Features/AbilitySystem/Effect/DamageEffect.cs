using System;
using Kope.Component.Combat;
using Kope.Component.Combat.Interface;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class DamageEffectFactory : IEffectFactory<ICombatable> {
		public IEffect<ICombatable> Create(EffectContext context = default) =>
		 new DamageEffect(context.DamageDetail);
	}

	[Serializable]
	public struct DamageEffect : IEffect<ICombatable> {
		public DamageDetail detail;
		public event Action<IEffect<ICombatable>> OnCompleted;

		public DamageEffect(DamageDetail detail) {
			this.detail = detail;
			this.OnCompleted = null;
		}

		public readonly float Apply(ICombatable target) {
			float finalDamage = target.TakeHit(detail);
			this.OnCompleted?.Invoke(this);
			return finalDamage;
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}

}