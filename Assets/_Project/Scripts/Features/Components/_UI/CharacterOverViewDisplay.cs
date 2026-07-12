using Kope.Component.ExperienceSystem;
using Kope.Component.Health;
using Kope.Component.Health.Interface;
using Kope.Core.Collections.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterOverViewDisplay : MonoBehaviour {
	[SerializeField] private HealthComponentBase healthComponent;
	[SerializeField] private ExperienceSystem experienceSystem;
	[SerializeField] private Slider healthBarFill;
	[SerializeField] private TextMeshProUGUI levelUpText;
	private void Start() {
		if (!ValidateReferences()) return;

		// Initialize values to current state
		UpdateHealthVisuals(this.healthComponent.CurrentHealth, this.healthComponent.MaxHealth);
		UpdateLevelVisuals(this.experienceSystem.CurrentLevel);

		// Bind events cleanly
		ToggleEventSubscriptions(true);
	}

	private void OnDestroy() {
		ToggleEventSubscriptions(false);
	}

	/// <summary>
	/// Centralized switch to easily register or unregister event listeners,
	/// preventing double-subscriptions and memory leaks.
	/// </summary>
	private void ToggleEventSubscriptions(bool subscribe) {
		if (this.healthComponent != null) {
			if (subscribe) {
				this.healthComponent.OnHealthChange(OnHealthChanged, true);
			} else {
				this.healthComponent.OnHealthChange(OnHealthChanged, false);
			}
		}

		if (this.experienceSystem != null) {
			this.experienceSystem.OnLevelChangeEvent(OnLevelChanged, subscribe);
		}
	}

	// ── Event Handlers ────────────────────────────────────────────────────────

	private void OnHealthChanged(HealthChangeInfo hpInfo) {
		UpdateHealthVisuals(hpInfo.CurrentHealth, hpInfo.MaxHealth);
	}

	private void OnLevelChanged(int newLevel) {
		UpdateLevelVisuals(newLevel);
	}

	// ── Visual Updates ────────────────────────────────────────────────────────

	private void UpdateHealthVisuals(float current, float max) {
		if (this.healthBarFill == null) return;
		this.healthBarFill.value = (max <= 0f) ? 0f : current / max;
	}

	private void UpdateLevelVisuals(int level) {
		if (this.levelUpText == null) return;
		this.levelUpText.text = level.ToString(); // ToString() is cleaner and faster than string interpolation here
	}

	// ── Validation ────────────────────────────────────────────────────────────

	private bool ValidateReferences() {
		bool isValid = true;
		string path = this.GetFullHierarchyPath();

		if (this.healthComponent == null) { Debug.LogError($"[HealthBar] Missing field '{nameof(healthComponent)}' at {path}", this); isValid = false; }
		if (this.experienceSystem == null) { Debug.LogError($"[HealthBar] Missing field '{nameof(experienceSystem)}' at {path}", this); isValid = false; }
		if (this.healthBarFill == null) { Debug.LogError($"[HealthBar] Missing field '{nameof(healthBarFill)}' at {path}", this); isValid = false; }
		if (this.levelUpText == null) { Debug.LogError($"[HealthBar] Missing field '{nameof(levelUpText)}' at {path}", this); isValid = false; }
		return isValid;
	}
}