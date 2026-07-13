using System;
using Kope.AI.Utility;
using Kope.Component.Movement;
using ThirdParty;
using UnityEngine;

[CreateAssetMenu(fileName = "MoveTowardAction", menuName = "Scriptable Objects/AI/Utility/Actions/MoveTowardAction")]
public class MoveTowardAction : ActionSO {
	[SerializeField, Tooltip("This defines the deadzone around the target." +
	"If this entity is near this radius the action will be assumed to be completed, and the entity will stop moving." +
	"Finally make sure this is almost same as the deadzone defined in the TargetDistanceConsideration to avoid jittery movement" +
	" when the entity is trying to maintain a certain distance from the target."),
	Range(0.001f, 5f)]
	private float deadZone = 0.2f;

	[SerializeField, Tooltip("The maximum duration for which the action can. So AI wont be always be in this action" +
	" Set to 0 to disable this feature."), Range(0f, 10f)]
	private float maxActionDuration = 0.5f;

	[SerializeField, Min(1), Tooltip("How many time AI will reEvaluate its movement direction per second")]
	private int directionChangeFrequency = 3;

	/// <summary>
	/// Using the transform of the target directly for movement calculations,
	/// since it is a very common component that most entities will have,
	/// And all entity in game are required to have ECRegistry and it also stored the 
	/// main root GO transform, so it is a safe assumption that the target will have a transform component.
	/// see <cref>IReadOnlyComponentRegistry</cref> for more details.
	/// </summary>
	private Transform _readOnlyTargetTransform;

	private MovementComponentBase _selfMovementComponent;
	private float _directionChangeTime;
	private float _directionChangeInterval;

	/// <summary>
	/// Just a cheap micro-optimization so we dont have to square the playerFreeSpaceRadius
	/// every frame in the distance check, since it is a constant value that only changes
	/// when the action is created or edited in the inspector.
	/// </summary>
	private float _squareFreeSpaceRadius;

	private CountdownTimer _actionTimer;

	protected override void OnInitialize(Context ctx) {
		this._directionChangeTime = 1f / this.directionChangeFrequency;
		this._directionChangeInterval = this._directionChangeTime;
		this._squareFreeSpaceRadius = this.deadZone * this.deadZone;
		var readOnlyTargetComponentRegistry = GetSelectedTargetRegistry(this.actionType);

		// just defensive checking, in case the considerations that provide the target registry 
		// are not properly set up or fail to find a valid target, we want to avoid starting an action 
		// that will try to act on a null target and cause errors.
		// but it is garuntee that if the considerations fail to find a valid target, they will return a score of 0, 
		// thus making this action unselectable, so this is just an extra safety check.
		if (readOnlyTargetComponentRegistry == null) {
			SetComplete();
			return;
		}

		var selfComponentRegistry = ctx.CurrentMutableEntityContext;
		this._readOnlyTargetTransform = readOnlyTargetComponentRegistry.EntityTransform;

		if (!selfComponentRegistry.TryGetReadOnly(out this._selfMovementComponent)) {
			Debug.LogError($"RangeAction Error: Self does not have a MovementComponent on {this.name}");
			SetComplete();
			return;
		}


		if (this.maxActionDuration > 0f) {
			this._actionTimer = new CountdownTimer(this.maxActionDuration);
			this._actionTimer.OnTimerStop += SetComplete;
			this._actionTimer.Start();
		}
	}
	public override void TickUpdate() {
		this._actionTimer?.Tick(Time.deltaTime);
	}

	public override void TickFixedUpdate() {
		// Decouples direction logic from physics updates using a lightweight float timer.
		this._directionChangeInterval -= Time.fixedDeltaTime;

		if (this._readOnlyTargetTransform == null || this._selfMovementComponent == null) {
			SetComplete();
			return;
		}

		// Positions are sampled every FixedUpdate to ensure distance checks remain accurate 
		// to the physics simulation, preventing deadzone overshooting.
		Vector3 targetPosition = this._readOnlyTargetTransform.position;
		Vector3 selfPosition = this._selfMovementComponent.Position;
		Vector3 directionToTarget = targetPosition - selfPosition;

		// Use sqrMagnitude to avoid the computationally expensive Square Root (Mathf.Sqrt) operation.
		// This allows for high-frequency proximity checks with minimal CPU overhead.
		// and we dont have to worry about the direction vector either.
		// since we are only comparing absolute distance to deadzone and 
		// radius mean it is being comparing every direction, so the direction vector being not normalized 
		// does not affect the logic.
		float sqrDistFromTargetToCurrentEntity = directionToTarget.sqrMagnitude;

		// Immediate exit: If within the deadzone, we stop and complete the action.
		// This provides the 'handshake' with the Consideration's deadzone to maintain stability.
		if (sqrDistFromTargetToCurrentEntity <= this._squareFreeSpaceRadius) {
			this._selfMovementComponent.SetMovementIntent(new MovementIntent(Vector3.zero, MovementIntentType.Stop));
			SetComplete();
			return;
		}

		// Throttling the movement intent logic reduces "micro-jitter" caused by 
		// high-frequency target repositioning and saves performance on vector normalization.
		if (this._directionChangeInterval > 0f) return;
		this._directionChangeInterval = this._directionChangeTime;

		// Vector normalization and intent dispatch are only performed at the specified frequency.
		Vector3 directionToMove = directionToTarget.normalized;
		this._selfMovementComponent.SetMovementIntent(new MovementIntent(directionToMove, MovementIntentType.Move));
	}

	private void SetComplete() {
		this._selfMovementComponent.SetMovementIntent(new MovementIntent(Vector3.zero, MovementIntentType.Stop));
		MarkCompleted();
	}

	protected override void OnEndOrAbort() {
		this._readOnlyTargetTransform = null;
		this._selfMovementComponent = null;
		if (this._actionTimer != null) {
			this._actionTimer.OnTimerStop -= SetComplete;
			this._actionTimer.Reset();
		}
	}
}