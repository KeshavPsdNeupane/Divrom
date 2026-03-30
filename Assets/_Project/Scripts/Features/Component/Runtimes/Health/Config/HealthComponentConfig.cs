using Kope.Core.Attribute;
using UnityEngine;

namespace Kope.Component.Health {

	[CreateAssetMenu(fileName = "HealthComponentConfig", menuName = "Configs/HealthComponentConfig")]
	public class HealthComponentConfig : ScriptableObject {
		[SerializeField, Tooltip("Factor by which defence scales with level")] private float defenceScalingFactor = 0.5f;
		[SerializeField, Tooltip("Threshold at which resistance begins to diminish")] private float resistanceDiminishingReturnsThreshold = 0.8f;
		[SerializeField, Tooltip("Factor by which level affects health")] private float levelScalingFactor = 0.02f;

		[SerializeField, ReadOnly, Tooltip("The inverse of the resistance diminishing returns threshold")]
		private float inverseResistanceDiminishingReturnsThreshold;
		public float DefenceScalingFactor => this.defenceScalingFactor;
		public float ResistanceDiminishingReturnsThreshold => this.resistanceDiminishingReturnsThreshold;
		public float LevelScalingFactor => this.levelScalingFactor;
		public float ReciprocalOfResistanceDiminishingReturnsThreshold => this.inverseResistanceDiminishingReturnsThreshold;

		void OnValidate() => CalculateInverse();
		void OnEnable() => CalculateInverse();
		void CalculateInverse() {
			// this is not "inverse" in the strict mathematical sense, 
			// but it is the value we need to multiply with the resistance value to get the diminishing returns effect.
			// just micro-optimizing to avoid doing the division every time we calculate damage,
			// since this value is constant and only depends on the config.
			// to make single source of truth, we calculate it here based on the resistance diminishing returns threshold,
			this.inverseResistanceDiminishingReturnsThreshold = 1f / (1 - this.resistanceDiminishingReturnsThreshold);
		}
	}


}