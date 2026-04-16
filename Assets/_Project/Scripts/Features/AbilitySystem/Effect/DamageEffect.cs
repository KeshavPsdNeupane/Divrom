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
		public DamageEffectData Data;
		public IEffect<ICombatable> Create(EffectContext context = default) =>
		 new DamageEffect(context, this.Data);
	}

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