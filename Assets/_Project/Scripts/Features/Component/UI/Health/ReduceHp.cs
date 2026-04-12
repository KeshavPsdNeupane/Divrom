using Kope.Component.Health;
using UnityEngine;


/// <summary>
/// A simple script to reduce health for testing purposes. 
/// This can be attached to a button or called from other scripts to simulate damage.
/// </summary>
public class ReduceHp : MonoBehaviour {
	[Header("Health Reduction Debugging Tools")]
	[SerializeField] private HealthComponentBase healthComponent;
	[SerializeField] private int hpReductionAmount = 10;
	[SerializeField, Range(0.05f, 1f),
	Tooltip("The minimum health ratio that the entity can be reduced to.")]
	private float minHpRatio;

	private void Start() {
		if (this.healthComponent == null) {
			Debug.LogError("HealthComponent reference is not set on ReduceHp script.");
		}
	}

	public void ReduceHealth() {
		if (this.healthComponent != null) {
			float targetHp = healthComponent.CurrentHealth - hpReductionAmount;
			float minHp = healthComponent.MaxHealth * minHpRatio;
			// we dont want the health to be reduced below the minimum threshold, so we clamp it to minHp
			if (targetHp < minHp) {
				targetHp = 0;
			}
			float actualReduction = healthComponent.CurrentHealth - targetHp;
			this.healthComponent.ApplyDamage(actualReduction);
		} else {
			Debug.LogError("HealthComponent reference is not set on ReduceHp script.");
		}
	}
}