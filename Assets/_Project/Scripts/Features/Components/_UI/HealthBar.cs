using Kope.Component.Health;
using Kope.Component.Health.Interface;
using UnityEngine;
using UnityEngine.UI;
using Kope.Core.Types.Extensions;
using Kope.Component.ExperienceSystem;
using TMPro;

public class HealthBar : MonoBehaviour {
	[SerializeField] private HealthComponentBase healthComponent;
	[SerializeField] private ExperienceSystem experienceSystem;
	[SerializeField] private Slider healthBarFill;
	[SerializeField] private TextMeshProUGUI levelUpText;


	private void Start() {
		if (!ValidateReferences()) return;

		this.healthComponent.OnHealthChange += RefreshHpBar;
		this.healthComponent.OnHealthChange += RefreshHpBar;
		this.experienceSystem.OnLevelChangeEvent(LevelUpHandler, true);
		UpdateVisuals(this.healthComponent.CurrentHealth, this.healthComponent.MaxHealth);
	}

	private void RefreshHpBar(HealthChangeInfo hpInfo) {
		UpdateVisuals(hpInfo.CurrentHealth, hpInfo.MaxHealth);
	}

	private void UpdateVisuals(float current, float max) {
		if (this.healthBarFill == null) return;
		this.healthBarFill.value = (max <= 0) ? 0 : current / max;
	}



	private bool ValidateReferences() {
		if (this.healthComponent == null || this.healthBarFill == null) {
			Debug.LogError($"HealthBar is missing references! {this.GetFullHierarchyPath()}", this);
			return false;
		}
		if (this.experienceSystem == null || this.levelUpText == null) {
			Debug.LogError($"HealthBar is missing references! {this.GetFullHierarchyPath()}", this);
			return false;
		}
		return true;
	}


	private void OnDestroy() {
		if (this.healthComponent != null) {
			this.healthComponent.OnHealthChange -= RefreshHpBar;
			this.healthComponent.OnHealthChange -= RefreshHpBar;
		}
		if (this.experienceSystem != null) {
			this.experienceSystem.OnLevelChangeEvent(LevelUpHandler, false);
		}
	}
	private void LevelUpHandler(int newLevel) {
		this.levelUpText.text = $"{newLevel}";
	}
}