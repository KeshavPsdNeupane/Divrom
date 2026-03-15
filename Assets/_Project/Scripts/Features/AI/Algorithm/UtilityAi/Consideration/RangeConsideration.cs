using UnityEngine;
using Kope.AI.Utility;
using Kope.Core.EntityComponentSystem;

[CreateAssetMenu(fileName = "RangeConsideration", menuName = "Scriptable Objects/AI/Utility/Considerations/RangeConsideration")]
public class RangeConsideration : ConsiderationSO {
	[SerializeField] private string considerationName = "Range Consideration";
	[SerializeField] private EntityCommonNameConfig entityCommonNameConfig;
	[SerializeField, Tooltip("The common name of the entity to consider." +
	"This should be defined in the EntityCommonNameConfig.")]
	private string entityCommonName = "Player";
	// 0.0001 to avoid divide by zero
	[SerializeField, Range(0.0001f, 100f), Tooltip("The maximum range within which to consider targets.")]
	private float maxRange = 10f;
	[SerializeField, Range(0, 360), Tooltip("The angle threshold for considering targets.")]
	private float angleThreshold = 180f;
	[SerializeField] private AnimationCurve rangeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

	private HashedTag hashedEntityCommonName;
	private float cosineOfAngleThreshold;
	public override string ConsiderationName => this.considerationName;

	private void OnEnable() => Init();
	private void OnValidate() => Init();

	private void Init() {
		this.hashedEntityCommonName = new HashedTag(this.entityCommonName);
		// Pre-calculate the cosine of the threshold once
		// Dividing angle by 2 because threshold usually represents total FOV width
		this.cosineOfAngleThreshold = Mathf.Cos(this.angleThreshold * 0.5f * Mathf.Deg2Rad);
		ValidateConfig(this.hashedEntityCommonName);
	}
	private void ValidateConfig(HashedTag commonNameTag) {
		if (this.entityCommonNameConfig == null) {
			Debug.LogError($"[{nameof(RangeConsideration)}] Missing EntityCommonNameConfig reference. Please assign it in the inspector.", this);
			return;
		}

		if (!this.entityCommonNameConfig.InternalContains(commonNameTag)) {
			Debug.LogError($"[{nameof(RangeConsideration)}] The specified common name '{this.entityCommonName}' was not found in the EntityCommonNameConfig. Please ensure it is defined correctly.", this);
		}
	}

	public override (float, int) Evaluate(IReadOnlyContext context) {
		if (this.entityCommonNameConfig == null) return (0f, 0); // no config, no targets, no score

		var closest = FindClosestValidTarget(context, out float actualDistance);

		if (closest == null) {
			return (0f, 0); // no valid target found, so score is 0. Multiplication count is not incremented since this consideration doesn't contribute to the score.
		}

		float normalizedDistance = Mathf.Clamp01(actualDistance / this.maxRange);
		float score = Mathf.Max(this.rangeCurve.Evaluate(normalizedDistance), 0.0f);
		// no multiplication is done here so we just return the incoming totalMultiplicationCount 
		// without incrementing it, since this consideration doesn't apply any multiplication to the score.
		// it just passes through the score from the curve evaluation,
		//  which is already normalized to be between 0 and 1, to allow for more flexible scoring based on distance.

		return (score, 0); // returning 0 for multiplication count because this consideration 
						   // is not a multiplying factor, it's just a score modifier based on distance.
	}

	private IReadOnlyEntityRegistry FindClosestValidTarget(IReadOnlyContext context, out float finalDistance) {
		finalDistance = 0f;
		if (!context.TryGetReadOnlyTargetContexts(this.hashedEntityCommonName, out var targetContexts)) {
			return null;
		}

		Transform selfTransform = context.ReadOnlyEntityContext.EntityTransform;
		Vector3 selfPos = selfTransform.position;
		Vector3 forward = selfTransform.forward;

		IReadOnlyEntityRegistry closest = null;
		float closestSqrDist = this.maxRange * this.maxRange;

		foreach (var target in targetContexts) {
			Vector3 targetPos = target.EntityTransform.position;
			Vector3 direction = targetPos - selfPos;
			float sqrDist = direction.sqrMagnitude;

			// 1. Distance check: Always do this first. 
			// If it's further than our best candidate, skip immediately.
			if (sqrDist >= closestSqrDist) continue;

			// 2. Optimized Angle Check (Works for 2D & 3D)
			if (this.angleThreshold < 360f) {
				float dot = Vector3.Dot(forward, direction);

				/* The Logic: dot / magnitude > cosineThreshold
				   Rearranged to avoid division: dot > cosineThreshold * magnitude
				*/

				// Optimization: If the dot is negative, the target is > 90 degrees away.
				// If our threshold is narrow (< 180 total FOV), we can skip negative dots instantly.
				if (this.cosineOfAngleThreshold >= 0 && dot < 0) continue;

				// Only perform the Sqrt if the target passed the distance check 
				// and the simple dot-plane check.
				if (dot < this.cosineOfAngleThreshold * Mathf.Sqrt(sqrDist)) continue;
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






