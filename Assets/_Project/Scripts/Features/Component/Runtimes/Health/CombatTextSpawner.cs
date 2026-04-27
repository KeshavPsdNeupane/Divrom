using Kope.Component.Health.Interface;
using Kope.Core.ObjectPooling;
using ServiceLocatorPattern;
using UnityEngine;

namespace Kope.Component.Health {
	public class CombatTextSpawner : MonoBehaviour {
		/*
    Why is the CombatTextSpawner's role limited to just "Renting and Initializing"?
    
    1. Separation of Concerns: The Spawner should only care about *when* to show 
       text (logic) and *what* the text should say (data). It should not care 
       *how* the text moves, fades, or cleans itself up. This allows you to 
       swap the FloatingText prefab for a completely different visual style 
       without ever touching this spawning logic.

    2. Service-Based Architecture: By using the ObjectPooler service, the Spawner 
       doesn't need to manage a local list of active text instances. It treats 
       the pool as a black box—it requests a "tool" (the text object), uses it 
       briefly to set parameters, and then immediately forgets about it.

    3. Frequency Handling: Combat text often triggers in massive bursts (AOE 
       spells, multi-hits). By offloading the lifecycle to the FloatingText 
       instances themselves, the Spawner remains performant because it doesn't 
       have to iterate through a collection of active labels every frame to 
       check for expiration.
*/
		[Header("References")]
		[SerializeField] private GameObject textPrefab;
		[SerializeField] private HealthComponentBase healthComponent;

		[Header("Settings")]
		[SerializeField] private int textSize = 5;
		[SerializeField] private Vector3 spawnOffset = new(0, 2f, 0);
		[SerializeField] private Color damageColor = Color.red;
		[SerializeField] private Color healColor = Color.green;

		private ObjectPooler _pooler;

		private void Awake() {
			if (!GlobalServiceLocator.Instance.TryGetService(out this._pooler)) {
				Debug.LogError("CombatTextSpawner: Failed to get ObjectPooler from Service Locator.");
			}

			if (healthComponent == null) {
				healthComponent = GetComponentInParent<HealthComponentBase>();
				if (healthComponent == null) Debug.LogError("CombatTextSpawner: HealthComponent reference is not set.");
			}

			if (textPrefab == null) {
				Debug.LogError("CombatTextSpawner: Text prefab reference is not set.");
			}
		}

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

		private void HandleHealthChange(HealthChangeInfo info) {
			if (!info.ShowFloatingText ||
				(info.ChangeType != HealthChangeType.Damage && info.ChangeType != HealthChangeType.Heal)) return;

			bool isDamage = info.ChangeType == HealthChangeType.Damage;
			float delta = isDamage
				? info.PreviousHealth - info.CurrentHealth
				: info.CurrentHealth - info.PreviousHealth;

			if (delta <= 0) return;

			Color targetColor = isDamage ? damageColor : healColor;
			string formattedText = $"{(isDamage ? "-" : "+")}{delta:0}";

			SpawnText(formattedText, targetColor);
		}

		private void SpawnText(string text, Color color) {
			if (textPrefab == null || _pooler == null) return;

			// Use TryRent with the prefab as the key
			if (_pooler.TryRent(textPrefab, out GameObject go)) {
				if (go.TryGetComponent<FloatingText>(out var floatingText)) {
					// Position and scale logic is handled inside Initialize
					floatingText.Initialize(text, color, this.textSize, transform.position + spawnOffset, Quaternion.identity);
				}
			} else {
				// Fallback: If for some reason the pool wasn't preloaded, instantiate manually.
				// Note: This instance won't have an OriginPrefab, so it will be Destroyed instead of Pooled on Release.
				GameObject goFallback = Instantiate(textPrefab, transform.position + spawnOffset, Quaternion.identity);
				if (goFallback.TryGetComponent<FloatingText>(out var floatingText)) {
					floatingText.Initialize(text, color, this.textSize, transform.position + spawnOffset, Quaternion.identity);
				}
			}
		}
	}
}