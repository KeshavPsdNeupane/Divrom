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

		public float CurrentHealth => this.currentHealth;
		public float MaxHealth => this.maxHealth;

		public event Action<HealthChangeInfo> OnMaxHealthChanged;
		public event Action<HealthChangeInfo> OnCurrentHealthChanged;

		protected void InvokeHpChange(HealthChangeInfo info) {
			this.OnCurrentHealthChanged?.Invoke(info);
		}

		protected override bool OnInit() {
			if (ecr == null) return false;
			if (ecr.ComponentRegistry.TryGetMutatableComponent(out characterStatsSystem)) {
				return true;
			}
			return false;
		}

		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable() => UnsubscribeToStats();

		private void SubscribeToStats() {
			// now IsInitialized garuntte the characterStatSyste internal is fully configured, so we 
			// can safely subscribe to the stat change event and get the initial max health value.
			if (this.characterStatsSystem != null && this.characterStatsSystem.IsInitialized) {
				this.characterStatsSystem.StatsSubscribe(CharacterStatType.HP, SetMaxHealth);
				SetMaxHealth(this.characterStatsSystem.CurrentStats[CharacterStatType.HP].GetValue());
				this.currentHealth = this.maxHealth;
			}
		}

		private void UnsubscribeToStats() {
			if (this.characterStatsSystem != null && this.characterStatsSystem.IsInitialized) {
				this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.HP, SetMaxHealth);
			}
		}

		private void SetMaxHealth(float newHealth) {
			this.maxHealth = newHealth;
			this.currentHealth = Mathf.Clamp(this.currentHealth, 0, this.maxHealth);
			this.OnMaxHealthChanged?.Invoke(
				new HealthChangeInfo(this.currentHealth,
				this.currentHealth, this.maxHealth,
				HealthChangeType.MaxHealthChanged));
		}

		public void Heal(float amount) {
			if (!this.IsInitialized) return;
			float previousHealth = this.currentHealth;
			this.currentHealth = Mathf.Clamp(this.currentHealth + amount, 0, this.maxHealth);
			// Debug.Log($"HealthComponentBase: Applied heal {amount}, health changed from {previousHealth} " +
			// $"to {this.currentHealth}.");
			this.OnCurrentHealthChanged?.Invoke(
				new HealthChangeInfo(previousHealth, this.currentHealth, this.maxHealth,
				HealthChangeType.Heal
				)
			);
		}

		public void Heal(float flatAmount, float percentage) {
			if (!this.IsInitialized) return;
			float healAmount = flatAmount + this.maxHealth * percentage;
			Heal(healAmount);
		}
		/// <summary>
		/// Simple entry point for pre-calculated damage.
		/// </summary>
		public void ApplyDamage(float amount) {
			if (!this.IsInitialized) return;

			float previousHealth = this.currentHealth;
			this.currentHealth = Mathf.Clamp(this.currentHealth - amount, 0, this.maxHealth);
			this.OnCurrentHealthChanged?.Invoke(
				new HealthChangeInfo(previousHealth, this.currentHealth, this.maxHealth,
				HealthChangeType.Damage
				)
			);
		}

		public ISaveData GetSaveData() => new HealthComponentSaveData(this.currentHealth);

		public void LoadFromSaveData(ISaveData data) {
			if (data is HealthComponentSaveData healthData) {
				this.currentHealth = healthData.CurrentHealth;
				this.OnCurrentHealthChanged?.Invoke(
					new HealthChangeInfo(0, this.currentHealth, this.maxHealth, HealthChangeType.LoadFromSave)
				);
			}
		}
	}
}