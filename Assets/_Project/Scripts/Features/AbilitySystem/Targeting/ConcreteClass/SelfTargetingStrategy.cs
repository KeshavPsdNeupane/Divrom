// SelfTargetingStrategy.cs
using System;
using Kope.AbilitySystem;
using Kope.Component.Combat.Interface;

namespace Kope.Component.Ability.Targeting {
	[Serializable]
	public class SelfTargetingStrategyFactory : ITargetingFactory {
		public TargetingStrategy Create() => new SelfTargetingStrategy();
	}

	[Serializable]
	public sealed class SelfTargetingStrategy : TargetingStrategy {


		public override void Cast(
			TargetingManager targetingManager,
			TargetContext casterContext,
			EffectContext effectContext,
			ITargetingReceiver onTargetResolved) {

			Initialize(targetingManager, casterContext, effectContext, onTargetResolved, false);

			if (this.casterContext.HitBox != null) {
				ResolveSingleTarget(this.casterContext);
			}
			// we will clear the stragety state regardless of whether the caster context had 
			// a valid hitbox or not, because even if it doesn't have a hitbox, 
			// it is still a valid target for self-targeting abilities (e.g. self-buffs).
			FinishTheStrategy();
		}

		protected override bool ExecuteResolution() {
			// self Targeting doesn't use clickPoint for resolution, so we can ignore it here.
			// we call the ResolveSingleTarget directly in Start method using the caster context, 
			// so we don't need to do anything here for self targeting.
			return true;
		}
	}
}