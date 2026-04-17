using Kope.Component.Combat.Interface;

namespace Kope.Component.Ability.Targeting {

	[System.Serializable]
	public sealed class SelfTargetingStrategy : TargetingStrategy, ITargetingFactory {
		public TargetingStrategy Create() {
			return new SelfTargetingStrategy();
		}

		public override void Start(AbilityBase ability, TargetingManager targetingManager,
		in TargetContext casterContext, EffectContext effectContext) {
			Begin(ability, targetingManager, casterContext, effectContext);
			if (this.casterContext != null && this.casterContext.HitBox != null) {
				ExecuteOnTarget(this.casterContext);
			}
			Cancel();
		}
	}
}