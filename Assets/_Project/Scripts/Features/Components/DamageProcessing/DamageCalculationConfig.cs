using Kope.Character.Stats;
using Kope.Component.Combat;
using Kope.Core.Attribute;
using UnityEngine;

namespace Kope.Component.Health {

	/// <summary>
	/// Configuration asset that governs the systemic math for incoming damage calculations.
	/// </summary>
	/// <remarks>
	/// The comprehensive damage mitigation loop is evaluated via the following unified master formula:
	/// <code>
	/// FinalDamage = DamageAmount × [defFactor / (defFactor + (DEF × (1 - Pierce)))] × [ConditionalResBranch] × [lvlDelta &lt; 0 ? 1 / ((lvlFactor × |lvlDelta|) + 1) : (lvlFactor × |lvlDelta|) + 1]
	/// </code>
	/// </remarks>
	[CreateAssetMenu(fileName = "DamageCalculationConfig", menuName = "Configs/DamageCalculationConfig", order = 1)]
	public class DamageCalculationConfig : ScriptableObject {

		[Header("Defense")]
		[SerializeField, Range(10f, 1000f)]
		[Tooltip("Controls defense reduction scaling weight. Lower values give individual DEF points more value; higher values diminish impact.")]
		private float defFactor = 100f;

		[Space(4)]

		[Header("Resistance")]
		[SerializeField, Range(0.2f, 0.90f)]
		[Tooltip("The inflection point where linear scaling drops off and diminishing returns begin (e.g., 0.8 = 80% linear reduction cap).")]
		private float resThreshold = 0.8f;

		[SerializeField, Range(0.1f, 4f)]
		[Tooltip("Amplification scalar for negative resistance. (e.g., a 0.5 factor means -1.0 resistance yields a 50% damage penalty).")]
		private float negResMult = 0.5f;

		[SerializeField, ReadOnly]
		[Tooltip("Cached normalization factor calculated dynamically as 1 / (1 - resThreshold) to avoid runtime division overhead.")]
		private float resScaleFactor;

		[Space(4)]

		[Header("Level Scaling")]
		[SerializeField]
		[Tooltip("Global weight modifier applied per point of level difference between the caster and target.")]
		private float lvlFactor = 0.02f;

		public float DefFactor => this.defFactor;
		public float ResThreshold => this.resThreshold;
		public float LvlFactor => this.lvlFactor;
		public float ResScaleFactor => this.resScaleFactor;

		#region Unity Lifecycle
		private void OnValidate() => CacheResScaleFactor();
		private void OnEnable() => CacheResScaleFactor();
		#endregion

		#region Main Damage Calculation Logic
		/// <summary>
		/// Computes the final resulting damage throughput after compiling all defensive and level statistics.
		/// </summary>
		public float TakeHit(DamageDetail details, float currentLvl, IStatSystem stats) {
			float def = stats.GetStatValue(CharacterStatType.DEF);
			float res = stats.GetResistanceValue(details.DamageType);

			float defMult = GetDefenceMultiplier(def, details.DefencePierceRatio);
			float resMult = GetResistanceMultiplier(res, details.DamageType, details.IgnoreResistance);
			float lvlMult = GetLevelMultiplier(details.CasterLevel, currentLvl);

			return details.DamageAmount * defMult * resMult * lvlMult;
		}
		#endregion

		#region Core Damage Calculation Helpers
		/// <summary>
		/// Calculates the effective damage reduction multiplier based on the target's defense stat and attacker pierce properties.
		/// Applies a standard diminishing returns curve to ensure defense cannot reduce damage to zero.
		/// </summary>
		protected virtual float GetDefenceMultiplier(float def, float pierce = 0f) {
			// Idea "stolen" from LOL's damage formula: Damage = BaseDamage * (100 / (100 + EffectiveDefense))
			// So credit to them for the inspiration, but this is a more generalized and configurable version.
			float effectiveDef = def * Mathf.Clamp01(1f - pierce);
			return this.defFactor / (this.defFactor + effectiveDef);
		}

		/// <summary>
		/// Calculates the effective damage multiplier based on the target's resistance stat, damage type, and resistance-ignoring effects.
		/// Handles negative scaling (damage amplification), standard linear reduction, and high-tier asymptotic diminishing returns.
		/// </summary>
		protected virtual float GetResistanceMultiplier(float res, DamageType type, float ignore = 0f) {
			// The formula is from Genshin Impact's Resistance formula, which is a well-balanced
			// So credit to the Genshin Wiki for the inspiration, but this is a more generalized 
			// and configurable version.
			float effectiveRes = res - ignore;

			// Band 1: Negative resistance (amplifies damage)
			if (effectiveRes < 0f)
				return 1f - (effectiveRes * this.negResMult);

			// Band 2: Linear mitigation zone (1:1 direct percentage protection)
			if (effectiveRes < this.resThreshold)
				return 1f - effectiveRes;

			// Band 3: Diminishing returns curve to prevent complete immunity/100% damage mitigation
			return 1f / (1f + (effectiveRes * this.resScaleFactor));
		}

		/// <summary>
		/// Calculates a scaling modifier based on the level difference between the caster and target.
		/// Caster level advantages increase damage output, while target level advantages diminish it via inverse scaling.
		/// </summary>
		protected virtual float GetLevelMultiplier(int casterLvl, float targetLvl) {
			float lvlDelta = casterLvl - targetLvl;
			float mult = (this.lvlFactor * Mathf.Abs(lvlDelta)) + 1f;

			// Invert the multiplier if the attacker is lower level than the target
			return lvlDelta < 0f ? 1f / mult : mult;
		}
		#endregion

		#region Cached Computation Helpers
		private void CacheResScaleFactor() {
			// Converts the linear cap threshold into a normalized slope for the post-threshold curve.
			// Formula: Mult = 1 / (1 + Res * ScaleFactor) where ScaleFactor = 1 / (1 - Threshold)
			this.resScaleFactor = 1f / (1f - this.resThreshold);
		}
		#endregion
	}
}