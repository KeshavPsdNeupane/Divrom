using System;

namespace Kope.Component.Health.Interface {

	public interface IHealthComponent : IHealable {
		float CurrentHealth { get; }
		float MaxHealth { get; }
		event Action<float> OnMaxHealthChanged;
		event Action<float> OnCurrentHealthChanged;
		void ApplyDamage(float amount);
	}
}
