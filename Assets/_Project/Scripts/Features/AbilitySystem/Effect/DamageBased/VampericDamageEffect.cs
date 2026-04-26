using System;
using Kope.Component.Combat.Interface;
using UnityEngine;
using Kope.Component.Combat;

namespace Kope.AbilitySystem.Effect {

	[Serializable]
	public class VampiricDamageEffectFactory : IEffectFactory<IDamagable> {
		[Header("Vampire Damage")]
		[SerializeField, Range(0f, 1f)] private float lifeStealRatio = 0.25f;
		[Header("Damage")]
		[SerializeField] private InstantDamageEffectData DamageEffectData;

		[Tooltip("Scaling values for the vampiric damage effect based on ability usage." +
		"Overrides base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private VampiricDamageEffectLevelingScale[] levelScalingValues = new VampiricDamageEffectLevelingScale[3];
		private float _cachedLifeStealRatio;
		private int _nextRecomputeThreshold = 0;
		public IEffect<IDamagable> Create(EffectContext context = default) {
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

			newLevelThreshold = this.levelScalingValues[0].AbilityUsedThreshold;
			for (int i = this.levelScalingValues.Length - 1; i >= 0; i--) {
				if (useCount >= this.levelScalingValues[i].AbilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.levelScalingValues.Length)
						? this.levelScalingValues[i + 1].AbilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return this.levelScalingValues[i].LifeStealRatioScale <= 0f
						? this.lifeStealRatio : this.levelScalingValues[i].LifeStealRatioScale;
				}
			}
			return this.lifeStealRatio;
		}
		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.levelScalingValues == null || this.levelScalingValues.Length == 0) {
				this.levelScalingValues = new VampiricDamageEffectLevelingScale[3];
			}
		}
	}

	[Serializable]
	public class VampiricDamageEffect : IEffect<IDamagable> {
		private EffectContext _context;
		private InstantDamageEffectData _data;
		[Range(0f, 1f)] public float _lifeStealRatio;

		public VampiricDamageEffect(EffectContext context, float lifeStealRatio, InstantDamageEffectData ded) {
			this._context = context;
			this._lifeStealRatio = lifeStealRatio;
			this._data = ded;
		}

		public void Apply(IDamagable target) {
			float finalDamage = this._context.CasterAttack != null
				? this._context.CasterAttack.GetDamageValue(
					this._data.ScalingStat, this._data.DamageMultiplier
					) : this._data.PityDamage * this._data.DamageMultiplier;
			var dmgDetail = new DamageDetail(
				finalDamage,
				this._context.Caster,
				this._data.DamageType,
				this._data.PierceRatio,
				this._data.IgnoreResistance,
				this._context.CasterLevel
			);
			float receivedDamage = target.TakeHit(dmgDetail);
			if (this._context.CasterHealth != null && receivedDamage > 0f && this._lifeStealRatio > 0f) {
				this._context.CasterHealth.Heal(receivedDamage * this._lifeStealRatio);
			}
		}
	}
	[Serializable]
	public struct VampiricDamageEffectLevelingScale {
		[Header("Scaling")]
		[Min(0f)]
		public int AbilityUsedThreshold;
		[Range(0f, 1f)] public float LifeStealRatioScale;
		// Potentially could add damage scaling here as well if we want to get really crazy with it
		// For now, just life steal scaling since that's the main point of this effect
		//DamageEffectLevelScaling damageEffectLevelScaling;
	}

}