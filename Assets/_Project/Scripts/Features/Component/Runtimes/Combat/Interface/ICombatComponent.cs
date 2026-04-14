using System;
using Kope.Component.Health.Interface;
using Kope.Component.HurtBox.Interface;
using UnityEngine;

namespace Kope.Component.Combat.Interface {
	/// <summary>
	/// Context passed from abilities into effect factories.
	/// </summary>
	public struct EffectContext {
		public GameObject Caster;
		public DamageDetail DamageDetail;
		public Vector3 KnockbackDirection;
		public IHealthComponent CasterHealth;
	}

	public interface ICombatable {
		IHurtBoxComponent HurtBox { get; }
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

	/// <summary>
	/// This represents a status effect that can be applied to a combatable target.
	/// with out the ITickableEffect interface, the effect is considered an "instant effect" 
	/// that applies its effect immediately upon application and doesn't require ticking or tracking.
	/// like "Fire and forget" type of effects, 
	/// for example, a one-time damage boost or heal that applies its effect immediately 
	/// and then is done.
	/// </summary>
	/// <typeparam name="TTarget"></typeparam>
	public interface IEffect<TTarget> {
		float Apply(TTarget target);
	}
	/// <summary>
	/// This interface represents a status effect that has a duration and requires ticking to
	/// update its state over time. like a DOT (Damage over Time) effect, Damage or healing over a duration, 
	/// buff or debuff doesnt count here since StatModifier Lifecycle is managed by the CharacterStatsSystem,
	/// SO for stat buff/debuff effects they "fire and forget" type of effects that apply
	///  their modifier immediately and don't require ticking or tracking within the CombatComponent.
	/// </summary>
	public interface ITickableEffect {
		event Action<ITickableEffect> OnCompletedOrCancell;
		void Tick(float deltaTime);
		void Cancel();
	}
}