using System;
using Kope.Character.Stats;
using Kope.Component.Health.Interface;
using Kope.Core.Attribute;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using Kope.SaveSystem;
using Newtonsoft.Json;
using UnityEngine;

namespace Kope.Component.Health {


	/// <summary>
	/// SaveData implementation for HealthComponentBase. Currently only saves current health.
	/// It should only store the CurrentHealth only, since we can grab the Maxhp and def from 
	/// character stats system and we can store them in the save data of character stats system, 
	/// so there is no need to store them here.
	/// Even it is a class but the underying data is immutable, since we only set the current
	/// health when we load from save data, and we will not change the current health in the save data after that,
	/// so it is effectively immutable.
	/// </summary>
	[SaveId("health_data")]
	public class HealthComponentSaveData : ISaveData {
		[JsonProperty("chp")]
		public float CurrentHealth { get; private set; }

		public HealthComponentSaveData(float currentHealth) {
			this.CurrentHealth = currentHealth;
		}
	}

	[SaveId("health")]
	public class HealthComponentBase : InitializableBase, IHealthComponent, ISaveable, IHealable {
		[SerializeField] EntityComponentsRegistry ecr;

		[SerializeField, ReadOnly] protected float maxHealth = 10;
		[SerializeField, ReadOnly] protected float currentHealth = 10;

		private CharacterStatsSystem characterStatsSystem;

		public float CurrentHealth => currentHealth;
		public float MaxHealth => maxHealth;

		public event Action<float> OnMaxHealthChanged;
		public event Action<float> OnCurrentHealthChanged;

		protected override bool OnInit() {
			if (ecr == null) return false;

			if (ecr.ComponentRegistry.TryGetMutatableComponent(out characterStatsSystem)) {
				return true;
			}
			return false;
		}

		private void OnEnable() => SubscribeToStats();
		private void OnDisable() => UnsubscribeToStats();

		private void SubscribeToStats() {
			if (characterStatsSystem?.CurrentStats != null) {
				characterStatsSystem.StatsSubscribe(CharacterStatType.HP, SetMaxHealth);
				SetMaxHealth(characterStatsSystem.CurrentStats[CharacterStatType.HP].GetValue());
				this.currentHealth = this.maxHealth;
			}
		}

		private void UnsubscribeToStats() {
			if (characterStatsSystem?.CurrentStats != null) {
				characterStatsSystem.StatsUnsubscribe(CharacterStatType.HP, SetMaxHealth);
			}
		}

		private void SetMaxHealth(float newHealth) {
			maxHealth = newHealth;
			currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
			OnMaxHealthChanged?.Invoke(maxHealth);
		}

		public void Heal(float amount) {
			currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
			OnCurrentHealthChanged?.Invoke(currentHealth);
		}
		public void Heal(float flatAmount, float percentage) {
			float healAmount = flatAmount + maxHealth * percentage;
			Heal(healAmount);
		}
		/// <summary>
		/// Simple entry point for pre-calculated damage.
		/// </summary>
		public void ApplyDamage(float amount) {
			if (!IsInitialized) return;

			currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
			OnCurrentHealthChanged?.Invoke(currentHealth);
		}

		public ISaveData GetSaveData() => new HealthComponentSaveData(currentHealth);

		public void LoadFromSaveData(ISaveData data) {
			if (data is HealthComponentSaveData healthData) {
				currentHealth = healthData.CurrentHealth;
				OnCurrentHealthChanged?.Invoke(currentHealth);
			}
		}
	}
}