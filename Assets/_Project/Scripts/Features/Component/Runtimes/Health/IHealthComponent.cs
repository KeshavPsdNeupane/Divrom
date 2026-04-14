using System;

namespace Kope.Component.Health.Interface {

	public interface IHealthComponent {
		float CurrentHealth { get; }
		float MaxHealth { get; }
		event Action<float> OnMaxHealthChanged;
		event Action<float> OnCurrentHealthChanged;
		void Heal(float amount);
		void Heal(float flatAmount, float percentage);
		void ApplyDamage(float amount);
	}
}
