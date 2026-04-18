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
		public float flatHealAmount;
		[Range(0f, 1f)] public float healPercentage;

	}
	[Serializable]
	public struct HealEffectLevelScaling {
		public int abilityUsedThreshold;
		public float flatHealAmount;
		[Range(0f, 1f)] public float healPercentage;
	}

	[Serializable]
	public class HealEffectFactory : IEffectFactory<IHealable> {
		public HealEffectData BaseData;
		[Tooltip("Level scaling for the heal effect,Will override base data when threshold is met")]
		public HealEffectLevelScaling[] LevelScaling = new HealEffectLevelScaling[3];
		private HealEffectData _cachedData;
		private int _cachedNewLevelThreshold = 0;
		IEffect<IHealable> IEffectFactory<IHealable>.Create(EffectContext context) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				this._cachedData = this.ResolveData(context.AbilityUsedCount, out this._cachedNewLevelThreshold);
			}
			return new HealEffect(this._cachedData);
		}
		private HealEffectData ResolveData(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.LevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.LevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = this.LevelScaling[i].abilityUsedThreshold;
					return new HealEffectData {
						flatHealAmount = this.LevelScaling[i].flatHealAmount,
						healPercentage = this.LevelScaling[i].healPercentage
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