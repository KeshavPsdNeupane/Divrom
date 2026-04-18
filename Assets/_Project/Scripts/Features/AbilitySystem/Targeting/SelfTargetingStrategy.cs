// SelfTargetingStrategy.cs
using System;
using Kope.Component.Combat.Interface;

namespace Kope.Component.Ability.Targeting {

	[Serializable]
	public sealed class SelfTargetingStrategy : TargetingStrategy, ITargetingFactory {
		public TargetingStrategy Create() => new SelfTargetingStrategy();

		public override void Start(
			TargetingManager targetingManager,
			in TargetContext casterContext,
			EffectContext effectContext,
			Action<TargetContext, EffectContext> onTargetResolved) {
			Begin(targetingManager, casterContext, effectContext, onTargetResolved);
			if (this.casterContext.HitBox != null) {
				ResolveTarget(this.casterContext);
				Cancel();
			}
		}
	}
}