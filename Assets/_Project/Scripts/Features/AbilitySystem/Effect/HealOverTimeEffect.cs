using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using ThirdParty;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	// the target selection is handled by the ability and the effect factory just creates the effect based on 
	// the context passed in, so the effect itself doesn't need to know/it doesn't care about the context of who the caster is
	// or who the target
	[Serializable]
	public class HealOverTimeEffectFactory : IEffectFactory<IHealable> {
		public float healPerInterval = 10f;
		public float duration;
		[Range(0.1f, 5f)] public float tickInterval = 1f;

		public IEffect<IHealable> Create(EffectContext context = default)
			=> new HealOverTimeEffect(this.healPerInterval, this.duration, this.tickInterval);
	}

	[Serializable]
	public class HealOverTimeEffect : IEffect<IHealable>, ITickableEffect {
		public float flathealAmountPerInterval;
		public float duration;
		public float tickInterval;
		public event Action<ITickableEffect> OnCompletedOrCancell;
		private IntervalTimer timer;
		private IHealable currentTarget;


		public HealOverTimeEffect(float healAmountPerInterval, float duration, float tickInterval) {

			this.flathealAmountPerInterval = healAmountPerInterval;
			this.duration = duration;
			this.tickInterval = tickInterval;

			this.OnCompletedOrCancell = null;
			this.timer = null;
			this.currentTarget = null;

		}

		public float Apply(IHealable target) {
			this.currentTarget = target;
			this.timer = new IntervalTimer(duration, tickInterval) {
				OnInterval = OnInterval,
				OnTimerStop = OnStop
			};
			this.timer.Start();
			return 0f;
		}

		public void Tick(float deltaTime) => this.timer?.Tick(deltaTime);

		private void OnInterval() {
			this.currentTarget?.Heal(this.flathealAmountPerInterval, 0f);
		}
		private void OnStop() => Cleanup();

		public void Cancel() {
			this.timer?.Stop();
			Cleanup();
		}

		private void Cleanup() {
			this.timer = null;
			this.currentTarget = null;
			this.OnCompletedOrCancell?.Invoke(this);
		}
	}

}