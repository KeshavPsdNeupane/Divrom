using System;

namespace Kope.Component.Health.Interface {
	public enum HealthChangeType {
		LoadFromSave = 0,
		Heal = 1,
		Damage = 2,
		MaxHealthChanged = 3
	}
	public readonly struct HealthChangeInfo {
		public float PreviousHealth { get; }
		public float CurrentHealth { get; }
		public float MaxHealth { get; }
		public HealthChangeType ChangeType { get; }
		public readonly bool ShowFloatingText;

		public HealthChangeInfo(float previousHealth,
		float currentHealth, float maxHealth,
		HealthChangeType changeType, bool showFloatingText = true) {
			PreviousHealth = previousHealth;
			CurrentHealth = currentHealth;
			MaxHealth = maxHealth;
			ChangeType = changeType;
			this.ShowFloatingText = showFloatingText;
		}

		public override string ToString() {
			return $"HealthChangeInfo(PreviousHealth: {PreviousHealth}, CurrentHealth: {CurrentHealth}, MaxHealth: {MaxHealth}, ChangeType: {ChangeType}, ShowFloatingText: {ShowFloatingText})";
		}
	}
	public interface IHealthComponent : IHealable {
		float CurrentHealth { get; }
		float MaxHealth { get; }
		void OnHealthChange(Action<HealthChangeInfo> action, bool subscribe);

		void ApplyDamage(float amount);
	}
}
