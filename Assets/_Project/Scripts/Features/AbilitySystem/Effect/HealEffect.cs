using System;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using UnityEngine;


namespace Kope.AbilitySystem.Effect {
	// the target selection is handled by the ability and the effect factory just creates the effect based on 
	// the context passed in, so the effect itself doesn't need to know/it doesn't care
	// about the context of who the caster is or who the target
	[Serializable]
	public class HealEffectFactory : IEffectFactory<IHealable> {
		public float flatHealAmount;
		[Range(0f, 1f)] public float healPercentage;

		IEffect<IHealable> IEffectFactory<IHealable>.Create(EffectContext context) {
			return new HealEffect(flatHealAmount, healPercentage);
		}
	}
	[Serializable]
	public class HealEffect : IEffect<IHealable> {
		private readonly float flatHealAmount;
		private readonly float healPercentage;

		public HealEffect(float flatHealAmount, float healPercentage) {
			this.flatHealAmount = flatHealAmount;
			this.healPercentage = healPercentage;
		}
		public float Apply(IHealable target) {
			target.Heal(flatHealAmount, healPercentage);
			return 0;
		}
	}

}