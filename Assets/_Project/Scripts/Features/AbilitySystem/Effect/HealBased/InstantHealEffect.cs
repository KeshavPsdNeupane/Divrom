using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using UnityEngine;


namespace Kope.AbilitySystem.Effect {
	// the target selection is handled by the ability and the effect factory just creates the effect based on 
	// the context passed in, so the effect itself doesn't need to know/it doesn't care
	// about the context of who the caster is or who the target

	[Serializable]
	public class InstantHealEffectFactory : IEffectFactory<IHealable> {
		[SerializeField] private InstantHealEffectData BaseData;
		[Tooltip("Scaling data for the heal effect. Overrides the base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private InstantHealEffectLevelScaling[] nextLevelScaling = new InstantHealEffectLevelScaling[3];
		private InstantHealEffectData _cachedData;
		private int _nextRecomputeThreshold = -1;

		IEffect<IHealable> IEffectFactory<IHealable>.Create(EffectContext context) {
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedData = this.ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
			}
			return new InstantHealEffect(this._cachedData);
		}

		private InstantHealEffectData ResolveData(int useCount, out int newLevelThreshold) {
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
					return new InstantHealEffectData {
						FlatHealAmount = this.nextLevelScaling[i].FlatHealAmount <= 0
							? this.BaseData.FlatHealAmount : this.nextLevelScaling[i].FlatHealAmount,
						PercentageHealAmount = this.nextLevelScaling[i].PercentageHealAmount <= 0
							? this.BaseData.PercentageHealAmount : this.nextLevelScaling[i].PercentageHealAmount
					};

				}
			}
			return this.BaseData;
		}
		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.nextLevelScaling == null || this.nextLevelScaling.Length == 0) {
				this.nextLevelScaling = new InstantHealEffectLevelScaling[3];
			}
		}
	}
	[Serializable]
	public class InstantHealEffect : IEffect<IHealable> {
		private readonly InstantHealEffectData _data;

		public InstantHealEffect(InstantHealEffectData data) {
			this._data = data;
		}
		public void Apply(IHealable target) {
			target.Heal(this._data.FlatHealAmount, this._data.PercentageHealAmount);

		}
	}
	[Serializable]
	public struct InstantHealEffectData {
		[Header("Healing")]
		[Min(0f)]
		public float FlatHealAmount;
		[Range(0f, 1f)] public float PercentageHealAmount;

	}
	[Serializable]
	public struct InstantHealEffectLevelScaling {
		[Header("Scaling")]
		[Min(0f)]
		public int AbilityUsedThreshold;
		[Min(0f)]
		public float FlatHealAmount;
		[Range(0f, 1f)] public float PercentageHealAmount;
	}

}