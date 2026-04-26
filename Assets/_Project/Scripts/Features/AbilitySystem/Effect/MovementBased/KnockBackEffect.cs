using Kope.Component.Combat.Interface;
using System;
using Kope.Component.Combat;
using Kope.Component.Movement;
using UnityEngine;


namespace Kope.AbilitySystem.Effect {
	[Serializable]
	public struct KnockBackLevelScaling {
		[Header("Scaling")]
		[Min(0f)]
		public int abilityUsedThreshold;
		[Min(0f)]
		public float knockbackStrength;
		[Min(0f)]
		public float duration;

	}
	[Serializable]
	public class KnockbackEffectFactory : IEffectFactory<IKnockbackable> {
		[Header("Knockback")]
		[SerializeField] private KnockbackDetail Detail;
		[Tooltip("Scaling values for the knockback effect based on ability usage." +
		"Overrides base data when the ability use count meets a threshold. Must be in ascending order by abilityUsedThreshold.")]
		[SerializeField] private KnockBackLevelScaling[] nextLevelScaling = new KnockBackLevelScaling[3];
		private KnockbackDetail _cachedDetail;
		private int _nextRecomputeThreshold = 0;

		public IEffect<IKnockbackable> Create(EffectContext context = default) {
			// The lookup only advances a few times per ability lifetime, so caching avoids rescanning the array on every create.
			if (this._nextRecomputeThreshold < int.MaxValue
			&& context.AbilityUsedCount >= this._nextRecomputeThreshold) {
				this._cachedDetail = this.ResolveData(context.AbilityUsedCount, out this._nextRecomputeThreshold);
			}
			return new KnockbackEffect(this._cachedDetail, context.HitPoint);
		}

		private KnockbackDetail ResolveData(int useCount, out int newLevelThreshold) {
			if (this.nextLevelScaling == null || this.nextLevelScaling.Length == 0) {
				newLevelThreshold = int.MaxValue;
				return this.Detail;
			}

			newLevelThreshold = this.nextLevelScaling[0].abilityUsedThreshold;
			for (int i = this.nextLevelScaling.Length - 1; i >= 0; i--) {
				if (useCount >= this.nextLevelScaling[i].abilityUsedThreshold) {
					newLevelThreshold = (i + 1 < this.nextLevelScaling.Length)
						? this.nextLevelScaling[i + 1].abilityUsedThreshold
						: int.MaxValue;

					// If a field in the scaling data is set to 0 or less, fall back to the base value.
					// This allows partial overrides without repeating every field for each level.
					return new KnockbackDetail(
						this.nextLevelScaling[i].knockbackStrength <= 0
							? this.Detail.KnockbackStrength : this.nextLevelScaling[i].knockbackStrength,
						this.nextLevelScaling[i].duration <= 0
							? this.Detail.Duration : this.nextLevelScaling[i].duration,
						this.Detail.IsPulling);
				}
			}
			return this.Detail;
		}
		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			if (this.nextLevelScaling == null || this.nextLevelScaling.Length == 0) {
				this.nextLevelScaling = new KnockBackLevelScaling[3];
			}
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
		public void Apply(IKnockbackable target) {
			target.ApplyKnockback(this.hitPoint, this.Detail.Duration, this.Detail.KnockbackStrength);

		}
	}
}