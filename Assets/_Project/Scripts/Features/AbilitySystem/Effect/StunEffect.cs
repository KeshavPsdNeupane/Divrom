using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Movement;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct StunEffectData {
		[Header("Timing")]
		[Range(0f, 10f)] public float duration;
		[Tooltip("If true, uses the stronger super stun variant instead of a normal stun.")]
		public bool superStun;
	}
	[Serializable]
	public struct StunEffectLevelingScale {
		[Header("Scaling")]
		[Min(0f)]
		public int abilityUsedThreshold;
		[Min(0f)]
		public float durationScale;
	}

	[Serializable]
	public class StunEffectFactory : IEffectFactory<IStunnable> {
		public StunEffectData BaseData;
		[Tooltip("Scaling values for the stun effect based on ability usage." +
		"Overrides base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		public StunEffectLevelingScale[] levelScalingValues = new StunEffectLevelingScale[3];
		private StunEffectData _cachedData;
		private int _nextRecomputeThreshold = 0;

		public IEffect<IStunnable> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = this.ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
			}
			return new StunEffect(this._cachedData);
		}

		private StunEffectData ResolveData(int useCount, out int newLevelThreshold) {
			if (this.levelScalingValues == null || this.levelScalingValues.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			newLevelThreshold = this.levelScalingValues[0].abilityUsedThreshold;
			for (int i = this.levelScalingValues.Length - 1; i >= 0; i--) {
				if (useCount >= this.levelScalingValues[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.levelScalingValues.Length)
						? this.levelScalingValues[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new StunEffectData() {
						duration = this.levelScalingValues[i].durationScale <= 0
							? this.BaseData.duration : this.levelScalingValues[i].durationScale,
						superStun = this.BaseData.superStun
					};
				}
			}
			return this.BaseData;
		}
	}

	[Serializable]
	public class StunEffect : IEffect<IStunnable> {
		private readonly float duration;
		private readonly bool superStun;

		public StunEffect(StunEffectData data) {
			this.duration = data.duration;
			this.superStun = data.superStun;
		}

		public float Apply(IStunnable target) {
			if (target == null) return 0f;

			if (this.superStun) {
				target.SuperStun(this.duration);
			} else {
				target.Stun(this.duration);
			}

			return 0f;
		}
	}
}