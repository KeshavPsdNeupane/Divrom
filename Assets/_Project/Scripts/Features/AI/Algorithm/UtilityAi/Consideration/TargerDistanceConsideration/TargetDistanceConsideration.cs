using UnityEngine;
using Kope.AI.Utility;
using Kope.Core.EntityComponentRegistry;
using Kope.Component.Movement;
using Kope.Core.Types.Hashes;


[CreateAssetMenu(fileName = "TargetDistanceConsideration", menuName = "Scriptable Objects/AI/Utility/Considerations/TargetDistanceConsideration")]
public class TargetDistanceConsideration : ConsiderationSO {


	[SerializeField] private string considerationName = "Range Consideration";
	[SerializeField] private EntityCommonNameConfig entityCommonNameConfig;
	[SerializeField, Tooltip("The common name of the entity to consider. " +
	"This should be defined in the EntityCommonNameConfig.")]
	private string entityCommonName = "Player";
	// 0.0001 to avoid divide by zero
	[SerializeField, Range(0.0001f, 100f), Tooltip("The maximum range within which to consider targets.")]
	private float maxRange = 10f;
	[SerializeField, Range(0, 360), Tooltip("The angle threshold for considering targets.")]
	private float angleThreshold = 180f;

	[SerializeField, Range(0.001f, 10f), Tooltip("The radius of the dead zone around the entity. " +
	"Targets within this radius will not be considered.")]
	private float deadZoneRadius = 1.0f;
	[SerializeField] private AnimationCurve rangeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

	private HashedTag _hashedEntityCommonName;
	private float _cosineOfAngleThreshold;
	private float _squareCosineOfAngleThreshold;
	private float _squareMaxRange;
	private float _squareDeadZoneRadius;
	private IReadOnlyComponentRegistry _closestTargetCache;
	public override string ConsiderationName => this.considerationName;

	public override IReadOnlyComponentRegistry GetSelectedTargetRegistry(ActionType actionType) {
		if (!IsRelevantFor(actionType)) {
			// if this consideration is not relevant for the given action type, 
			// we return null to avoid providing a target registry that might be irrelevant or misleading for the action.
			return null;
		}
		return this._closestTargetCache;
	}


