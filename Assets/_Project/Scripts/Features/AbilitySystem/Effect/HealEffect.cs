using System;
using Kope.Component.Combat.Interface;
using UnityEngine;


namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class HealEffectFactory : IEffectFactory<ICombatable> {
		public float flatHealAmount;
		[Range(0f, 1f)] public float healPercentage;

		IEffect<ICombatable> IEffectFactory<ICombatable>.Create(EffectContext context) {
			return new HealEffect(flatHealAmount, healPercentage);
		}
	}
	[Serializable]
	public struct HealEffect : IEffect<ICombatable> {
		private readonly float flatHealAmount;
		private readonly float healPercentage;

		public HealEffect(float flatHealAmount, float healPercentage) {
			this.flatHealAmount = flatHealAmount;
			this.healPercentage = healPercentage;
			this.OnCompleted = null;
		}
		public event Action<IEffect<ICombatable>> OnCompleted;
		public readonly float Apply(ICombatable target) {
			target.Heal(flatHealAmount, healPercentage);
			this.OnCompleted?.Invoke(this);
			return 0; // Heal effect does not deal damage, so we return 0 here.
		}

		public readonly void Cancel() => this.OnCompleted?.Invoke(this);
	}

}