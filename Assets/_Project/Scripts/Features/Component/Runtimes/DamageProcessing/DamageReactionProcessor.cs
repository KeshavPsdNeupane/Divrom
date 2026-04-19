using System.Collections.Generic;
using Kope.Character.Stats;
using Kope.Component.Health.Interface;
using Kope.Component.Combat.Interface;
using Kope.Component.HitBox.Interface;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using UnityEngine;
using Kope.Component.Health;

namespace Kope.Component.Combat {
	/// <summary>
	/// This component is responsible for processing incoming damage and effects on an entity. 
	/// It listens for hits on the attached HurtBox and applies damage and effects accordingly.
	/// If a entity his this component, then that entity is expected fight back to player, so be careful where you put this.<br/>
	/// <b> Important: </b><br/>
	/// - This component assumes that the entity has a HealthComponent and an IStatSystem for it to function properly. <br/>
	/// - The main reason of seperation of this component from the HealthComponent is to allow for more flexible and 
	/// modular damage processing logic, as well as to keep the HealthComponent focused solely on managing
	/// health values and related mechanics. <br/>
	/// - and not all entity needs below whole complex damage processing, for example,
	///  a destructible environment might just need to get destroyed on a single hit, we can just create 1HitEntityComponent.
	/// 	which will handle that event rather than this bloat of component. <br/>
	/// </summary>
	public class DamageReactionProcessor : InitializableBase, IDamageProcessor {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private HealthComponentConfig config;

		private IHitBoxComponent hurtBox;
		private IHealthComponent healthComponent;
		private IStatSystem statSystem;

		public IHitBoxComponent HurtBox => this.hurtBox;

		private readonly List<ITickableEffect> _activeTickableEffects = new();
		// replace this with the actual LevelUp component when we have it, this is just for
		// testing the level scaling portion of the damage formula
		private readonly float _currentLevel = 0;


		private float ResistanceDiminishingReturnsThreshold => config.ResistanceDiminishingReturnsThreshold;
		private float DefenceScalingFactor => config.DefenceScalingFactor;
		private float LevelScalingFactor => config.LevelScalingFactor;
		private float InverseResistanceDiminishingReturnsThreshold => config.ReciprocalOfResistanceDiminishingReturnsThreshold;

		protected override bool OnInit() {
			if (this.ecr == null) {
				Debug.LogError($"DamageProcessor on {gameObject.name} has no ECR assigned.");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out healthComponent)) {
				Debug.LogError($"DamageProcessor on {gameObject.name} failed to find HealthComponent.");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetReadOnlyComponent(out statSystem)) {
				Debug.LogError($"DamageProcessor on {gameObject.name} failed to find IStatSystem.");
				return false;
			}

			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out this.hurtBox)) {
				Debug.LogError($"DamageProcessor on {gameObject.name} failed to find HurtBox.");
				return false;
			}

			return true;
		}

		private void OnEnable() {
			if (this.hurtBox == null) return;
			this.hurtBox.OnHitCombatible += HandleHurtBoxHit;
		}

		private void OnDisable() {
			if (this.hurtBox != null) {
				this.hurtBox.OnHitCombatible -= HandleHurtBoxHit;
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
			float currentDef = this.statSystem.GetStatValue(CharacterStatType.DEF);
			float effectiveDefence = currentDef * Mathf.Clamp01(1 - pierceRatio);
			return this.DefenceScalingFactor / (this.DefenceScalingFactor + effectiveDefence);
		}

		protected virtual float GetResistanceMultiplier(DamageType damageType, float ignore = 0) {
			float resistanceValue = this.statSystem.GetResistanceValue(damageType);
			float er = resistanceValue - ignore;
			if (er < 0) return 1f - (er * 0.5f);
			if (er < this.ResistanceDiminishingReturnsThreshold) return 1f - er;
			return 1f / (1f + er * this.InverseResistanceDiminishingReturnsThreshold);
		}

		protected virtual float GetLevelMultiplier(int casterLevel = 0) {
			float lvlDiff = casterLevel - this._currentLevel;

			float temp = this.LevelScalingFactor * Mathf.Abs(lvlDiff) + 1;
			return lvlDiff < 0f ? 1f / temp : temp;
		}

		#endregion

		public void TakeDamageDebugOnly(int amount) {
			TakeHit(new DamageDetail(amount, null, DamageType.Physical));
		}

		public float TakeHit(DamageDetail damageDetail) {
			if (!this.IsInitialized) return 0f;

			float defMult = GetDefenceMultiplier(damageDetail.DefencePierceRatio);
			float resMult = GetResistanceMultiplier(damageDetail.DamageType, damageDetail.IgnoreResistance);
			float levelMult = GetLevelMultiplier(damageDetail.CasterLevel);

			float finalDamage = damageDetail.DamageAmount * defMult * resMult * levelMult;
			this.healthComponent.ApplyDamage(finalDamage);
			return finalDamage;
		}

		public void ApplyEffect(IEffect<ICombatable> effect) {
			if (!this.IsInitialized || effect == null) return;

			if (effect is ITickableEffect tickable) {
				this._activeTickableEffects.Add(tickable);
				tickable.OnCompletedOrCancelled += OnEffectExpired;
			}
			effect.Apply(this);
		}

		private void OnEffectExpired(ITickableEffect effect) {
			effect.OnCompletedOrCancelled -= OnEffectExpired;
			this._activeTickableEffects.Remove(effect);
		}

		private void ClearActiveEffects() {
			for (int i = this._activeTickableEffects.Count - 1; i >= 0; i--) {
				this._activeTickableEffects[i]?.Cancel();
			}
			this._activeTickableEffects.Clear();
		}

		private void HandleHurtBoxHit(CombatibleHitInfo hitInfo) {
			if (!this.IsInitialized || hitInfo.Effects == null) return;

			for (int i = 0; i < hitInfo.Effects.Count; i++) {
				var effect = hitInfo.Effects[i]?.Create(hitInfo.EffectContext);
				ApplyEffect(effect);
			}
		}
	}
}