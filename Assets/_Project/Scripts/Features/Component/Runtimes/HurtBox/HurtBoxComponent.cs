using System.Collections.Generic;
using Kope.Character.Stats;
using Kope.Core.Init;
using Kope.Core.EntityComponentRegistry;
using UnityEngine;
using Kope.Component.HurtBox.Interface;
using Kope.Component.Health;
using Kope.Component.Movement;
using Kope.Component.Health.Interface;

namespace Kope.Component.HurtBox {
	/// <summary>
	/// Data container for a hit event. Includes source, damage math parameters,
	/// and level scaling data.
	/// </summary>
	public struct DamageDetail {
		public GameObject Source;
		public DamageType DamageType;
		public int LevelDifference;
		public float DamageAmount;
		public float DefencePierceRatio;
		public float IgnoreResistance;

		public DamageDetail(
			float damageAmount,
			GameObject source,
			DamageType damageType,
			float defencePierceRatio = 0,
			float ignoreResistance = 0,
			int levelDifference = 0) {
			this.DamageAmount = damageAmount;
			this.Source = source;
			this.DamageType = damageType;
			this.DefencePierceRatio = defencePierceRatio;
			this.IgnoreResistance = ignoreResistance;
			this.LevelDifference = levelDifference;
		}
	}

	public class HurtBoxComponent : InitializableBase, IDamageable {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private HealthComponentConfig config;
		private IHealthComponent healthComponent;
		private IMovementComponent movement;
		private CharacterStatsSystem characterStatsSystem;

		private readonly List<IEffect<IDamageable>> _activeEffects = new();
		private readonly List<ITickableEffect> _activeTickableEffects = new();

		private float ResistanceDiminishingReturnsThreshold => config.ResistanceDiminishingReturnsThreshold;
		private float DefenceScalingFactor => config.DefenceScalingFactor;
		private float LevelScalingFactor => config.LevelScalingFactor;
		private float InverseResistanceDiminishingReturnsThreshold => config.ReciprocalOfResistanceDiminishingReturnsThreshold;

		protected override bool OnInit() {
			if (this.ecr == null) {
				Debug.LogError($"HurtBoxComponent on {gameObject.name} has no EntityComponentsRegistry assigned."
				+ GetParentGameObjectHeirarchyMessage());
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out healthComponent)) {
				Debug.LogError($"HurtBoxComponent on {gameObject.name} failed to find HealthComponent in ECR."
				+ GetParentGameObjectHeirarchyMessage());
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out characterStatsSystem)) {
				Debug.LogError($"HurtBoxComponent on {gameObject.name} failed to find CharacterStatsSystem in ECR."
				+ GetParentGameObjectHeirarchyMessage());
				return false;
			}

			// Look for movement/knockback capabilities
			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out movement)) {
				Debug.LogWarning($"HurtBoxComponent on {gameObject.name} failed to find KnockbackComponent in ECR. " +
				"Knockback will be unavailable.\n" + GetParentGameObjectHeirarchyMessage());
			}
			return true;
		}

		protected override void OnUpdate() {
			if (_activeEffects.Count == 0) return;

			float deltaTime = Time.deltaTime;
			// it backwards to iterate but we might remove effects during the loop,
			// so it safer to go backwards to avoid skipping elements.
			for (int i = this._activeTickableEffects.Count - 1; i >= 0; i--) {
				this._activeTickableEffects[i]?.Tick(deltaTime);
			}
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

		public void ApplyKnockback(Vector3 direction, float duration, float impulse) {
			if (!this.IsInitialized || this.movement == null) return;
			this.movement.ApplyKnockback(direction, duration, impulse);
		}

		#region Damage Formula Logic

		/// <summary>
		/// Calculates the defence multiplier based on the target's DEF stat and the pierce ratio of the attack.
		/// This is a hyperbolic function that approaches 0 as effective defence increases, and 
		/// approaches 1 as effective defence decreases.
		/// This formula is inspired from mainly LOL(League of Legends) Armor damage reduction formula.
		/// With little bit of influence from other ton of games for defence formula, and
		/// also taking into account the pierce ratio which is a common stat in many games 
		/// that reduces the effective defence of the target.
		/// And we can efficitively pierce through 100% defence with 100% pierce ratio, but we can never 
		/// have negative effective defence, so the minimum effective defence is 0.
		/// </summary> 
		protected virtual float GetDefenceMultiplier(float pierceRatio = 0) {
			float currentDef = this.characterStatsSystem.GetStatValue(CharacterStatType.DEF);
			float effectiveDefence = currentDef * Mathf.Clamp01(1 - pierceRatio);
			return this.DefenceScalingFactor / (this.DefenceScalingFactor + effectiveDefence);
		}



		/// <summary>
		/// Calculates the resistance multiplier based on the target's resistance stat for the given damage type, 
		/// and the ignore resistance value of the attack. This formula uses a diminishing returns approach to 
		/// ensure that as resistance increases, the additional benefit of more resistance decreases.
		/// The formula was inspired mainly from Genshin Impact's elemental resistance formula,
		/// Credit to the Wiki contributors who reverse engineered the formula,
		/// I did a little bit of liberty and made my own version. 
		/// </summary>
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

		#region IDamageable Implementation
		// for debug only 
		public void TakeDamageDebugOnly(int amount) {
			TakeHit(new DamageDetail(amount, null, DamageType.Physical));
		}

		public bool ApplyStatModifier(StatModifier effect) {
			if (!this.IsInitialized || effect == null || this.characterStatsSystem == null) return false;
			return this.characterStatsSystem.AddStatModifier(effect);
		}

		public void ApplyEffect(IEffect<IDamageable> effect) {
			if (effect == null) return;

			effect.OnCompleted += OnEffectCompleted;
			this._activeEffects.Add(effect);
			if (effect is ITickableEffect tickable) {
				this._activeTickableEffects.Add(tickable);
			}

			effect.Apply(this);
		}

		private void OnEffectCompleted(IEffect<IDamageable> effect) {
			effect.OnCompleted -= OnEffectCompleted;
			this._activeEffects.Remove(effect);
			if (effect is ITickableEffect tickable) {
				this._activeTickableEffects.Remove(tickable);
			}
		}
		#endregion
	}
}




