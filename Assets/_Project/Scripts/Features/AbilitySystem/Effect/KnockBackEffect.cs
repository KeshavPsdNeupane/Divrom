using Kope.Component.Combat.Interface;
using System;
using Kope.Component.Combat;
using Kope.Component.Movement;
using UnityEngine;


namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public class KnockbackEffectFactory : IEffectFactory<IKnockbackable> {
		public KnockbackDetail Detail;

		public IEffect<IKnockbackable> Create(EffectContext context = default)
		=> new KnockbackEffect(this.Detail, context.HitPoint);
	}

	[Serializable]
	public class KnockbackEffect : IEffect<IKnockbackable> {

		private readonly KnockbackDetail Detail;
		private readonly Vector3 hitPoint;
		public KnockbackEffect(KnockbackDetail detail, Vector3 hitPoint) {
			this.Detail = detail;
			this.hitPoint = hitPoint;
		}
		public float Apply(IKnockbackable target) {
			target.ApplyKnockback(this.hitPoint, this.Detail.Duration, this.Detail.KnockbackStrength);
			return 0f;
		}
	}
}