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
		[SerializeField] private HealOverTimeEffectData BaseData;
		[Tooltip("Scaling data for the heal-over-time effect. Overrides the base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private HealOverTimeEffectLevelScaling[] nextLevelScaling = new HealOverTimeEffectLevelScaling[3];
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
			if (this.nextLevelScaling == null || this.nextLevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			newLevelThreshold = this.nextLevelScaling[0].AbilityUsedThreshold;
			for (int i = this.nextLevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.nextLevelScaling[i].AbilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.nextLevelScaling.Length)
						? this.nextLevelScaling[i + 1].AbilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new HealOverTimeEffectData {
						FlathealPerInterval = this.nextLevelScaling[i].FlathealPerInterval <= 0
							? this.BaseData.FlathealPerInterval : this.nextLevelScaling[i].FlathealPerInterval,
						PercentHealPerInterval = this.nextLevelScaling[i].PercentHealPerInterval <= 0
							? this.BaseData.PercentHealPerInterval : this.nextLevelScaling[i].PercentHealPerInterval,
						Duration = this.nextLevelScaling[i].Duration <= 0
							? this.BaseData.Duration : this.nextLevelScaling[i].Duration,
						DickInterval = this.nextLevelScaling[i].TickInterval <= 0
							? this.BaseData.DickInterval : this.nextLevelScaling[i].TickInterval
					};
				}
			}
			return this.BaseData;
		}

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.nextLevelScaling == null || this.nextLevelScaling.Length == 0) {
				this.nextLevelScaling = new HealOverTimeEffectLevelScaling[3];
			}
		}
	}

	[Serializable]
	public class HealOverTimeEffect : IEffect<IHealable>, ITickableEffect {
		private readonly HealOverTimeEffectData _data;
		public event Action<ITickableEffect> OnCompletedOrCancelled;
		private IntervalTimer timer;
		private IHealable currentTarget;


		public HealOverTimeEffect(HealOverTimeEffectData data) {
			this._data = data;
			this.OnCompletedOrCancelled = null;
			this.timer = null;
			this.currentTarget = null;

		}

		public void Apply(IHealable target) {
			this.currentTarget = target;
			this.timer = new IntervalTimer(this._data.Duration, this._data.DickInterval) {
				OnInterval = OnInterval,
				OnTimerStop = OnStop
			};
			this.timer.Start();
			//initial heal
			this.currentTarget?.Heal(this._data.FlathealPerInterval, this._data.PercentHealPerInterval);
		}

		public void Tick(float deltaTime) => this.timer?.Tick(deltaTime);

		private void OnInterval() {
			this.currentTarget?.Heal(this._data.FlathealPerInterval, this._data.PercentHealPerInterval);
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




	[Serializable]
	public struct HealOverTimeEffectData {
		[Header("Healing")]
		[Min(0f)]
		public float FlathealPerInterval;
		[Range(0f, 1f)]
		public float PercentHealPerInterval;
		[Header("Timing")]
		[Min(0f)]
		public float Duration;
		[Range(0.1f, 5f)] public float DickInterval;
	}
	[Serializable]
	public struct HealOverTimeEffectLevelScaling {
		[Header("Scaling")]
		[Min(0f)]
		public int AbilityUsedThreshold;
		[Min(0f)]
		public float FlathealPerInterval;
		[Range(0f, 1f)]
		public float PercentHealPerInterval;
		[Min(0f)]
		public float Duration;
		[Range(0.1f, 5f)] public float TickInterval;
	}

}