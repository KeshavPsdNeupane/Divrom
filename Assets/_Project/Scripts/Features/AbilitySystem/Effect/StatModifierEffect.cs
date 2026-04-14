using Kope.Component.HurtBox.Interface;
using System;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class StatModifierEffectFactory : IEffectFactory<IDamageable> {
		public StatModifier modifier;

		public IEffect<IDamageable> Create(EffectContext context = default) => new StatModifierEffect(modifier);
	}

	[Serializable]
	public struct StatModifierEffect : IEffect<IDamageable> {
		public StatModifier modifier;
		public event Action<IEffect<IDamageable>> OnCompleted;

		public StatModifierEffect(StatModifier modifier) {
			this.modifier = modifier;
			this.OnCompleted = null;
		}

		public readonly float Apply(IDamageable target) {
			target.ApplyStatModifier(modifier);
			this.OnCompleted?.Invoke(this);
			return 0f;
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}

}