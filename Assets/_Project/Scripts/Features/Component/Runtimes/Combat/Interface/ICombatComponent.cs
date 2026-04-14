using System;
using Kope.Component.Health.Interface;
using UnityEngine;

namespace Kope.Component.Combat.Interface {
	/// <summary>
	/// Context passed from abilities into effect factories.
	/// </summary>
	public struct EffectContext {
		public DamageDetail DamageDetail;
		public Vector3 KnockbackDirection;
		public IHealthComponent CasterHealth;
	}

	public interface ICombatable {
		float TakeHit(DamageDetail damageDetail);
		void TakeDamageDebugOnly(int amount);
		bool ApplyStatModifier(StatModifier effect);
		void ApplyKnockback(Vector3 direction, float duration, float impulse);
		void Heal(float flatHealAmount, float healPercentage);
	}

	public interface ICombatComponent : ICombatable {
	}

	public interface IEffectFactory<TTarget> {
		IEffect<TTarget> Create(EffectContext context = default);
	}

	public interface IEffect<TTarget> {
		float Apply(TTarget target);
		void Cancel();
		event Action<IEffect<TTarget>> OnCompleted;
	}

	public interface ITickableEffect {
		void Tick(float deltaTime);
	}
}