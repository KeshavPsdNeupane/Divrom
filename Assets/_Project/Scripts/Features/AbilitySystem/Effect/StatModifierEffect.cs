using System;
using Kope.Character.Stats;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct StatMofifierEffectLevelingScale {
		[Header("Scaling")]
		[Min(0f)]
		public int abilityUsedThreshold;
		[Header("Modifier")]
		public float modifierAmount;
		[Range(-1f, 100f)]
		public float duration;
	}

	[Serializable]
	public class StatModifierEffectFactory : IEffectFactory<IStatSystem> {
		[Header("Stat Modifier")]
		public StatModifier BaseModifier;
		[Tooltip("Scaling values for the stat modifier based on ability usage." +
		"Overrides base modifier when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		public StatMofifierEffectLevelingScale[] levelScalingValues = new StatMofifierEffectLevelingScale[3];
		private StatModifier _cachedModifier;
		private int _nextRecomputeThreshold = 0;


		public IEffect<IStatSystem> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedModifier = this.ResolveModifier(context.AbilityUsedCount,
				out this._nextRecomputeThreshold);
			}
			return new StatModifierEffect(this._cachedModifier);
		}

		private StatModifier ResolveModifier(int useCount, out int newLevelThreshold) {
			if (this.levelScalingValues == null || this.levelScalingValues.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseModifier;
			}

			newLevelThreshold = this.levelScalingValues[0].abilityUsedThreshold;
			for (int i = this.levelScalingValues.Length - 1; i >= 0; i--) {
				if (useCount >= this.levelScalingValues[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.levelScalingValues.Length)
						? this.levelScalingValues[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new StatModifier(
						this.BaseModifier.source,
						this.BaseModifier.effectName,
						this.BaseModifier.statType,
						this.levelScalingValues[i].modifierAmount <= 0
							? this.BaseModifier.modifierAmount : this.levelScalingValues[i].modifierAmount,
						this.levelScalingValues[i].duration <= 0
							? this.BaseModifier.totalDuration : this.levelScalingValues[i].duration,
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