using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using UnityEngine;


namespace Kope.AbilitySystem.Effect {
	// the target selection is handled by the ability and the effect factory just creates the effect based on 
	// the context passed in, so the effect itself doesn't need to know/it doesn't care
	// about the context of who the caster is or who the target

	[Serializable]
	public struct HealEffectData {
		[Header("Healing")]
		[Min(0f)]
		public float flatHealAmount;
		[Range(0f, 1f)] public float healPercentage;

	}
	[Serializable]
	public struct HealEffectLevelScaling {
		[Header("Scaling")]
		[Min(0f)]
		public int abilityUsedThreshold;
		[Min(0f)]
		public float flatHealAmount;
		[Range(0f, 1f)] public float healPercentage;
	}

	[Serializable]
	public class HealEffectFactory : IEffectFactory<IHealable> {
		public HealEffectData BaseData;
		[Tooltip("Scaling data for the heal effect. Overrides the base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		public HealEffectLevelScaling[] LevelScaling = new HealEffectLevelScaling[3];
		private HealEffectData _cachedData;
		private int _nextRecomputeThreshold = -1;

		IEffect<IHealable> IEffectFactory<IHealable>.Create(EffectContext context) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.

			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = this.ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
				// 	Debug.Log($"DamageEffectFactory: Recomputing damage effect data for" +
				// $" ability used count {context.AbilityUsedCount}/{this._nextRecomputeThreshold}.");
			}
			return new HealEffect(this._cachedData);
		}

		private HealEffectData ResolveData(int useCount, out int newLevelThreshold) {
			if (this.LevelScaling == null || this.LevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			newLevelThreshold = this.LevelScaling[0].abilityUsedThreshold;
			for (int i = this.LevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.LevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.LevelScaling.Length)
						? this.LevelScaling[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new HealEffectData {
						flatHealAmount = this.LevelScaling[i].flatHealAmount <= 0
							? this.BaseData.flatHealAmount : this.LevelScaling[i].flatHealAmount,
						healPercentage = this.LevelScaling[i].healPercentage <= 0
							? this.BaseData.healPercentage : this.LevelScaling[i].healPercentage
					};

				}
			}
			return this.BaseData;
		}
	}
	[Serializable]
	public class HealEffect : IEffect<IHealable> {
		private readonly float flatHealAmount;
		private readonly float healPercentage;

		public HealEffect(HealEffectData data) {
			this.flatHealAmount = data.flatHealAmount;
			this.healPercentage = data.healPercentage;
		}
		public float Apply(IHealable target) {
			target.Heal(flatHealAmount, healPercentage);
			return 0;
		}
	}

}