using System;
using Kope.Component.HurtBox;
using Kope.Component.HurtBox.Interface;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class DamageEffectFactory : IEffectFactory<IDamageable> {
		public IEffect<IDamageable> Create(EffectContext context = default) =>
		 new DamageEffect(context.DamageDetail);
	}

	[Serializable]
	public struct DamageEffect : IEffect<IDamageable> {
		public DamageDetail detail;
		public event Action<IEffect<IDamageable>> OnCompleted;

		public DamageEffect(DamageDetail detail) {
			this.detail = detail;
			this.OnCompleted = null;
		}

		public readonly float Apply(IDamageable target) {
			float finalDamage = target.TakeHit(detail);
			this.OnCompleted?.Invoke(this);
			return finalDamage;
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}

}