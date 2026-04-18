using System;
using Kope.Component.Combat.Interface;
using UnityEngine;
using Kope.Component.Combat;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct VampiricDamageEffectLevelingScale {
		public int abilityUsedThreshold;
		[Range(0f, 1f)] public float lifeStealRatioScale;
		// Potentially could add damage scaling here as well if we want to get really crazy with it
		// For now, just life steal scaling since that's the main point of this effect
		//DamageEffectLevelScaling damageEffectLevelScaling;
	}

	[Serializable]
	public class VampiricDamageEffectFactory : IEffectFactory<ICombatable> {
		[Range(0f, 1f)] public float lifeStealRatio = 0.25f;
		public DamageEffectData DamageEffectData;
		public VampiricDamageEffectLevelingScale[] levelScalingValues = new VampiricDamageEffectLevelingScale[3];
		private float _cachedLifeStealRatio;
		private int _cachedNewLevelThreshold = 0;
		public IEffect<ICombatable> Create(EffectContext context = default) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				this._cachedLifeStealRatio = this.ResolveLifeStealRatio(context.AbilityUsedCount,
				out this._cachedNewLevelThreshold);
			}
			return new VampiricDamageEffect(context, this._cachedLifeStealRatio, this.DamageEffectData);
		}

		private float ResolveLifeStealRatio(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.levelScalingValues.Length - 1; i >= 0; i--) {
				if (useCount >= this.levelScalingValues[i].abilityUsedThreshold) {
					newLevelThreshold = this.levelScalingValues[i].abilityUsedThreshold;
					return this.levelScalingValues[i].lifeStealRatioScale;
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