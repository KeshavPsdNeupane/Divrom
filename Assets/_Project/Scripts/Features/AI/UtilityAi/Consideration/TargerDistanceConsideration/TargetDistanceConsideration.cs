using UnityEngine;
using Kope.AI.Utility;
using Kope.Core.EntityComponentRegistry;
using Kope.Component.Movement;
using Kope.Core.Collections.Hashes;
using Kope.AI.AIBlackBoard;
using Kope.AI;
using System.Collections.Generic;

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

	[SerializeField, Range(0.001f, 10f), Tooltip("The radius of the dead zone around the entity. " +
	"Targets within this radius will not be considered.")]
	private float deadZoneRadius = 1.0f;
	[SerializeField] private AnimationCurve rangeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

	[Header("ContextNew Evaluation")]
	[SerializeField] private EntityQuery query;

	private HashedTag _hashedEntityCommonName;
	private float _angleThreshold = 360f;
	private float _cosineOfAngleThreshold;
	private float _squareCosineOfAngleThreshold;
	private float _squareMaxRange;
	private float _squareDeadZoneRadius;
	private IReadOnlyComponentRegistry _closestTargetCache;
	public override string ConsiderationName => this.considerationName;

	public override IReadOnlyComponentRegistry GetSelectedTargetRegistry(ActionType actionType) {
		if (!IsRelevantFor(actionType)) {
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
		ValidateConfig(this._hashedEntityCommonName);
		this._closestTargetCache = null;
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

	public override (float, int) EvaluateNew(IReadOnlyContext context) {
		this._closestTargetCache = null;
		if (this.entityCommonNameConfig == null) return (0f, 0);

		var closest = FindClosestValidTargetNew(context, out float actualDistance);
		this._closestTargetCache = closest;

		if (closest == null) return (0f, 0);

		float denominator = Mathf.Max(Mathf.Epsilon, this.maxRange - this.deadZoneRadius);
		float normalizedDistance = Mathf.Clamp01((actualDistance - this.deadZoneRadius) / denominator);
		float score = Mathf.Max(this.rangeCurve.Evaluate(normalizedDistance), 0.0f);

		return (score, 0);
	}

	private IReadOnlyComponentRegistry FindClosestValidTargetNarrowFov(IReadOnlyList<IReadOnlyComponentRegistry> targetContexts, Vector3 selfPos, Vector3 forward, out float finalDistance) {
		finalDistance = 0f;
		IReadOnlyComponentRegistry closest = null;
		float closestSqrDist = this._squareMaxRange;
		foreach (var target in targetContexts) {
			Vector3 targetPos = target.EntityTransform.position;
			Vector3 direction = targetPos - selfPos;
			float sqrDist = direction.sqrMagnitude;
			if (sqrDist < this._squareDeadZoneRadius || sqrDist > closestSqrDist) continue;

			if (this._angleThreshold < 360f) {
				float dot = Vector3.Dot(forward, direction);
				/* 
				PERFORMANCE OPTIMIZATION: High-speed Field of View (FOV) Check.

				WHY A NARROW-FOV PATH EXISTS:
				For FOV angles <= 180°, all valid targets must lie in the forward
				hemisphere (dot >= 0). Since sign information is no longer needed,
				the comparison can be safely squared to eliminate the costly square root.

				DERIVATION:
				1. Standard Dot Product: cos(theta) = (A . B) / (|A| * |B|)
				2. Threshold Condition:  cos(theta) >= cos(angleThreshold)
				3. As forward is normalized (|A| = 1):
											(A . B) / |B| >= cosThreshold
				4. Rearrangement:
											dot >= cosThreshold * dist
				5. Since dot >= 0, square both sides:
											dot² >= cosThreshold² * dist²
				6. Replace dist² with sqrDist:
											dot² >= cosThreshold² * sqrDist

				Result: Exact FOV testing without requiring a square root.
*/
				if (dot < 0f) continue;
				if (dot * dot < this._squareCosineOfAngleThreshold * sqrDist) continue;
			}
			closestSqrDist = sqrDist;
			closest = target;
		}
		if (closest != null) finalDistance = Mathf.Sqrt(closestSqrDist);
		return closest;
	}

	private IReadOnlyComponentRegistry FindClosestValidTargetWideFov(IReadOnlyList<IReadOnlyComponentRegistry> targetContexts, Vector3 selfPos, Vector3 forward, out float finalDistance) {
		finalDistance = 0f;
		IReadOnlyComponentRegistry closest = null;
		float closestSqrDist = this._squareMaxRange;
		foreach (var target in targetContexts) {
			Vector3 targetPos = target.EntityTransform.position;
			Vector3 direction = targetPos - selfPos;
			float sqrDist = direction.sqrMagnitude;
			if (sqrDist < this._squareDeadZoneRadius || sqrDist > closestSqrDist) continue;

			float dot = Vector3.Dot(forward, direction);
			/* 
				FIELD OF VIEW (FOV) CHECK.

				WHY A WIDE-FOV PATH EXISTS:
				For FOV angles > 180°, valid targets may exist behind the observer
				(dot < 0). Squaring the comparison would lose sign information and
				incorrectly classify rear targets, so the exact form must be used.

				DERIVATION:
				1. Standard Dot Product: cos(theta) = (A . B) / (|A| * |B|)
				2. Threshold Condition:  cos(theta) >= cos(angleThreshold)
				3. As forward is normalized (|A| = 1):
											(A . B) / |B| >= cosThreshold
				4. Rearrangement:
											dot >= cosThreshold * dist

				This preserves sign information and remains correct for all
				wide-angle and panoramic FOV checks.
			*/
			if (dot < 0f) {
				float dist = Mathf.Sqrt(sqrDist);
				if (dot < this._cosineOfAngleThreshold * dist) continue;
			}
			closestSqrDist = sqrDist;
			closest = target;
		}
		if (closest != null) finalDistance = Mathf.Sqrt(closestSqrDist);
		return closest;
	}

	private IReadOnlyComponentRegistry FindClosestValidTargetNew(IReadOnlyContext context, out float finalDistance) {
		finalDistance = 0f;
		if (!context.TryGetTargets(this.query, out var targetContexts)) return null;
		if (!context.SelfReadOnlyEntityContext.TryGetReadOnly<MovementComponentBase>(out var movementComponent)) {
			Debug.LogError($"[{this.considerationName}] The entity does not have a MovementComponentBase.", this);
			return null;
		}

		this._angleThreshold = context.FieldOfViewData.FieldOfViewAngle;
		this._cosineOfAngleThreshold = context.FieldOfViewData.CosineOfAngleThreshold;
		this._squareCosineOfAngleThreshold = context.FieldOfViewData.SquareCosineOfAngleThreshold;

		Vector3 selfPos = movementComponent.Position;
		Vector3 forward = movementComponent.GetLookingAtDirection().normalized;

		return this._angleThreshold > 180f
			? FindClosestValidTargetWideFov(targetContexts, selfPos, forward, out finalDistance)
			: FindClosestValidTargetNarrowFov(targetContexts, selfPos, forward, out finalDistance);
	}
}