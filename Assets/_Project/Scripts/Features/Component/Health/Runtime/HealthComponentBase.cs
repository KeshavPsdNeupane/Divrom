using System;
using Kope.Character.Stats;
using Kope.Component.Movement;
using Kope.Core.Attribute;
using Kope.Core.EntityComponentSystem;
using Kope.Core.Init;
using UnityEngine;


namespace Kope.Component.Health {

	public interface IHealthComponent {
		float CurrentHealth { get; }
		float MaxHealth { get; }

		event Action<float> OnMaxHealthChanged;
		event Action<float> OnCurrentHealthChanged;
	}


	// this will be moved to HurtBoxComponentBase in the future, since not all 
	// damageable entities need to have health component, but for now it's fine to 
	// just put it here since we only have one type of damageable entity which is the player.
	public interface IDamageable {
		void TakeHit(DamageDetail damageDetail);
	}

	public struct DamageDetail {
		public GameObject Source;
		public DamageType DamageType;
		public int LevelDifference;
		public float DamageAmount;
		public float DefencePierceRatio;
		public float IgnoreResistance;
		// knockback info will be handled in the future when we implement the knockback system, 
		// for now we will just ignore it.
		// public float KnockbackForce;
		// public Vector3 KnockbackDirection;

		public DamageDetail(
			float damageAmount,
			GameObject source,
			DamageType damageType,
			float defencePierceRatio = 0,
			float ignoreResistance = 0,
			int levelDifference = 0
			/*float knockbackForce = 0,
			Vector3 knockbackDirection = default*/) {
			this.DamageAmount = damageAmount;
			this.Source = source;
			this.DamageType = damageType;
			this.DefencePierceRatio = defencePierceRatio;
			this.IgnoreResistance = ignoreResistance;
			this.LevelDifference = levelDifference;
			// this.KnockbackForce = knockbackForce;
			// this.KnockbackDirection = knockbackDirection;
		}
	}

	public class HealthComponentBase : InitializableBase, IDamageable, IHealthComponent {

		[SerializeField] EntityComponentsRegistry ecr;
		[SerializeField] HealthComponentConfig config;
		[SerializeField, ReadOnly, Tooltip("The maximum health value for this entity.")]
		protected float maxHealth = 10;

		[SerializeField, ReadOnly, Tooltip("The current health value for this entity." +
		"It is updated based on healing and damage taken, and should not exceed maxHealth.")]
		protected float currentHealth = 10;

		[SerializeField, ReadOnly, Tooltip("The defense value for this entity, which reduces incoming damage.")]
		protected float defence;

		private CharacterStatsSystem characterStatsSystem;

		// not inplemented yet, will be used for knockback in the future when we implement the knockback system.
		// will be removed from To MovementComponentBase, since not all damageable entities 
		// need to have movement component, but for now putting here to remind us that we 
		// will need to interact with movement component when we implement knockback.
#pragma warning disable IDE0044 // Add readonly modifier
		private MovementComponentBase movementComponent;
#pragma warning restore IDE0044 // Add readonly modifier


		public float CurrentHealth => this.currentHealth;
		public float MaxHealth => this.maxHealth;

		private float ResistanceDiminishingReturnsThreshold => this.config.ResistanceDiminishingReturnsThreshold;
		private float DefenceScalingFactor => this.config.DefenceScalingFactor;
		private float LevelScalingFactor => this.config.LevelScalingFactor;
		private float InverseResistanceDiminishingReturnsThreshold => this.config.ReciprocalOfResistanceDiminishingReturnsThreshold;


		public event Action<float> OnMaxHealthChanged;
		public event Action<float> OnCurrentHealthChanged;

		private void SetMaxHealth(float newHealth) {
			this.maxHealth = newHealth;
			this.currentHealth = Mathf.Clamp(this.currentHealth, 0, this.maxHealth);
			this.OnMaxHealthChanged?.Invoke(this.maxHealth);
		}
		private void SetDefence(float newDefence) {
			this.defence = newDefence;
		}

		protected override bool OnInit() {
			if (this.config == null) {
				Debug.LogError("HealthComponentBase requires a HealthComponentConfig reference." + GetParentGameObjectHeirarchyMessage());
				return false;
			}
			if (this.ecr == null) {
				Debug.LogError("HealthComponentBase requires an EntityComponentsRegistry reference." + GetParentGameObjectHeirarchyMessage());
				return false;
			}
			if (!this.ecr.ComponentRegistry.TryGetMutatableComponent(out CharacterStatsSystem characterStatsSystem)) {
				Debug.LogError("HealthComponentBase requires a CharacterStatsSystem component in the EntityComponentsRegistry." + GetParentGameObjectHeirarchyMessage());
				return false;
			} else {
				this.characterStatsSystem = characterStatsSystem;
			}
			return true;
		}

		private void OnEnable() => SubScribeToStats();
		private void OnDisable() => UnSubScribeToStats();


