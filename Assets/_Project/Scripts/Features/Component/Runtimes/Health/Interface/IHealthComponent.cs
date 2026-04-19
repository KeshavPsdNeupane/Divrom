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

		public HealthChangeInfo(float previousHealth, float currentHealth, float maxHealth, HealthChangeType changeType) {
			PreviousHealth = previousHealth;
			CurrentHealth = currentHealth;
			MaxHealth = maxHealth;
			ChangeType = changeType;
		}
	}
	public interface IHealthComponent : IHealable {
		float CurrentHealth { get; }
		float MaxHealth { get; }

		event Action<HealthChangeInfo> OnMaxHealthChanged;
		event Action<HealthChangeInfo> OnCurrentHealthChanged;

		void ApplyDamage(float amount);
	}
}
