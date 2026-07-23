using Kope.Character.Stats;
using UnityEngine;

namespace Kope.Component.Combat {
	/// <summary>
	/// Data container for a hit event. Includes source, damage math parameters,
	/// and level scaling data.
	/// </summary>
	public struct DamageDetail {
		public float DamageAmount;
		public GameObject Source;
		public DamageType DamageType;
		public int CasterLevel;
		public float DefencePierceRatio;
		public float IgnoreResistance;

		public DamageDetail(
			float damageAmount,
			GameObject source,
			DamageType damageType,
			float defencePierceRatio = 0,
			float ignoreResistance = 0,
			int casterLevel = 0) {
			this.DamageAmount = damageAmount;
			this.Source = source;
			this.DamageType = damageType;
			this.DefencePierceRatio = defencePierceRatio;
			this.IgnoreResistance = ignoreResistance;
			this.CasterLevel = casterLevel;
		}
	}
	// will move this to effect location later
	[System.Serializable]
	public struct KnockbackDetail {
		[Tooltip("If true the target will be pulled toward the point of origin. " +
		"Not a full blackhole pull, but more of a directional pull that still respects the direction vector, " +
		"just in the opposite direction. Useful for things like a hookshot or a tornado lift that pulls enemies up into the air. " +
		"If false, the target will be pushed away from the point of origin like a traditional knockback.")]
		public bool IsPulling;
		public float Duration;

		public float KnockbackStrength;
		public KnockbackDetail(float strength, float duration, bool isPulling) {
			this.IsPulling = isPulling;
			this.KnockbackStrength = strength;
			this.Duration = duration;
		}
	}
}