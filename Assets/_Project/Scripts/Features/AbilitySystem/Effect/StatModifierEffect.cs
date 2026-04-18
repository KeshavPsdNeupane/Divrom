using System;
using Kope.Character.Stats;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct StatMofifierEffectLevelingScale {
		public int abilityUsedThreshold;
		public float modifierAmount;
		[Range(-1f, 100f)]
		public float duration;
	}

	[Serializable]
	public class StatModifierEffectFactory : IEffectFactory<IStatSystem> {
		public StatModifier BaseModifier;
		public StatMofifierEffectLevelingScale[] levelScalingValues = new StatMofifierEffectLevelingScale[3];
		private StatModifier _cachedModifier;
		private int _cachedNewLevelThreshold = 0;
		public IEffect<IStatSystem> Create(EffectContext context = default) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				this._cachedModifier = this.ResolveModifier(context.AbilityUsedCount,
				out this._cachedNewLevelThreshold);
			}
			return new StatModifierEffect(this._cachedModifier);
		}
		private StatModifier ResolveModifier(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.levelScalingValues.Length - 1; i >= 0; i--) {
				if (useCount >= this.levelScalingValues[i].abilityUsedThreshold) {
					newLevelThreshold = this.levelScalingValues[i].abilityUsedThreshold;
					return new StatModifier(
						this.BaseModifier.source,
						this.BaseModifier.effectName,
						this.BaseModifier.statType,
						this.levelScalingValues[i].modifierAmount,
						this.levelScalingValues[i].duration,
						this.BaseModifier.isPercentage,
						this.BaseModifier.isDebuffFromArmor,
						this.BaseModifier.isDebuffFromEnemy,
						this.BaseModifier.debuffPriority,
						this.BaseModifier.description);
				}
			}
			return this.BaseModifier;
		}
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