using System;
using Kope.Component.Combat.Interface;
using UnityEngine;
using Kope.Component.Combat;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class VampiricDamageEffectFactory : IEffectFactory<ICombatable> {
		[Range(0f, 1f)] public float lifeStealRatio = 0.25f;
		public DamageEffectData DamageEffectData;
		public IEffect<ICombatable> Create(EffectContext context = default) =>
			new VampiricDamageEffect(context, lifeStealRatio, DamageEffectData);
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