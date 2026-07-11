namespace Kope.Component.Health.Interface {
	public interface IHealable {
		void Heal(float amount);
		void Heal(float flatAmount, float percentage);
	}
}