using Kope.Component.Health;
using Kope.Component.Health.Temp;
using Kope.Core.Sensor;
using UnityEngine;

namespace Kope.Component {
	public class HpRestorationCollector : SensorBase {
		[SerializeField] private HealthComponentBase healthComponent;

		public override void OnStart() {
			base.OnStart();
			if (this.healthComponent == null) {
				Debug.LogWarning($"[HpRestorationCollector] No IHealthComponent assigned on {gameObject.name}.");
			}
		}

		public override void OnDetect(Collider2D other) {
			if (this.healthComponent == null) return;
			if (!other.TryGetComponent<EntityManager>(out var mgr)) {
				Debug.LogWarning($"[HpRestorationCollector] Detected collider {other.name} does not have an EntityManager component. Cannot restore HP." + this._parentGOHiearchPathMessage, other.gameObject);
				return;
			}

			if (!mgr.EntityDetail.ComponentRegistry.TryGetReadOnlyComponent(out HpRestoration healthComp)) {
				Debug.LogWarning($"[HpRestorationCollector] Detected collider {other.name} does not have an HpRestoration component. Cannot restore HP." + this._parentGOHiearchPathMessage, other.gameObject);
				return;
			}
			float currentHp = this.healthComponent.CurrentHealth;
			float maxHp = this.healthComponent.MaxHealth;
			Debug.Log($"Current hp: {currentHp}, Max hp: {maxHp}" + this._parentGOHiearchPathMessage, other.gameObject);

			if (this.healthComponent.CurrentHealth < this.healthComponent.MaxHealth) {
				float amountToRestore = healthComp.RestoreAmount;
				if (healthComp.IsPercentage) {
					amountToRestore *= this.healthComponent.MaxHealth;
				}
				this.healthComponent.Heal(amountToRestore);
				mgr.NotifyEntityDiedOrPooled();
				Destroy(other.gameObject);
				return;
			}
			Debug.Log($"[HpRestorationCollector] Detected collider {other.name} has full health. No need to restore HP." + this._parentGOHiearchPathMessage, other.gameObject);
		}
	}
}