		private void SubScribeToStats() {
			if (this.characterStatsSystem != null &&
				this.characterStatsSystem.CurrentStats != null) {
				this.characterStatsSystem.StatsSubscribe(CharacterStatType.HP, SetMaxHealth);
				this.characterStatsSystem.StatsSubscribe(CharacterStatType.DEF, SetDefence);
				// initial fetch
				SetMaxHealth(this.characterStatsSystem.CurrentStats[CharacterStatType.HP].GetValue());
				this.currentHealth = this.maxHealth;
				// setting current health to max health on initialization, 
				// can be changed later if we want to have entities that spawn with less than max health.
				SetDefence(this.characterStatsSystem.CurrentStats[CharacterStatType.DEF].GetValue());
				Debug.Log($"Hp = {this.currentHealth} Def = {this.defence}");
			}
		}
		private void UnSubScribeToStats() {
			if (this.characterStatsSystem != null &&
				this.characterStatsSystem.CurrentStats != null) {
				this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.HP, SetMaxHealth);
				this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.DEF, SetDefence);
			}
		}

		/// <summary>
		/// Calculates the final damage after applying defense, resistance, and level multipliers, 
		/// then reduces current health by that amount.
		/// </summary>
		/// <param name="damageDetail"></param>
		public virtual void TakeHit(DamageDetail damageDetail) {
			// so no need to do extra null checks for characterStatsSystem and its stats,
			// since if we are not initialized, we will just ignore the damage. 
			// this is to prevent any potential issues with the order of initialization and enabling of components.
			if (!this.IsInitialized) return;
			float defMult = GetDefenceMultiplier(damageDetail.DefencePierceRatio);
			float resMult = GetResistanceMultiplier(damageDetail.DamageType, damageDetail.IgnoreResistance);
			float levelMult = GetLevelMultiplier(damageDetail.LevelDifference);
			float finalDamage = damageDetail.DamageAmount * defMult * resMult * levelMult;
			this.currentHealth = Mathf.Clamp(this.currentHealth - finalDamage, 0, this.maxHealth);
			this.OnCurrentHealthChanged?.Invoke(this.currentHealth);
			// knock logic will be handled in the future when we implement the knockback system, 
			// for now we will just ignore it.
		}

		/// <summary>
		/// Calculates the resistance multiplier based on the entity's resistance stat for the given damage type and any ignore value.
		/// The formula is as follows:<br/>
		/// If effective resistance (resistance value - ignore) is negative,
		/// the multiplier increases damage by half of the negative resistance. <br/>
		/// If effective resistance is between 0 and 0.8, the multiplier reduces
		/// damage by the effective resistance value.<br/>
		/// If effective resistance is greater than or equal to 0.8, the multiplier 
		/// applies diminishing returns to prevent resistance<br/>
		/// </summary>
		/// <param name="damageType"></param>
		/// <param name="ignore"></param>
		/// <returns></returns>
		protected virtual float GetResistanceMultiplier(DamageType damageType, float ignore = 0) {
			// this formula is from Gatcha Game Genshin Impact, Credit to their Wiki.
			float resistanceValue = this.characterStatsSystem.GetResistanceValue(damageType);
			float er = resistanceValue - ignore;
			if (er < 0) return 1.0f - (er * 0.5f);
			if (er >= 0f && er < this.ResistanceDiminishingReturnsThreshold) return 1.0f - er;
			else return 1 / (1f + er * this.InverseResistanceDiminishingReturnsThreshold);
		}

		protected virtual float GetDefenceMultiplier(float pierceRatio = 0) {
			// this is from the formula League of Legends uses for their armor,
			// which is a pretty standard formula for defense in many games.
			// defence work for both physical and magical damage, 
			// specific dmage type reduction will be handled by resistance, 
			// so we dont need to differentiate physical and magical defense for now.
			// pierce ratio is the percentage of defense that is ignored, so if pierce ratio is 0.2, 
			// then 20% of the defense is ignored. and we dont want pierce ratio to be greater than 1, 
			// because that would mean we are ignoring more than 100% of the defense, 
			// which doesnt make sense. so we clamp it between 0 and 1.
			float effectiveDefence = this.defence * Mathf.Clamp01(1 - pierceRatio);
			return this.DefenceScalingFactor / (this.DefenceScalingFactor + effectiveDefence);
		}


		protected virtual float GetLevelMultiplier(int levelDifference = 0) {
			// this gives X% more damage for each level the attacker is above the defender, and 
			// (since 1/X formula) ~X% less damage for each level the attacker is below the defender.
			// so there is gives a noticeable difference in damage when there is a level difference, 
			// but it is not too extreme.
			float temp = this.LevelScalingFactor * levelDifference + 1;
			if (levelDifference < 0) temp = 1 / temp;
			return temp;
		}


		/// <summary>
		/// Used for dubugging purposes, directly reduces HP by a specified amount, ignoring all calculations.
		/// </summary>
		/// <param name="amount"></param>
		public void ReduceHp(float amount, float minHealthAmount = 0.2f) {
			this.currentHealth = Mathf.Clamp(
				this.currentHealth - amount,
				this.maxHealth * minHealthAmount,
				this.maxHealth
			);
			this.OnCurrentHealthChanged?.Invoke(this.currentHealth);
		}


	}
}