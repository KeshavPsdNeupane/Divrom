using System;
using Kope.Component.HurtBox;
using Kope.Component.HurtBox.Interface;
using ThirdParty;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class DamageOverTimeEffectFactory : IEffectFactory<IDamageable> {
		public float duration = 3f;
		public float tickInterval = 1f;

		public IEffect<IDamageable> Create(EffectContext context = default) =>
			new DamageOverTimeEffect(context.DamageDetail, duration, tickInterval);
	}

	[Serializable]
	public struct DamageOverTimeEffect : IEffect<IDamageable>, ITickableEffect {
		public DamageDetail detail;
		public float duration;
		public float tickInterval;
		public event Action<IEffect<IDamageable>> OnCompleted;

		private IntervalTimer timer;
		private IDamageable currentTarget;

		public DamageOverTimeEffect(DamageDetail detail, float duration, float tickInterval) {
			this.detail = detail;
			this.duration = duration;
			this.tickInterval = tickInterval;
			this.OnCompleted = null;
			this.timer = null;
			this.currentTarget = null;
		}

		public float Apply(IDamageable target) {
			this.currentTarget = target;
			this.timer = new IntervalTimer(duration, tickInterval) {
				OnInterval = OnInterval,
				OnTimerStop = OnStop
			};
			this.timer.Start();
			return 0f;
		}

		public readonly void Tick(float deltaTime) => this.timer?.Tick(deltaTime);

		private readonly void OnInterval() => this.currentTarget?.TakeHit(this.detail);
		private void OnStop() => Cleanup();

		public void Cancel() {
			this.timer?.Stop();
			Cleanup();
		}

		private void Cleanup() {
			this.timer = null;
			this.currentTarget = null;
			this.OnCompleted?.Invoke(this);
		}
	}
}