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
			this.healthComponent.ReduceHp(this.hpReductionAmount, this.minHpRatio);
		}

	}
}