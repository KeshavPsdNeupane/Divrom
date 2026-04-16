using System;
using Kope.Component.Attack;
using Kope.Component.Health.Interface;
using Kope.Component.HitBox.Interface;
using UnityEngine;

namespace Kope.Component.Combat.Interface {
	public struct EffectContext {
		public Vector3 HitPoint;      // For Knockback calculation
		public int CasterLevel;       // For Level Scaling math
		public GameObject Caster;     // For attribution (who killed who)
		public IAttackComponent CasterAttack; // For scaling damage (ATK/SP)
		public IHealable CasterHealth; // For "Vampire" or "Thorn" effects
	}

	public interface ICombatable {
		IHurtBoxComponent HurtBox { get; }
		float TakeHit(DamageDetail damageDetail);
		void TakeDamageDebugOnly(int amount);
	}

	public interface IDamageProcessor : ICombatable { }

	public interface IEffectFactory<TTarget> {
		IEffect<TTarget> Create(EffectContext context = default);
	}

	public interface IEffect<TTarget> {
		float Apply(TTarget target);
	}

	public interface ITickableEffect {
		event Action<ITickableEffect> OnCompletedOrCancell;
		void Tick(float deltaTime);
		void Cancel();
	}
}