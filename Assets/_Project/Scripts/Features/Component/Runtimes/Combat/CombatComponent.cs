using System.Collections.Generic;
using Kope.Character.Stats;
using Kope.Component.Health;
using Kope.Component.Health.Interface;
using Kope.Component.Combat.Interface;
using Kope.Component.Movement;
using Kope.Component.HurtBox;
using Kope.Component.HurtBox.Interface;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Component.Combat {
	public class CombatComponent : InitializableBase, ICombatComponent {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private HealthComponentConfig config;
		[SerializeField] private IHurtBoxComponent hurtBox;
		private IHealthComponent healthComponent;
		private CharacterStatsSystem characterStatsSystem;
		private IMovementComponent movement;
		public IHurtBoxComponent HurtBox => this.hurtBox;

		/// <summary>
		/// List of active tickable effects currently applied to this combat component.
		/// The CombatComponent is responsible for ticking these effects and removing them when they expire or
		/// are cancelled. This allows for effects with durations, such as damage over time or Heal over time, to be properly managed and updated each frame while they are active on the combat component.
		/// The CombatComponent does not need to manage "instant" effects that apply their effect immediately a
		/// nd don't require ticking or tracking, such as stat modifiers, since those are handled 
		/// directly by the CharacterStatsSystem and don't require the CombatComponent to track 
		/// their lifecycle or update them over time.
		/// </summary>
		private readonly List<ITickableEffect> _activeTickableEffects = new();

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

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this.hurtBox)) {
				Debug.LogWarning($"CombatComponent on {gameObject.name} did not find a HurtBoxComponent in ECR. " +
				"HurtBox hit detection will be unavailable.\n" + GetParentGameObjectHeirarchyMessage());
				return false;
			}

			return true;
		}
		private void OnEnable() {
			if (this.hurtBox == null) return;
			this.hurtBox.OnHitEntity -= HandleHurtBoxHit;
			this.hurtBox.OnHitEntity += HandleHurtBoxHit;
		}

		private void OnDisable() {
			if (this.hurtBox != null) {
				this.hurtBox.OnHitEntity -= HandleHurtBoxHit;
			}

			ClearActiveEffects();
		}

		protected override void OnUpdate() {
			if (this._activeTickableEffects.Count == 0) return;

			float deltaTime = Time.deltaTime;
			for (int i = this._activeTickableEffects.Count - 1; i >= 0; i--) {
				this._activeTickableEffects[i]?.Tick(deltaTime);
			}
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
			// the stat system is like a "instant effect" that applies the modifier immediately 
			// and doesn't require tracking or ticking, and repply the modifier every time the stat is accessed,
			// so we can directly apply the modifier to the stat system without needing to track 
			// it as an active effect in the CombatComponent.
			var success = this.characterStatsSystem.AddStatModifier(effect);
			return success;
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

		public void ApplyEffect(IEffect<ICombatable> effect) {
			if (!this.IsInitialized || effect == null) return;

			if (effect is ITickableEffect tickable) {
				this._activeTickableEffects.Add(tickable);
				tickable.OnCompletedOrCancell += OnEffectExpired;
			}
			effect.Apply(this);
		}
		private void OnEffectExpired(ITickableEffect effect) {
			// only remove from active effects if it's actually in the list, to 
			// avoid potential issues with multiple events firing or effects being removed more than once.
			// if cancelled or completed effect will trigger the same event so we only need one event
			//  handler to remove the effect from the active list.
			effect.OnCompletedOrCancell -= OnEffectExpired;
			this._activeTickableEffects.Remove(effect);
		}

		private void ClearActiveEffects() {
			for (int i = this._activeTickableEffects.Count - 1; i >= 0; i--) {
				this._activeTickableEffects[i]?.Cancel();
			}
			this._activeTickableEffects.Clear();
		}

		private void HandleHurtBoxHit(HurtBoxHitInfo hitInfo) {
			if (!this.IsInitialized || hitInfo.Effects == null) return;

			for (int i = 0; i < hitInfo.Effects.Count; i++) {
				var effect = hitInfo.Effects[i]?.Create(hitInfo.EffectContext);
				ApplyEffect(effect);
			}
		}
	}
}
