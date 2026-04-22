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
		[Header("Healing")]
		[Min(0f)]
		public float healPerInterval;
		[Header("Timing")]
		[Min(0f)]
		public float duration;
		[Range(0.1f, 5f)] public float tickInterval;
	}
	[Serializable]
	public struct HealOverTimeEffectLevelScaling {
		[Header("Scaling")]
		[Min(0f)]
		public int abilityUsedThreshold;
		[Min(0f)]
		public float healPerInterval;
		[Min(0f)]
		public float duration;
		[Range(0.1f, 5f)] public float tickInterval;
	}

	[Serializable]
	public class HealOverTimeEffectFactory : IEffectFactory<IHealable> {
		public HealOverTimeEffectData BaseData;
		[Tooltip("Scaling data for the heal-over-time effect. Overrides the base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		public HealOverTimeEffectLevelScaling[] LevelScaling = new HealOverTimeEffectLevelScaling[3];
		private HealOverTimeEffectData _cachedData;
		private int _nextRecomputeThreshold = 0;

		public IEffect<IHealable> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = this.ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
			}
			return new HealOverTimeEffect(this._cachedData);
		}

		private HealOverTimeEffectData ResolveData(int useCount, out int newLevelThreshold) {
			if (this.LevelScaling == null || this.LevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			newLevelThreshold = this.LevelScaling[0].abilityUsedThreshold;
			for (int i = this.LevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.LevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.LevelScaling.Length)
						? this.LevelScaling[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new HealOverTimeEffectData {
						healPerInterval = this.LevelScaling[i].healPerInterval <= 0
							? this.BaseData.healPerInterval : this.LevelScaling[i].healPerInterval,
						duration = this.LevelScaling[i].duration <= 0
							? this.BaseData.duration : this.LevelScaling[i].duration,
						tickInterval = this.LevelScaling[i].tickInterval <= 0
							? this.BaseData.tickInterval : this.LevelScaling[i].tickInterval
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