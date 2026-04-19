using System;
using Kope.Component.Combat.Interface;
using UnityEngine;
using Kope.Component.Combat;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct VampiricDamageEffectLevelingScale {
		[Header("Scaling")]
		[Min(0f)]
		public int abilityUsedThreshold;
		[Range(0f, 1f)] public float lifeStealRatioScale;
		// Potentially could add damage scaling here as well if we want to get really crazy with it
		// For now, just life steal scaling since that's the main point of this effect
		//DamageEffectLevelScaling damageEffectLevelScaling;
	}

	[Serializable]
	public class VampiricDamageEffectFactory : IEffectFactory<ICombatable> {
		[Header("Vampire Damage")]
		[Range(0f, 1f)] public float lifeStealRatio = 0.25f;
		[Header("Damage")]
		public DamageEffectData DamageEffectData;
		[Tooltip("Scaling values for the vampiric damage effect based on ability usage." +
		"Overrides base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		public VampiricDamageEffectLevelingScale[] levelScalingValues = new VampiricDamageEffectLevelingScale[3];
		private float _cachedLifeStealRatio;
		private int _nextRecomputeThreshold = 0;
		public IEffect<ICombatable> Create(EffectContext context = default) {
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedLifeStealRatio = this.ResolveLifeStealRatio(context.AbilityUsedCount,
				out this._nextRecomputeThreshold);
			}
			return new VampiricDamageEffect(context, this._cachedLifeStealRatio, this.DamageEffectData);
		}

		private float ResolveLifeStealRatio(int useCount, out int newLevelThreshold) {
			if (this.levelScalingValues == null || this.levelScalingValues.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.lifeStealRatio;
			}

			newLevelThreshold = this.levelScalingValues[0].abilityUsedThreshold;
			for (int i = this.levelScalingValues.Length - 1; i >= 0; i--) {
				if (useCount >= this.levelScalingValues[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.levelScalingValues.Length)
						? this.levelScalingValues[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return this.levelScalingValues[i].lifeStealRatioScale <= 0f
						? this.lifeStealRatio : this.levelScalingValues[i].lifeStealRatioScale;
				}
			}
			return this.lifeStealRatio;
		}
	}

	[Serializable]
	public class VampiricDamageEffect : IEffect<ICombatable> {
		private EffectContext _context;
		private DamageEffectData _data;
		[Range(0f, 1f)] public float _lifeStealRatio;

		public VampiricDamageEffect(EffectContext context, float lifeStealRatio, DamageEffectData ded) {
			this._context = context;
			this._lifeStealRatio = lifeStealRatio;
			this._data = ded;
		}

		public float Apply(ICombatable target) {
			float dmg = this._context.CasterAttack != null
				? this._context.CasterAttack.GetDamage(this._data.ScalingStat) : this._data.pityDamage;
			var dmgDetail = new DamageDetail(
				dmg * this._data.DamageMultiplier,
				this._context.Caster,
				this._data.DamageType,
				this._data.pierceRatio,
				this._data.ignoreResistance,
				this._context.CasterLevel
			);
			float finalDamage = target.TakeHit(dmgDetail);
			if (this._context.CasterHealth != null && finalDamage > 0f && this._lifeStealRatio > 0f) {
				this._context.CasterHealth.Heal(finalDamage * this._lifeStealRatio);
			}
			return finalDamage;
		}
	}
}