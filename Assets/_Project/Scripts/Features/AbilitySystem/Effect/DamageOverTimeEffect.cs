using System;
using Kope.Component.Combat;
using Kope.Component.Combat.Interface;
using ThirdParty;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class DamageOverTimeEffectFactory : IEffectFactory<ICombatable> {
		public float duration = 3f;
		public float tickInterval = 1f;

		public IEffect<ICombatable> Create(EffectContext context = default) =>
			new DamageOverTimeEffect(context.DamageDetail, duration, tickInterval);
	}

	[Serializable]
	public struct DamageOverTimeEffect : IEffect<ICombatable>, ITickableEffect {
		public DamageDetail detail;
		public float duration;
		public float tickInterval;
		public event Action<IEffect<ICombatable>> OnCompleted;

		private IntervalTimer timer;
		private ICombatable currentTarget;

		public DamageOverTimeEffect(DamageDetail detail, float duration, float tickInterval) {
			this.detail = detail;
			this.duration = duration;
			this.tickInterval = tickInterval;
			this.OnCompleted = null;
			this.timer = null;
			this.currentTarget = null;
		}

		public float Apply(ICombatable target) {
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