	protected override void OnInitialize() {

		if (this.deadZoneRadius >= this.maxRange) {
			Debug.LogWarning($"[{this.considerationName}] deadZoneRadius is >= maxRange. Adjusting to avoid logic errors.");
			this.maxRange = this.deadZoneRadius + 0.1f;
		}
		this._squareMaxRange = this.maxRange * this.maxRange;
		this._squareDeadZoneRadius = this.deadZoneRadius * this.deadZoneRadius;
		this._hashedEntityCommonName = new HashedTag(this.entityCommonName);
		// Pre-calculate the cosine of the threshold once
		// Dividing angle by 2 because threshold usually represents total FOV width
		this._cosineOfAngleThreshold = Mathf.Cos(this.angleThreshold * 0.5f * Mathf.Deg2Rad);
		this._squareCosineOfAngleThreshold = this._cosineOfAngleThreshold * this._cosineOfAngleThreshold;
		ValidateConfig(this._hashedEntityCommonName);
		this._closestTargetCache = null; // Clear cache on init/validate to ensure fresh evaluation
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

	/// <summary>
	/// Returns the forward direction of the given transform based on the configured ForwardAxis.
	/// Override in subclasses to provide custom forward direction logic.
	/// </summary>

	public override (float, int) Evaluate(IReadOnlyContext context) {
		this._closestTargetCache = null;
		if (this.entityCommonNameConfig == null) return (0f, 0); // no config, no targets, no score

		var closest = FindClosestValidTarget(context, out float actualDistance);
		this._closestTargetCache = closest;

		if (closest == null) {
			return (0f, 0); // no valid target found, so score is 0. Multiplication count is not incremented since this consideration doesn't contribute to the score.
		}
		// technically not possible for maxrange-deadzone to be 0 since we 
		// enforce a minimum value for both in the inspector, but we will still do 
		// this check to be safe and avoid any potential divide by zero errors if 
		// the values are set to something unexpected through code or future changes.
		float denominator = Mathf.Max(Mathf.Epsilon, this.maxRange - this.deadZoneRadius);
		float normalizedDistance = Mathf.Clamp01((actualDistance - this.deadZoneRadius) / denominator);
		float score = Mathf.Max(this.rangeCurve.Evaluate(normalizedDistance), 0.0f);

		// We are returning 0, here since we are not multiplying the score with 
		// any other consideration during the evaluation of this consideration, so the multiplication count is 0.
		// and the score is solely determined by the range curve based on the distance to the closest valid target.
		// The multiplication count would be relevant if we were combining this score with other considerations 
		// in a way that requires tracking how many scores have been multiplied together,
		// So that will be work of CompositeConsideration, so Look at composite considerations
		// for examples of how multiplication count is used in score combination.

		return (score, 0);
	}

	private IReadOnlyComponentRegistry FindClosestValidTarget(IReadOnlyContext context, out float finalDistance) {
		finalDistance = 0f;
		if (!context.TryGetReadOnlyTargetContexts(this._hashedEntityCommonName, out var targetContexts)) {
			return null;
		}

		if (!context.SelfReadOnlyEntityContext.TryGetReadOnlyComponent<MovementComponentBase>(out var movementComponent)) {
			Debug.LogError($"[{this.considerationName}] The entity does not have a MovementComponentBase. Please ensure it is added to the entity.", this);
			return null;
		}
		Vector3 selfPos = movementComponent.Position;
		Vector3 forward = movementComponent.GetLookingAtDirection().normalized;
		//	Debug.Log($"[RangeConsideration] Self Position: {selfPos}, Forward Direction: {forward}");

		IReadOnlyComponentRegistry closest = null;
		float closestSqrDist = this._squareMaxRange;

		foreach (var target in targetContexts) {
			Vector3 targetPos = target.EntityTransform.position;
			Vector3 direction = targetPos - selfPos;
			float sqrDist = direction.sqrMagnitude;

			// 1. Skipping targets that are outside the max range or within the dead zone,
			//  as they are not relevant for scoring.
			if (sqrDist < this._squareDeadZoneRadius || sqrDist > closestSqrDist) { continue; }


			// 2. If an angle threshold is set, check if the target is within the angle threshold 
			// relative to the entity's forward direction.
			if (this.angleThreshold < 360f) {
				float dot = Vector3.Dot(forward, direction);

				/* PERFORMANCE OPTIMIZATION: High-speed Field of View (FOV) Check.
			We avoid expensive trigonometric functions (Acos) and square roots (Magnitude) 
			by performing the angular comparison in squared-cosine space.

			DERIVATION:
			1. Standard Dot Product: cos(theta) = (A . B) / (|A| * |B|) for two vectors A and B.
			2. Threshold Condition:  cos(theta) > cos(angleThreshold)
			3. Substitution:         (A . B) / (|A| * |B|) > cos(angleThreshold)
			4. Rearrangement:        A . B > cos(angleThreshold) * |A| * |B| to avoid division. or division by zero issues.
			5. Simplification:       As A (forward vector) is normalized (|A|=1), then: 
									 dot > cosThreshold * |B|
			6. Final Squared Form:   dot^2 > cosThreshold^2 * |B|^2
									 dot^2 > squareCosineThreshold * sqrMagnitude

			By squaring both sides, we eliminate the need for the Magnitude calculation (Sqrt),
			allowing us to use the raw 'sqrMagnitude' already calculated for the distance check.
		*/

				// Safety: If the FOV is <= 180 degrees (cosThreshold >= 0), any negative dot 
				// product is automatically outside the threshold. This check also prevents 
				// squared negative dots from erroneously passing as positive matches behind the entity.
				if (this._cosineOfAngleThreshold >= 0 && dot < 0) continue;

				// Final comparison using pre-squared threshold and sqrMagnitude. 
				// This is mathematically equivalent to the angular check but requires zero Sqrt/Acos calls.
				if (dot * dot < this._squareCosineOfAngleThreshold * sqrDist) continue;
			}
			closestSqrDist = sqrDist;
			closest = target;
		}

		if (closest != null) {
			finalDistance = Mathf.Sqrt(closestSqrDist);
		}

		return closest;
	}


}