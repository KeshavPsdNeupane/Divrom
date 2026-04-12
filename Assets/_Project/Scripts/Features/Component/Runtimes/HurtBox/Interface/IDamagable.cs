using System;
using ThirdParty;
using UnityEngine;

namespace Kope.Component.HurtBox.Interface {

	/// <summary>
	/// IDamageable inherits from IKnockbackable because every damageable entity
	/// in this framework should react to physical forces.
	/// </summary>
	public interface IDamageable {
		Vector3 Position { get; }
		void TakeHit(DamageDetail damageDetail);
		void TakeDamage(int amount);
		void ApplyEffect(IEffect<IDamageable> effect);
		void ApplyKnockback(Vector3 direction, float duration, float impulse);
	}

	/// <summary>
	/// Context passed from the ability/attack system into effect factories at create time.
	/// Carries caster data needed for direction-dependent effects like knockback.
	/// </summary>
	public struct EffectContext {
		public Vector3 CasterPosition;
	}

	public interface IEffectFactory<TTarget> {
		IEffect<TTarget> Create(EffectContext context = default);
	}

	public interface IEffect<TTarget> {
		void Apply(TTarget target);
		void Cancel();
		event Action<IEffect<TTarget>> OnCompleted;
	}

	/// <summary>
	/// Interface for effects that require per-frame updates (like DoT timers).
	/// </summary>
	public interface ITickableEffect {
		void Tick(float deltaTime);
	}

	#region Concrete Effect Implementations

	[Serializable]
	public class DamageEffectFactory : IEffectFactory<IDamageable> {
		public DamageDetail detail;
		public IEffect<IDamageable> Create(EffectContext context = default) => new DamageEffect { detail = detail };
	}

	[Serializable]
	public struct DamageEffect : IEffect<IDamageable> {
		public DamageDetail detail;
		public event Action<IEffect<IDamageable>> OnCompleted;

		public readonly void Apply(IDamageable target) {
			target.TakeHit(detail);
			OnCompleted?.Invoke(this);
		}

		public readonly void Cancel() => OnCompleted?.Invoke(this);
	}

	[Serializable]
	public class DamageOverTimeEffectFactory : IEffectFactory<IDamageable> {
		public DamageDetail detail;
		public float duration = 3f;
		public float tickInterval = 1f;

		public IEffect<IDamageable> Create(EffectContext context = default) => new DamageOverTimeEffect {
			detail = detail,
			duration = duration,
			tickInterval = tickInterval
		};
	}

	[Serializable]
	public struct DamageOverTimeEffect : IEffect<IDamageable>, ITickableEffect {
		public DamageDetail detail;
		public float duration;
		public float tickInterval;
		public event Action<IEffect<IDamageable>> OnCompleted;

		private IntervalTimer timer;
		private IDamageable currentTarget;

		public void Apply(IDamageable target) {
			currentTarget = target;
			timer = new IntervalTimer(duration, tickInterval) {
				OnInterval = OnInterval,
				OnTimerStop = OnStop
			};
			timer.Start();
		}

		public readonly void Tick(float deltaTime) => timer?.Tick(deltaTime);

		private readonly void OnInterval() => currentTarget?.TakeHit(detail);
		private void OnStop() => Cleanup();

		public void Cancel() {
			timer?.Stop();
			Cleanup();
		}

		private void Cleanup() {
			timer = null;
			currentTarget = null;
			OnCompleted?.Invoke(this);
		}
	}

	[Serializable]
	public class KnockbackEffectFactory : IEffectFactory<IDamageable> {
		public float duration = 0.2f;
		public float impulse = 2.5f;

		public IEffect<IDamageable> Create(EffectContext context = default) => new KnockbackEffect {
			casterPosition = context.CasterPosition,
			duration = duration,
			impulse = impulse
		};
	}

	[Serializable]
	public struct KnockbackEffect : IEffect<IDamageable> {
		public Vector3 casterPosition;
		public float duration;
		public float impulse;
		public event Action<IEffect<IDamageable>> OnCompleted;

		public readonly void Apply(IDamageable target) {
			Vector3 dir = (target.Position - casterPosition).normalized;
			target.ApplyKnockback(dir, duration, impulse);
			OnCompleted?.Invoke(this);
		}

		public readonly void Cancel() => OnCompleted?.Invoke(this);
	}

	#endregion
}