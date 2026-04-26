using System;
using Kope.Character.Stats;
using Kope.Component.Combat;
using Kope.Component.Combat.Interface;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class InstantDamageEffectFactory : IEffectFactory<IDamagable> {
		[Tooltip("The base damage effect data")]
		[SerializeField] private InstantDamageEffectData BaseData;
		[Tooltip("Scaling data for each level. Overrides the base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private DamageEffectLevelScaling[] nextLevelScalings = new DamageEffectLevelScaling[3];

		private InstantDamageEffectData _cachedData;
		private int _nextRecomputeThreshold = 0;

		public IEffect<IDamagable> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.


			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
			}
			return new DamageEffect(context, this._cachedData);
		}

		private InstantDamageEffectData ResolveData(int useCount, out int nextLevelThreshold) {
			if (this.nextLevelScalings == null || this.nextLevelScalings.Length == 0) {
				nextLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			nextLevelThreshold = this.nextLevelScalings[0].AbilityUsedThreshold;
			for (int i = nextLevelScalings.Length - 1; i >= 0; i--) {

				if (useCount >= nextLevelScalings[i].AbilityUsedThreshold) {
					nextLevelThreshold = (i + 1 < nextLevelScalings.Length)
						? nextLevelScalings[i + 1].AbilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new InstantDamageEffectData {
						PityDamage = BaseData.PityDamage,
						DamageMultiplier = nextLevelScalings[i].Multiplier <= 0
							? BaseData.DamageMultiplier : nextLevelScalings[i].Multiplier,
						DamageType = BaseData.DamageType,
						ScalingStat = BaseData.ScalingStat,
						PierceRatio = nextLevelScalings[i].PierceRatio <= 0
							? BaseData.PierceRatio : nextLevelScalings[i].PierceRatio,
						IgnoreResistance = nextLevelScalings[i].IgnoreResistance <= 0
							? BaseData.IgnoreResistance : nextLevelScalings[i].IgnoreResistance
					};
				}
			}
			return this.BaseData;
		}
		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.nextLevelScalings == null || this.nextLevelScalings.Length == 0) {
				this.nextLevelScalings = new DamageEffectLevelScaling[3];
			}
		}
	}

	// due to the interface, if this was a struct it would cause boxing and unboxing issues, 
	// so we make it a class to avoid that. and since it is a small class that only contains a few fields,
	// the performance impact should be minimal.
	[Serializable]
	public class DamageEffect : IEffect<IDamagable> {
		private EffectContext context;
		private InstantDamageEffectData _data;

		public DamageEffect(EffectContext context, InstantDamageEffectData data) {
			this.context = context;
			this._data = data;
		}

		public void Apply(IDamagable target) {
			float finalDamage = this.context.CasterAttack != null
				? this.context.CasterAttack.GetDamageValue(
					this._data.ScalingStat, this._data.DamageMultiplier
					) : this._data.PityDamage * this._data.DamageMultiplier;

			var dmgDetail = new DamageDetail(
				finalDamage,
				this.context.Caster,
				this._data.DamageType,
				this._data.PierceRatio,
				this._data.IgnoreResistance,
				this.context.CasterLevel
				);
			_ = target.TakeHit(dmgDetail);
		}
	}





	[Serializable]
	public struct InstantDamageEffectData {
		[Header("Damage")]
		[Tooltip("IF the caster attack component is null, this pity damage is delt")]
		[Min(0f)]
		public float PityDamage;
		[Min(0f)]
		public float DamageMultiplier;
		public DamageType DamageType;
		public CharacterStatType ScalingStat;
		[Min(0f)]
		public float PierceRatio;
		[Min(0f)]
		public float IgnoreResistance;
	}

	[Serializable]
	public struct DamageEffectLevelScaling {
		[Header("Scaling")]
		[Tooltip("The number of times the ability must be used to trigger this scaling")]
		[Min(0f)]
		public int AbilityUsedThreshold;
		[Min(0f)]
		public float Multiplier;
		[Min(0f)]
		public float PierceRatio;
		[Min(0f)]
		public float IgnoreResistance;
	}



}