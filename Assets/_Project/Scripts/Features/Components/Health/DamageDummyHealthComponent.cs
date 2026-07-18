using Kope.Component.Health.Interface;
using Kope.Core.LifeTimeManagement;
using Kope.SaveSystem.Attributes;
using UnityEngine;

namespace Kope.Component.Health {
	[InheritSaveId(1)]
	public class DamageDummyHealthComponent : HealthComponentBase, IUpdatable {
		[SerializeField, Range(0f, 1f), Tooltip("The ratio at which healing starts," +
		"Put 0 to disable healing")]
		private float healingStartRatio = 0.5f;
		private readonly int healDelayFrameCount = 2;
		private int remainingHealDelayFrames = -1;

		private void ResetTheHealth(HealthChangeInfo info) {
			if (this.healingStartRatio <= 0f) return;
			if (CurrentHealth / MaxHealth < healingStartRatio) {
				this.remainingHealDelayFrames = healDelayFrameCount;
			} else {
				this.remainingHealDelayFrames = -1;
			}
		}

		public void OnUpdate() {
			if (this.remainingHealDelayFrames > 0) {
				this.remainingHealDelayFrames--;
				if (this.remainingHealDelayFrames == 0) {
					float previousHealth = this.currentHealth;
					this.currentHealth = this.maxHealth;
					InvokeHpChange(new HealthChangeInfo(previousHealth,
					 this.currentHealth, this.maxHealth, HealthChangeType.Heal, false));
				}
			}
		}

		protected override void OnEnable() {
			base.OnEnable();
			if (this.healingStartRatio > 0f) {
				OnHealthChange(ResetTheHealth, true);
			}
		}
		protected override void OnDisable() {
			base.OnDisable();
			OnHealthChange(ResetTheHealth, false);
		}
	}
}
