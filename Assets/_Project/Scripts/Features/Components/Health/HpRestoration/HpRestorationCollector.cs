using Kope.Component.Health;
using Kope.Component.Health.Temp;
using Kope.Core.Sensor;
using UnityEngine;
using Kope.Core.Identity;
using Kope.Core.Collections.Extensions;

namespace Kope.Component {
	public class HpRestorationCollector : SensorBase {
		[SerializeField] private HealthComponentBase healthComponent;

		public override void OnStart() {
			base.OnStart();
			if (this.healthComponent == null) {
				Debug.LogWarning($"[HpRestorationCollector] No IHealthComponent assigned on {gameObject.name}" +
				$".{this.GetFullHierarchyPath()}", this);
			}
		}

		public override void OnDetect(Collider2D other) {
			if (this.healthComponent == null) return;
			if (!other.TryGetComponent<EntityInstanceNew>(out var entityInstance)) {
				Debug.LogWarning($"[HpRestorationCollector] Detected collider {other.name} does not have an EntityManager component. Cannot restore HP." + this._parentGOHiearchPathMessage, other.gameObject);
				return;
			}

			if (!entityInstance.EntityDetail.ComponentRegistry.TryGetReadOnly(out HpRestoration healthComp)) {
				Debug.LogWarning($"[HpRestorationCollector] Detected collider {other.name} does not have an HpRestoration component. Cannot restore HP." + this._parentGOHiearchPathMessage, other.gameObject);
				return;
			}
			float currentHp = this.healthComponent.CurrentHealth;
			float maxHp = this.healthComponent.MaxHealth;

			if (currentHp < maxHp) {
				float amountToRestore = healthComp.RestoreAmount;
				// the AmountToRestore is either a flat value or a percentage of max HP, depending on the IsPercentage flag in the HpRestoration component.
				// if we normalize the percentage value to be between 0 and 1, then we can just multiply it by max HP to get the actual amount to restore.
				if (healthComp.IsPercentage) {
					amountToRestore *= maxHp * 0.01f;
				}
				this.healthComponent.Heal(amountToRestore);
				entityInstance.InvokeOnEntityDiedOrPooledEvent();
				Destroy(other.gameObject);
				return;
			}
			Debug.Log($"[HpRestorationCollector] Detected collider {other.name} has full health. No need to restore HP." + this._parentGOHiearchPathMessage, other.gameObject);
		}
	}
}
