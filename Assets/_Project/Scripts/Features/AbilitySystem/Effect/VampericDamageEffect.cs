using System;
using Kope.Component.Combat.Interface;
using UnityEngine;
using Kope.Component.Health.Interface;
using Kope.Component.Combat;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class VampiricDamageEffectFactory : IEffectFactory<ICombatable> {
		[Range(0f, 1f)] public float lifeStealRatio = 0.25f;

		public IEffect<ICombatable> Create(EffectContext context = default) =>
			new VampiricDamageEffect(context.DamageDetail, lifeStealRatio, context.CasterHealth);
	}

	[Serializable]
	public struct VampiricDamageEffect : IEffect<ICombatable> {
		public DamageDetail detail;
		[Range(0f, 1f)] public float lifeStealRatio;
		// even the stuct is a value type, the IHealthComponent it holds is a reference, 
		// so we can still modify the caster's health through it and heal them for the life steal portion.
		public IHealthComponent casterHealth;
		public event Action<IEffect<ICombatable>> OnCompleted;

		public VampiricDamageEffect(DamageDetail detail, float lifeStealRatio, IHealthComponent casterHealth) {
			this.detail = detail;
			this.lifeStealRatio = lifeStealRatio;
			this.casterHealth = casterHealth;
			this.OnCompleted = null;
		}

		public readonly float Apply(ICombatable target) {
			float finalDamage = target.TakeHit(detail);
			if (this.casterHealth != null && finalDamage > 0f && this.lifeStealRatio > 0f) {
				this.casterHealth.Heal(finalDamage * this.lifeStealRatio);
			}
			this.OnCompleted?.Invoke(this);
			return finalDamage;
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}


}