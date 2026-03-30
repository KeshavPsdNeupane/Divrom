using Kope.AI.Utility;
using Kope.Component.Health;
using Kope.Core.EntityComponentSystem;
using UnityEngine;


[CreateAssetMenu(fileName = "HealthConsideration", menuName = "Scriptable Objects/AI/Utility/Considerations/HealthConsideration")]
public class HealthConsideration : ConsiderationSO {
	[SerializeField] private string considerationName = "Range Consideration";
	[SerializeField] private EntityCommonNameConfig entityCommonNameConfig;
	[SerializeField, Tooltip("The common name of the entity to consider. " +
	"This should be defined in the EntityCommonNameConfig.")]
	private string entityCommonName = "Health";
	[SerializeField] private AnimationCurve healthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);


	private HashedTag _hashedEntityCommonName;
	protected override void OnInitialize() {
		this._hashedEntityCommonName = new HashedTag(this.entityCommonName);
		ValidateConfig(this._hashedEntityCommonName);
	}
	private void ValidateConfig(HashedTag commonNameTag) {
		if (this.entityCommonNameConfig == null) {
			Debug.LogError($"[{this.considerationName}] Missing EntityCommonNameConfig reference. Please assign it in the inspector.", this);
			return;
		}

		if (!this.entityCommonNameConfig.InternalContains(commonNameTag)) {
			Debug.LogError($"[{this.considerationName}] The specified common name '{this.entityCommonName}' was not found in the EntityCommonNameConfig. Please ensure it is defined correctly.", this);
		}
	}

	public override string ConsiderationName => this.considerationName;

	public override (float, int) Evaluate(IReadOnlyContext context) {
		if (this.entityCommonNameConfig == null) return (0.0f, 0);

		var selfContext = context.SelfReadOnlyEntityContext;
		if (!selfContext.TryGetReadOnlyComponent<IHealthComponent>(out var healthComponent)) {
			Debug.LogError($"[{this.considerationName}] The entity does not have a HealthComponent. Please ensure it is added to the entity.", this);
			return (0.0f, 0);
		}
		float healthPercentage = healthComponent.CurrentHealth / healthComponent.MaxHealth;
		float score = this.healthCurve.Evaluate(healthPercentage);
		//	Debug.Log($"[{this.considerationName}] Evaluated health percentage: {healthPercentage:F2}, score: {score:F2}");
		return (score, 0);
	}

}
