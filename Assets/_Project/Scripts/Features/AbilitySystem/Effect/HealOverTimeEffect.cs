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
	public struct HealOverTimeEffectData {
		public float healPerInterval;
		public float duration;
		[Range(0.1f, 5f)] public float tickInterval;
	}
	[Serializable]
	public struct HealOverTimeEffectLevelScaling {
		public int abilityUsedThreshold;
		public float healPerInterval;
		public float duration;
		[Range(0.1f, 5f)] public float tickInterval;
	}

	[Serializable]
	public class HealOverTimeEffectFactory : IEffectFactory<IHealable> {
		public HealOverTimeEffectData BaseData;
		[Tooltip("Level scaling for the healOverTime effect,Will override base data when threshold is met")]
		public HealOverTimeEffectLevelScaling[] LevelScaling = new HealOverTimeEffectLevelScaling[3];
		private HealOverTimeEffectData _cachedData;
		private int _cachedNewLevelThreshold = 0;

		public IEffect<IHealable> Create(EffectContext context = default) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				this._cachedData = this.ResolveData(context.AbilityUsedCount, out this._cachedNewLevelThreshold);
			}
			return new HealOverTimeEffect(this._cachedData);
		}

		private HealOverTimeEffectData ResolveData(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.LevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.LevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = this.LevelScaling[i].abilityUsedThreshold;
					return new HealOverTimeEffectData {
						healPerInterval = this.LevelScaling[i].healPerInterval,
						duration = this.LevelScaling[i].duration,
						tickInterval = this.LevelScaling[i].tickInterval
					};
				}
			}
			return this.BaseData;
		}
	}

	[Serializable]
	public class HealOverTimeEffect : IEffect<IHealable>, ITickableEffect {
		public float flathealAmountPerInterval;
		public float duration;
		public float tickInterval;
		public event Action<ITickableEffect> OnCompletedOrCancelled;
		private IntervalTimer timer;
		private IHealable currentTarget;


		public HealOverTimeEffect(HealOverTimeEffectData data) {

			this.flathealAmountPerInterval = data.healPerInterval;
			this.duration = data.duration;
			this.tickInterval = data.tickInterval;

			this.OnCompletedOrCancelled = null;
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
			this.OnCompletedOrCancelled?.Invoke(this);
		}
	}

}