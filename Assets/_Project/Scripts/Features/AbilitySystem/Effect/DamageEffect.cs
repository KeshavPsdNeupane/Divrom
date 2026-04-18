using System;
using Kope.Character.Stats;
using Kope.Component.Combat;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct DamageEffectData {
		[Tooltip("IF the caster attack component is null, this pity damage is delt")]
		public float pityDamage;
		public float DamageMultiplier;
		public DamageType DamageType;
		public CharacterStatType ScalingStat;
		public float pierceRatio;
		public float ignoreResistance;
	}

	[Serializable]
	public class DamageEffectFactory : IEffectFactory<ICombatable> {
		[Tooltip("The base damage effect data")]
		public DamageEffectData BaseData;
		[Tooltip("The scaling data for each level, will override the base data if the " +
		"ability used count meets the threshold")]
		public DamageEffectLevelScaling[] nextLevelScalings = new DamageEffectLevelScaling[3];

		private DamageEffectData _cachedData;
		private int _cachedNewLevelThreshold = 0;

		public IEffect<ICombatable> Create(EffectContext context = default) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				// resolve new data if the ability used count meets the threshold, otherwise use the cached data
				// and also update the cached threshold to avoid unnecessary checks in the future.
				// for 1 time it is O(n), but after that it will be O(1) since the data is cached
				// and the threshold is updated. until the count is reset, then it will be O(n) again 
				// for the first time.
				this._cachedData = ResolveData(context.AbilityUsedCount, out this._cachedNewLevelThreshold);
			}
			return new DamageEffect(context, this._cachedData);
		}

		private DamageEffectData ResolveData(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.nextLevelScalings.Length - 1; i >= 0; i--) {
				if (useCount >= this.nextLevelScalings[i].abilityUsedThreshold) {
					newLevelThreshold = this.nextLevelScalings[i].abilityUsedThreshold;
					return new DamageEffectData {
						pityDamage = this.BaseData.pityDamage,
						DamageMultiplier = this.nextLevelScalings[i].Multiplier,
						DamageType = this.BaseData.DamageType,
						ScalingStat = this.BaseData.ScalingStat,
						pierceRatio = this.nextLevelScalings[i].pierceRatio,
						ignoreResistance = this.nextLevelScalings[i].ignoreResistance
					};
				}
			}

			return this.BaseData;
		}
	}

	[Serializable]
	public struct DamageEffectLevelScaling {
		[Tooltip("The number of times the ability must be used to trigger this scaling")]
		public int abilityUsedThreshold;
		public float Multiplier;
		public float pierceRatio;
		public float ignoreResistance;
	}


	// due to the interface, if this was a struct it would cause boxing and unboxing issues, 
	// so we make it a class to avoid that. and since it is a small class that only contains a few fields,
	// the performance impact should be minimal.
	[Serializable]
	public class DamageEffect : IEffect<ICombatable> {
		private EffectContext context;
		private DamageEffectData _data;

		public DamageEffect(EffectContext context, DamageEffectData data) {
			this.context = context;
			this._data = data;
		}

		public float Apply(ICombatable target) {
			float dmg = this.context.CasterAttack != null
				? this.context.CasterAttack.GetDamage(this._data.ScalingStat) : this._data.pityDamage;

			var dmgDetail = new DamageDetail(
				dmg * this._data.DamageMultiplier,
				this.context.Caster,
				this._data.DamageType,
				this._data.pierceRatio,
				this._data.ignoreResistance,
				this.context.CasterLevel
				);
			float finalDamage = target.TakeHit(dmgDetail);
			return finalDamage;
		}
	}

}