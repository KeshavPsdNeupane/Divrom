using Kope.Character.Stats;
using Kope.Component.Combat.Interface;
using System;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class StatModifierEffectFactory : IEffectFactory<IStatSystem> {
		public StatModifier modifier;

		public IEffect<IStatSystem> Create(EffectContext context = default) => new StatModifierEffect(modifier);
	}

	[Serializable]
	public class StatModifierEffect : IEffect<IStatSystem> {
		public StatModifier modifier;

		public StatModifierEffect(StatModifier modifier) {
			this.modifier = modifier;
		}

		public float Apply(IStatSystem target) {
			target.AddStatModifier(modifier);
			return 0f;
		}
	}

}