using Kope.Component.Health.Interface;
using UnityEngine;
namespace Kope.Component.Health {
	public class CombatTextSpawner : MonoBehaviour {
		[Header("References")]
		[SerializeField] private GameObject textPrefab;
		[SerializeField] private HealthComponentBase healthComponent;

		[Header("Settings")]
		[SerializeField] private int textSize = 5;
		[SerializeField] private Vector3 spawnOffset = new(0, 2f, 0);
		[SerializeField] private Color damageColor = Color.red;
		[SerializeField] private Color healColor = Color.green;

		private void OnEnable() {
			if (healthComponent != null) {
				healthComponent.OnCurrentHealthChanged += HandleHealthChange;
			}
		}

		private void OnDisable() {
			if (healthComponent != null) {
				healthComponent.OnCurrentHealthChanged -= HandleHealthChange;
			}
		}

		// private void Update() {
		// 	// This is just for testing purposes. In a real implementation, you would trigger 
		// 	// SpawnText based on actual health changes from the HealthComponentBase.
		// 	// For example, you could subscribe to health change events and call SpawnText 
		// 	// with the appropriate values when damage or healing occurs.
		// 	if (Mouse.current.leftButton.wasPressedThisFrame) {
		// 		SpawnText(-Random.Range(5, 15), this.damageColor);
		// 	}
		// 	if (Mouse.current.rightButton.wasPressedThisFrame) {
		// 		SpawnText(Random.Range(5, 15), this.healColor);
		// 	}
		// }

		private void HandleHealthChange(HealthChangeInfo info) {
			// 1. Combine checks for early exit
			if (!info.ShowFloatingText ||
			(info.ChangeType != HealthChangeType.Damage && info.ChangeType != HealthChangeType.Heal)) return;

			// 2. Determine if it's damage or heal and get the absolute value
			bool isDamage = info.ChangeType == HealthChangeType.Damage;
			float delta = isDamage
				? info.PreviousHealth - info.CurrentHealth
				: info.CurrentHealth - info.PreviousHealth;

			// 3. Skip if zero or negative (sanity check)
			if (delta <= 0) return;

			Color targetColor = isDamage ? damageColor : healColor;
			// 4. Single allocation for the formatted string
			string formattedText = $"{(isDamage ? "-" : "+")}{delta:0}";

			SpawnText(formattedText, targetColor);
		}

		private void SpawnText(string text, Color color) {
			if (textPrefab == null) return;

			GameObject go = Instantiate(textPrefab, transform.position + spawnOffset, Quaternion.identity);

			if (go.TryGetComponent<FloatingText>(out var floatingText)) {
				// Pass the pre-formatted data
				floatingText.Initialize(text, color, this.textSize);
			}
		}
	}
}