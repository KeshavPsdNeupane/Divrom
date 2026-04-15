using Kope.Component.Combat.Interface;
using System;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class StatModifierEffectFactory : IEffectFactory<ICombatable> {
		public StatModifier modifier;

		public IEffect<ICombatable> Create(EffectContext context = default) => new StatModifierEffect(modifier);
	}

	[Serializable]
	public class StatModifierEffect : IEffect<ICombatable> {
		public StatModifier modifier;

		public StatModifierEffect(StatModifier modifier) {
			this.modifier = modifier;
		}

		public float Apply(ICombatable target) {
			target.ApplyStatModifier(modifier);
			return 0f;
		}
	}

}