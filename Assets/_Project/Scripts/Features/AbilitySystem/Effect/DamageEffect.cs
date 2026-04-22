using System;
using Kope.Character.Stats;
using Kope.Component.Combat;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct DamageEffectData {
		[Header("Damage")]
		[Tooltip("IF the caster attack component is null, this pity damage is delt")]
		[Min(0f)]
		public float pityDamage;
		[Min(0f)]
		public float DamageMultiplier;
		public DamageType DamageType;
		public CharacterStatType ScalingStat;
		[Min(0f)]
		public float pierceRatio;
		[Min(0f)]
		public float ignoreResistance;
	}

	[Serializable]
	public class DamageEffectFactory : IEffectFactory<ICombatable> {
		[Tooltip("The base damage effect data")]
		public DamageEffectData BaseData;
		[Tooltip("Scaling data for each level. Overrides the base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		public DamageEffectLevelScaling[] nextLevelScalings = new DamageEffectLevelScaling[3];

		private DamageEffectData _cachedData;
		private int _nextRecomputeThreshold = 0;

		public IEffect<ICombatable> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.


			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
				// Debug.Log($"DamageEffectFactory: Recomputing damage effect data for" +
				// $" ability used count {context.AbilityUsedCount}/{this._nextRecomputeThreshold}.");
			}
			return new DamageEffect(context, this._cachedData);
		}

		private DamageEffectData ResolveData(int useCount, out int nextLevelThreshold) {
			if (this.nextLevelScalings == null || this.nextLevelScalings.Length == 0) {
				nextLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			nextLevelThreshold = this.nextLevelScalings[0].abilityUsedThreshold;
			for (int i = nextLevelScalings.Length - 1; i >= 0; i--) {

				if (useCount >= nextLevelScalings[i].abilityUsedThreshold) {
					nextLevelThreshold = (i + 1 < nextLevelScalings.Length)
						? nextLevelScalings[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new DamageEffectData {
						pityDamage = BaseData.pityDamage,
						DamageMultiplier = nextLevelScalings[i].Multiplier <= 0
							? BaseData.DamageMultiplier : nextLevelScalings[i].Multiplier,
						DamageType = BaseData.DamageType,
						ScalingStat = BaseData.ScalingStat,
						pierceRatio = nextLevelScalings[i].pierceRatio <= 0
							? BaseData.pierceRatio : nextLevelScalings[i].pierceRatio,
						ignoreResistance = nextLevelScalings[i].ignoreResistance <= 0
							? BaseData.ignoreResistance : nextLevelScalings[i].ignoreResistance
					};
				}
			}
			return this.BaseData;
		}
	}

	[Serializable]
	public struct DamageEffectLevelScaling {
		[Header("Scaling")]
		[Tooltip("The number of times the ability must be used to trigger this scaling")]
		[Min(0f)]
		public int abilityUsedThreshold;
		[Min(0f)]
		public float Multiplier;
		[Min(0f)]
		public float pierceRatio;
		[Min(0f)]
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