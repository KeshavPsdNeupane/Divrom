using Kope.Component.Health.Interface;
using Kope.Core.ObjectPooling;
using Kope.Core.ServiceLocator;
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
				healthComponent.OnHealthChange(HandleHealthChange, true);
			}
		}

		private void OnDisable() {
			if (healthComponent != null) {
				healthComponent.OnHealthChange(HandleHealthChange, false);
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
			if (this.textPrefab == null || this._pooler == null) return;

			var go = this._pooler.Rent(this.textPrefab);
			if (go.TryGetComponent<FloatingText>(out var floatingText)) {
				go.SetActive(true);
				floatingText.SubScribeToRelease(ReleasePooledObject);
				floatingText.Initialize(text, color, this.textSize, transform.position + spawnOffset, Quaternion.identity);
			} else {
				Debug.LogError("CombatTextSpawner: Rented object does not have a FloatingText component.", this.textPrefab);
				ReleasePooledObject(go);
			}
		}
		private void ReleasePooledObject(GameObject obj) {
			if (this._pooler != null) {
				obj.SetActive(false);
				this._pooler.Release(this.textPrefab, obj);
			} else {
				Debug.LogError("CombatTextSpawner: Cannot release object because ObjectPooler reference is missing.");
				Destroy(obj);
			}

		}
	}
}