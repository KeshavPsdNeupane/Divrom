using UnityEngine;
using UnityEngine.UI;

namespace Kope.Component.Health {
	/// <summary>
	/// A simple health bar script that updates a UI Slider based on the current health of an entity.
	/// It listens to changes in the health component and updates the fill amount of the slider accordingly
	/// This only work for 2D UI elements. For 3D health bars, a different approach would be needed,
	/// Like Fixing the Bar rotation to the MainCamera and fixing the position to entity position + offset.
	/// But for 2D there is nothing like rotation about camera, so just parenting the health bar to the entity
	/// and updating the fill amount is enough.
	/// </summary>
	public class HealthBar : MonoBehaviour {
		[SerializeField] private HealthComponentBase healthComponent;
		[SerializeField] private Slider healthBarFill;

		private void Start() {
			if (this.healthComponent == null) {
				Debug.LogError("HealthComponent reference is not set on HealthBar script.");
				return;
			}
			if (this.healthBarFill == null) {
				Debug.LogError("HealthBarFill reference is not set on HealthBar script.");
				return;
			}

			// Initialize health bar fill
			this.healthBarFill.value = this.healthComponent.CurrentHealth / this.healthComponent.MaxHealth;

			// Subscribe to health changes
			this.healthComponent.OnCurrentHealthChanged += UpdateHealthBar;
			this.healthComponent.OnMaxHealthChanged += UpdateHealthBar;
			UpdateHealthBar(0f); // Initial update to set the correct fill amount
		}
		private void UpdateHealthBar(float _) {
			if (this.healthComponent != null && this.healthBarFill != null) {
				float healthRatio = this.healthComponent.CurrentHealth / this.healthComponent.MaxHealth;
				this.healthBarFill.value = healthRatio;
			}
		}

		private void OnDestroy() {
			// Unsubscribe from events to prevent memory leaks
			if (this.healthComponent != null) {
				this.healthComponent.OnCurrentHealthChanged -= UpdateHealthBar;
				this.healthComponent.OnMaxHealthChanged -= UpdateHealthBar;
			}
		}
	}
}