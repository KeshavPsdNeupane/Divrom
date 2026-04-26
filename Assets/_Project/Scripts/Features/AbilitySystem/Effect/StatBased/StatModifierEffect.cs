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
		[SerializeField] private StatModifier BaseModifier;
		[Tooltip("Scaling values for the stat modifier based on ability usage." +
		"Overrides base modifier when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private StatMofifierEffectLevelingScale[] nextlevelScaling = new StatMofifierEffectLevelingScale[3];
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
			if (this.nextlevelScaling == null || this.nextlevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseModifier;
			}

			newLevelThreshold = this.nextlevelScaling[0].abilityUsedThreshold;
			for (int i = this.nextlevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.nextlevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.nextlevelScaling.Length)
						? this.nextlevelScaling[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new StatModifier(
						this.BaseModifier.source,
						this.BaseModifier.effectName,
						this.BaseModifier.statType,
						this.nextlevelScaling[i].modifierAmount <= 0
							? this.BaseModifier.modifierAmount : this.nextlevelScaling[i].modifierAmount,
						this.nextlevelScaling[i].duration <= 0
							? this.BaseModifier.totalDuration : this.nextlevelScaling[i].duration,
						this.BaseModifier.isPercentage,
						this.BaseModifier.isDebuffFromArmor,
						this.BaseModifier.isDebuffFromEnemy,
						this.BaseModifier.debuffPriority,
						this.BaseModifier.description);
				}
			}
			return this.BaseModifier;
		}
		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.nextlevelScaling == null || this.nextlevelScaling.Length == 0) {
				this.nextlevelScaling = new StatMofifierEffectLevelingScale[3];
			}
		}
	}

	[Serializable]
	public class StatModifierEffect : IEffect<IStatSystem> {
		public StatModifier modifier;


		public StatModifierEffect(StatModifier modifier) {
			this.modifier = modifier;
		}
		public void Apply(IStatSystem target) {
			target.AddStatModifier(modifier);

		}
	}

}