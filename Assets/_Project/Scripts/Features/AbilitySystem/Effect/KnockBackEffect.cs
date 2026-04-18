using Kope.Component.Combat.Interface;
using System;
using Kope.Component.Combat;
using Kope.Component.Movement;
using UnityEngine;


namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct KnockBackLevelScaling {
		public int abilityUsedThreshold;
		public float knockbackStrength;
		public float duration;

	}
	[Serializable]
	public class KnockbackEffectFactory : IEffectFactory<IKnockbackable> {
		public KnockbackDetail Detail;
		public KnockBackLevelScaling[] LevelScaling = new KnockBackLevelScaling[3];
		private KnockbackDetail _cachedDetail;
		private int _cachedNewLevelThreshold = 0;
		public IEffect<IKnockbackable> Create(EffectContext context = default) {
			if (context.AbilityUsedCount >= this._cachedNewLevelThreshold) {
				this._cachedDetail = this.ResolveData(context.AbilityUsedCount, out this._cachedNewLevelThreshold);
			}
			return new KnockbackEffect(this._cachedDetail, context.HitPoint);
		}

		private KnockbackDetail ResolveData(int useCount, out int newLevelThreshold) {
			newLevelThreshold = 0;
			for (int i = this.LevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.LevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = this.LevelScaling[i].abilityUsedThreshold;
					return new KnockbackDetail(
						this.LevelScaling[i].knockbackStrength,
						this.LevelScaling[i].duration,
						this.Detail.IsPulling);
				}
			}
			return this.Detail;
		}
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