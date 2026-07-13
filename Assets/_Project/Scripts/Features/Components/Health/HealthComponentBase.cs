using System;
using Kope.Character.Stats;
using Kope.Component.Health.Interface;
using Kope.Core.Attribute;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.LifeTimeManagement;
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

		private CharacterStatsSystemBase characterStatsSystem;

		public float CurrentHealth => this.currentHealth;
		public float MaxHealth => this.maxHealth;

		private event Action<HealthChangeInfo> _onHealthChange;

		protected void InvokeHpChange(HealthChangeInfo info) {
			Debug.Log($"HealthChangeEvent Invoked: {info}");
			this._onHealthChange?.Invoke(info);
		}



		protected override bool OnInit() {
			if (ecr == null) return false;
			if (ecr.ComponentRegistry.TryGetMutable(out characterStatsSystem)) {
				return true;
			}
			return false;
		}

		protected virtual void OnEnable() => SubscribeToStats();
		protected virtual void OnDisable() => UnsubscribeToStats();


		/// <summary>
		/// Toggles the registration of a listener callback for health change updates.
		/// </summary>
		/// <param name="action">The listener callback to register or unregister.</param>
		/// <param name="subscribe">Pass <c>true</c> to subscribe the listener, or <c>false</c> to unsubscribe it.</param>
		/// <remarks>
		/// To prevent memory leaks and accidental double-subscriptions, this method completely removes the 
		/// callback from the underlying invocation list before re-adding it if <paramref name="subscribe"/> is true.
		/// </remarks>
		public void OnHealthChange(Action<HealthChangeInfo> action, bool subscribe) {
			this._onHealthChange -= action;
			if (subscribe) {
				this._onHealthChange += action;
			}
		}

		private void SubscribeToStats() {
			// now IsInitialized garuntte the characterStatSyste internal is fully configured, so we 
			// can safely subscribe to the stat change event and get the initial max health value.
			if (this.characterStatsSystem != null && this.characterStatsSystem.IsInitialized) {
				this.characterStatsSystem.StatsSubscribe(CharacterStatType.HP, SetMaxHealth);
				SetMaxHealth(this.characterStatsSystem.CurrentStats[CharacterStatType.HP].GetValue());
			}
		}

		private void UnsubscribeToStats() {
			if (this.characterStatsSystem != null && this.characterStatsSystem.IsInitialized) {
				this.characterStatsSystem.StatsUnsubscribe(CharacterStatType.HP, SetMaxHealth);
			}
		}

		/// <summary>
		/// Updates the maximum health threshold and adjusts current health accordingly.
		/// </summary>
		/// <remarks>
		/// <para>
		/// When max health increases, the current health is boosted by the difference 
		/// to reward the player instantly (e.g., equipping a health item).
		/// </para>
		/// <para>
		/// When max health decreases, current health is not penalized or reduced, 
		/// unless it exceeds the new maximum cap, in which case it is safely clamped.
		/// </para>
		/// </remarks>
		/// <param name="newMaxHealth">The target maximum health value to apply.</param>
		private void SetMaxHealth(float newMaxHealth) {
			float previousCurrentHealth = this.currentHealth;
			float previousMaxHealth = this.maxHealth;

			float maxHealthDifference = newMaxHealth - previousMaxHealth;
			this.maxHealth = newMaxHealth;

			// Only heal on increase; do not penalize current health on decrease.
			if (maxHealthDifference > 0) {
				this.currentHealth += maxHealthDifference;
			}

			this.currentHealth = Mathf.Clamp(this.currentHealth, 0f, this.maxHealth);

			this._onHealthChange?.Invoke(new HealthChangeInfo(
				previousCurrentHealth,
				this.currentHealth,
				this.maxHealth,
				HealthChangeType.MaxHealthChanged
			));
		}
		public void Heal(float amount) {
			if (!this.IsInitialized) return;
			float previousHealth = this.currentHealth;
			this.currentHealth = Mathf.Clamp(this.currentHealth + amount, 0, this.maxHealth);
			this._onHealthChange?.Invoke(
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
			this._onHealthChange?.Invoke(
				new HealthChangeInfo(previousHealth, this.currentHealth, this.maxHealth,
				HealthChangeType.Damage
				)
			);
		}


		public ISaveData GetSaveData() => new HealthComponentSaveData(this.currentHealth);



		public void LoadFromSaveData(ISaveData data) {
			if (data is HealthComponentSaveData healthData) {
				this.currentHealth = healthData.CurrentHealth;
				this._onHealthChange?.Invoke(
					new HealthChangeInfo(0, this.currentHealth, this.maxHealth, HealthChangeType.LoadFromSave)
				);
			}
		}
	}
}