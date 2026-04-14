using Kope.Character.Stats;
using Kope.Component.Health;
using Kope.Component.Health.Interface;
using Kope.Component.Combat.Interface;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component.Combat {
	public class CombatComponent : InitializableBase, ICombatComponent {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private HealthComponentConfig config;
		private IHealthComponent healthComponent;
		private CharacterStatsSystem characterStatsSystem;
		private IMovementComponent movement;

		private float ResistanceDiminishingReturnsThreshold => config.ResistanceDiminishingReturnsThreshold;
		private float DefenceScalingFactor => config.DefenceScalingFactor;
		private float LevelScalingFactor => config.LevelScalingFactor;
		private float InverseResistanceDiminishingReturnsThreshold => config.ReciprocalOfResistanceDiminishingReturnsThreshold;

		protected override bool OnInit() {
			if (this.ecr == null) {
				Debug.LogError($"CombatComponent on {gameObject.name} has no EntityComponentsRegistry assigned." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out healthComponent)) {
				Debug.LogError($"CombatComponent on {gameObject.name} failed to find HealthComponent in ECR." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out characterStatsSystem)) {
				Debug.LogError($"CombatComponent on {gameObject.name} failed to find CharacterStatsSystem in ECR." +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out movement)) {
				Debug.LogWarning($"CombatComponent on {gameObject.name} failed to find MovementComponent in ECR. " +
				"Knockback will be unavailable.\n" + GetParentGameObjectHeirarchyMessage());
			}

			return true;
		}

		#region Damage Formula Logic

		protected virtual float GetDefenceMultiplier(float pierceRatio = 0) {
			float currentDef = this.characterStatsSystem.GetStatValue(CharacterStatType.DEF);
			float effectiveDefence = currentDef * Mathf.Clamp01(1 - pierceRatio);
			return this.DefenceScalingFactor / (this.DefenceScalingFactor + effectiveDefence);
		}

		protected virtual float GetResistanceMultiplier(DamageType damageType, float ignore = 0) {
			float resistanceValue = this.characterStatsSystem.GetResistanceValue(damageType);
			float er = resistanceValue - ignore;
			if (er < 0) return 1f - (er * 0.5f);
			if (er < this.ResistanceDiminishingReturnsThreshold) return 1f - er;
			return 1f / (1f + er * this.InverseResistanceDiminishingReturnsThreshold);
		}

		protected virtual float GetLevelMultiplier(int levelDifference = 0) {
			float temp = this.LevelScalingFactor * levelDifference + 1;
			return levelDifference < 0f ? 1f / temp : temp;
		}

		#endregion

		public void TakeDamageDebugOnly(int amount) {
			TakeHit(new DamageDetail(amount, null, DamageType.Physical));
		}

		public bool ApplyStatModifier(StatModifier effect) {
			if (!this.IsInitialized || effect == null || this.characterStatsSystem == null) return false;
			return this.characterStatsSystem.AddStatModifier(effect);
		}

		public void ApplyKnockback(Vector3 direction, float duration, float impulse) {
			if (!this.IsInitialized || this.movement == null) return;
			this.movement.ApplyKnockback(direction, duration, impulse);
		}

		public float TakeHit(DamageDetail damageDetail) {
			if (!this.IsInitialized) return 0f;

			float defMult = GetDefenceMultiplier(damageDetail.DefencePierceRatio);
			float resMult = GetResistanceMultiplier(damageDetail.DamageType, damageDetail.IgnoreResistance);
			float levelMult = GetLevelMultiplier(damageDetail.LevelDifference);

			float finalDamage = damageDetail.DamageAmount * defMult * resMult * levelMult;
			this.healthComponent.ApplyDamage(finalDamage);
			return finalDamage;
		}
		public void Heal(float flatHealAmount, float healPercentage) {
			if (!this.IsInitialized) return;
			this.healthComponent.Heal(flatHealAmount, healPercentage);
		}
	}
}