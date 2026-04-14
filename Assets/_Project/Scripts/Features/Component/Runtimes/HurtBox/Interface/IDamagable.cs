using System;
using Kope.Component.Health.Interface;
using UnityEngine;

namespace Kope.Component.HurtBox.Interface {
	public interface IDamageable {
		float TakeHit(DamageDetail damageDetail);
		void TakeDamageDebugOnly(int amount);
		bool ApplyStatModifier(StatModifier effect);
		void ApplyEffect(IEffect<IDamageable> effect);
		void ApplyKnockback(Vector3 direction, float duration, float impulse);
	}
	/// <summary>
	/// Context passed on effect from ability to effect factory, allowing for dynamic effect
	///  creation based on ability state (like caster position for knockback).
	/// </summary>
	public struct EffectContext {
		// no constructor since the fields are often optional and can be set in any combination
		// depending on the effect being created, so we can just use object initializer 
		// syntax when creating the context.
		public DamageDetail DamageDetail;
		public Vector3 KnockbackDirection;
		// even if the struct is a value type, the IHealthComponent it holds is a reference, 
		// so we can still modify the caster's health through it and heal them for the life steal 
		// portion in the VampiricDamageEffect.
		public IHealthComponent CasterHealth;
		// removed damageMultipler since it should be ability specific and can be applied 
		// on the damage amount before creating the effect context and passing it to the factory.
	}


	public interface IEffectFactory<TTarget> {
		IEffect<TTarget> Create(EffectContext context = default);
	}

	public interface IEffect<TTarget> {
		float Apply(TTarget target);
		void Cancel();
		event Action<IEffect<TTarget>> OnCompleted;
	}
	/// <summary>
	/// Interface for effects that require per-frame updates (like DoT timers).
	/// </summary>
	public interface ITickableEffect {
		void Tick(float deltaTime);
	}

}