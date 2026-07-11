using Kope.Character.Stats;
using Kope.Component.Combat;
using Kope.Core.Attribute;
using UnityEngine;

namespace Kope.Component.Health {

	[CreateAssetMenu(fileName = "DamageCalculationConfig", menuName = "Configs/DamageCalculationConfig", order = 1)]
	public class DamageCalculationConfig : ScriptableObject {

		[Header("Defense")]
		[SerializeField, Range(10f, 1000f)]
		[Tooltip("Def reduction scaling weight. Lower values give individual DEF points more value; higher values diminish impact.")]
		private float defFactor = 100f;

		[Space(4)]

		[Header("Resistance")]
		[SerializeField, Range(0.2f, 0.90f)]
		[Tooltip("Inflection point where linear scaling drops off and diminishing returns begin (e.g., 0.8 = 80% linear reduction cap).")]
		private float resThreshold = 0.8f;

		[SerializeField, Range(0.1f, 4f)]
		[Tooltip("Amplification scalar for negative resistance. (e.g., 0.5 factor means -1.0 resistance yields a 50% damage penalty).")]
		private float negResMult = 0.5f;

		[SerializeField, ReadOnly]
		[Tooltip("Cached normalization factor calculated dynamically as 1 / (1 - resThreshold) to decouple division math from runtime hits.")]
		private float resScaleFactor;

		[Space(4)]

		[Header("Level Scaling")]
		[SerializeField]
		[Tooltip("Global weight modifier applied per point of level variance between caster and target.")]
		private float lvlFactor = 0.02f;

		public float DefFactor => this.defFactor;
		public float ResThreshold => this.resThreshold;
		public float LvlFactor => this.lvlFactor;
		public float ResScaleFactor => this.resScaleFactor;

		private void OnValidate() => CacheResScaleFactor();
		private void OnEnable() => CacheResScaleFactor();

		private void CacheResScaleFactor() {
			// Converts the linear cap into a normalized slope for the post-threshold curve:
			// Mult = 1 / (1 + Res * ScaleFactor) where ScaleFactor = 1 / (1 - Threshold)
			// Prevents absolute full immunity thresholds while maintaining continuous asset scaling growth.
			this.resScaleFactor = 1f / (1f - this.resThreshold);
		}

		protected virtual float GetDefenceMultiplier(float def, float pierce = 0f) {
			float effectiveDef = def * Mathf.Clamp01(1f - pierce);
			return this.defFactor / (this.defFactor + effectiveDef);
		}

		protected virtual float GetResistanceMultiplier(float res, DamageType type, float ignore = 0f) {
			float effectiveRes = res - ignore;

			// Branch 1: Sub-zero resistance scaling (Damage amplification phase)
			if (effectiveRes < 0f)
				return 1f - (effectiveRes * this.negResMult);

			// Branch 2: Standard linear protection zone (1:1 percentage evaluation)
			if (effectiveRes < this.resThreshold)
				return 1f - effectiveRes;

			// Branch 3: Diminishing returns calculation tracking asymptotic curves to block total mitigation limits
			return 1f / (1f + (effectiveRes * this.resScaleFactor));
		}

		protected virtual float GetLevelMultiplier(int casterLvl, float targetLvl) {
			float lvlDelta = casterLvl - targetLvl;
			float mult = (this.lvlFactor * Mathf.Abs(lvlDelta)) + 1f;

			// Inverse scaling matrix logic based on positive/negative attacker delta advantage positions
			return lvlDelta < 0f ? 1f / mult : mult;
		}

		public float TakeHit(DamageDetail details, float currentLvl, IStatSystem stats) {
			float def = stats.GetStatValue(CharacterStatType.DEF);
			float res = stats.GetResistanceValue(details.DamageType);

			float defMult = GetDefenceMultiplier(def, details.DefencePierceRatio);
			float resMult = GetResistanceMultiplier(res, details.DamageType, details.IgnoreResistance);
			float lvlMult = GetLevelMultiplier(details.CasterLevel, currentLvl);

			// Combines isolated systemic dimensions into unified final throughput scalar values
			return details.DamageAmount * defMult * resMult * lvlMult;
		}
	}
}