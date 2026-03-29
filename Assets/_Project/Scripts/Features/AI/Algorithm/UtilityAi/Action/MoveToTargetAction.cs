using System;
using Kope.AI.Utility;
using Kope.Component.Movement;
using ThirdParty;
using UnityEngine;

[CreateAssetMenu(fileName = "MoveTowardAction", menuName = "Scriptable Objects/AI/Utility/Actions/MoveTowardAction")]
public class MoveTowardAction : ActionSO {
	[SerializeField, Tooltip("If this Action is used againt player or other entities that require some distance to be maintained, " +
	"this radius defines the free space around the target that the AI will try to maintain. " +
	"Set to 0.001 to effectively disable this feature and have the AI move all the way to the target," +
	"For entities like portion and such"),
	Range(0.001f, 5f)]
	private float playerFreeSpaceRadius = 0.2f;

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
		this._squareFreeSpaceRadius = this.playerFreeSpaceRadius * this.playerFreeSpaceRadius;
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

		if (!selfComponentRegistry.TryGetReadOnlyComponent(out this._selfMovementComponent)) {
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
		//	Debug.Log($"[RangeAction] TickFixedUpdate called for action {this.name}");
		// just simple float timer, no need to have a full timer class for this,
		//  since we dont need any of the extra features of the CountdownTimer for this.
		this._directionChangeInterval -= Time.fixedDeltaTime;

		if (this._readOnlyTargetTransform == null || this._selfMovementComponent == null) {
			SetComplete();
			return;
		}

		// just a crude movement logic to move towards the target until we are within the desired range,
		// then we will move away from the target to maintain some distance, creating a back
		// and forth movement around the target.
		Vector3 targetPosition = this._readOnlyTargetTransform.position;
		Vector3 selfPosition = this._selfMovementComponent.Position;
		Vector3 directionToTarget = targetPosition - selfPosition;

		// cheap operations, since it can be vectorized by the CPU as (a*x + c) operation in 1 clock cycle,
		// and we want to do it every frame to
		// check if we are within the free space radius, even if we are not changing direction every frame.
		float sqrDistFromTargetToCurrentEntity = directionToTarget.sqrMagnitude;

		// if we are within the free space radius, we want to stop moving to avoid jittery movement 
		// and to maintain a safe distance from the target.
		if (sqrDistFromTargetToCurrentEntity <= this._squareFreeSpaceRadius) {
			this._selfMovementComponent.SetMovementIntent(new MovementIntent(Vector3.zero, MovementIntentType.Stop));
			SetComplete();
			return;
		}

		// we only want to change direction at a certain frequency to prevent jittery movement,
		// especially when the target is moving and we are trying to maintain a certain distance from it.
		// but above distance check must be done every frame to make sure we can stop at the right time,
		// even if we are not changing direction every frame.
		if (this._directionChangeInterval > 0f) return;
		this._directionChangeInterval = this._directionChangeTime;

		// expensive operation, so we only want to do it at a certain frequency, as mentioned above.
		Vector3 directionToMove = directionToTarget.normalized;
		this._selfMovementComponent.SetMovementIntent(new MovementIntent(directionToMove, MovementIntentType.Move));
		return;
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