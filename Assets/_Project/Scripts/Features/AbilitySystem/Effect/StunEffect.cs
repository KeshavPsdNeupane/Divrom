using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Movement;
using UnityEngine;

namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct StunEffectData {
		[Range(0f, 10f)] public float duration;
		public bool superStun;
	}
	[Serializable]
	public struct StunEffectLevelingScale {
		public int abilityUsedThreshold;
		public float durationScale;
	}

	[Serializable]
	public class StunEffectFactory : IEffectFactory<IStunnable> {
		public StunEffectData BaseData;
		public StunEffectLevelingScale[] levelScalingValues = new StunEffectLevelingScale[3];
		private StunEffectData _cachedData;
		private int _cachedNewLevelThreshold = 0;
		public IEffect<IStunnable> Create(EffectContext context = default) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				this._cachedData = this.ResolveData(context.AbilityUsedCount, out this._cachedNewLevelThreshold);
			}
			return new StunEffect(this._cachedData);
		}
		private StunEffectData ResolveData(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.levelScalingValues.Length - 1; i >= 0; i--) {
				if (useCount >= this.levelScalingValues[i].abilityUsedThreshold) {
					newLevelThreshold = this.levelScalingValues[i].abilityUsedThreshold;
					return new StunEffectData() {
						duration = this.levelScalingValues[i].durationScale,
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