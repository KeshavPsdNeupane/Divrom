using Kope.Component.Combat.Interface;
using System;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class StatModifierEffectFactory : IEffectFactory<ICombatable> {
		public StatModifier modifier;

		public IEffect<ICombatable> Create(EffectContext context = default) => new StatModifierEffect(modifier);
	}

	[Serializable]
	public struct StatModifierEffect : IEffect<ICombatable> {
		public StatModifier modifier;
		public event Action<IEffect<ICombatable>> OnCompleted;

		public StatModifierEffect(StatModifier modifier) {
			this.modifier = modifier;
			this.OnCompleted = null;
		}

		public readonly float Apply(ICombatable target) {
			target.ApplyStatModifier(modifier);
			this.OnCompleted?.Invoke(this);
			return 0f;
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}

}