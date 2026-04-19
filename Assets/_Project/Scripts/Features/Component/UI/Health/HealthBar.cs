using Kope.Component.Health;
using Kope.Component.Health.Interface;
using UnityEngine;
using UnityEngine.UI;
using Kope.Core.Extensions;

public class HealthBar : MonoBehaviour {
	[SerializeField] private HealthComponentBase healthComponent;
	[SerializeField] private Slider healthBarFill;

	private void Start() {
		if (!ValidateReferences()) return;

		this.healthComponent.OnCurrentHealthChanged += RefreshUI;
		this.healthComponent.OnMaxHealthChanged += RefreshUI;

		UpdateVisuals(this.healthComponent.CurrentHealth, this.healthComponent.MaxHealth);
	}

	private void RefreshUI(HealthChangeInfo hpInfo) {
		UpdateVisuals(hpInfo.CurrentHealth, hpInfo.MaxHealth);
	}

	private void UpdateVisuals(float current, float max) {
		if (this.healthBarFill == null) return;
		this.healthBarFill.value = (max <= 0) ? 0 : current / max;
	}

	private bool ValidateReferences() {
		if (this.healthComponent != null && this.healthBarFill != null) return true;
		Debug.LogError($"HealthBar is missing references! {this.GetFullHierarchyPath()}", this);
		return false;
	}

	private void OnDestroy() {
		if (this.healthComponent != null) {
			this.healthComponent.OnCurrentHealthChanged -= RefreshUI;
			this.healthComponent.OnMaxHealthChanged -= RefreshUI;
		}
	}
}