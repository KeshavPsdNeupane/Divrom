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
		[SerializeField] private StunEffectData BaseData;
		[Tooltip("Scaling values for the stun effect based on ability usage." +
		"Overrides base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private StunEffectLevelingScale[] nextlevelScaling = new StunEffectLevelingScale[3];
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
			if (this.nextlevelScaling == null || this.nextlevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.BaseData;
			}

			newLevelThreshold = this.nextlevelScaling[0].abilityUsedThreshold;
			for (int i = this.nextlevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.nextlevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.nextlevelScaling.Length)
						? this.nextlevelScaling[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new StunEffectData() {
						duration = this.nextlevelScaling[i].durationScale <= 0
							? this.BaseData.duration : this.nextlevelScaling[i].durationScale,
						superStun = this.BaseData.superStun
					};
				}
			}
			return this.BaseData;
		}
		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.nextlevelScaling == null || this.nextlevelScaling.Length == 0) {
				this.nextlevelScaling = new StunEffectLevelingScale[3];
			}
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

		public void Apply(IStunnable target) {
			if (target == null) return;

			if (this.superStun) {
				target.SuperStun(this.duration);
			} else {
				target.Stun(this.duration);
			}

		}
	}
}