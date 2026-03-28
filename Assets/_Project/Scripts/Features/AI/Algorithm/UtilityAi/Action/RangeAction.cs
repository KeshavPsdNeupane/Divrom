using System;
using Kope.AI.Utility;
using Kope.Component.Movement;
using Kope.Core.EntityComponentSystem;
using ThirdParty;
using UnityEngine;

[CreateAssetMenu(fileName = "RangeAction", menuName = "Scriptable Objects/AI/Utility/Actions/RangeAction")]
public class RangeAction : ActionSO {
	[SerializeField, Tooltip("AI Will not completely fill the free space around the player"),
	 Range(0.0f, 5f)]
	private float playerFreeSpaceRadius = 0.2f;

	[SerializeField, Tooltip("The maximum duration for which the action can. So AI wont be always be in this action" +
	" Set to 0 to disable this feature."), Range(0f, 10f)]
	private float maxActionDuration = 0.5f;


	[SerializeField, Tooltip("How many time AI will reEvaluate its movement direction per second")]
	private int directionChangeFrequency = 3;

	private IReadOnlyComponentRegistry _readOnlyTargetComponentRegistry;
	private IReadOnlyComponentRegistry _selfComponentRegistry;
	private MovementComponentBase _readOnlyTargetMovementComponent;
	private MovementComponentBase _selfMovementComponent;
	private float _directionChangeTime;
	private float _directionChangeInterval;


	private CountdownTimer _actionTimer;

	protected override void OnInitialize(Context ctx) {
		this._directionChangeTime = 1f / this.directionChangeFrequency;
		this._directionChangeInterval = this._directionChangeTime;
		this._readOnlyTargetComponentRegistry = GetSelectedTargetRegistry(this.actionType);

		// just defensive checking, in case the considerations that provide the target registry 
		// are not properly set up or fail to find a valid target, we want to avoid starting an action 
		// that will try to act on a null target and cause errors.
		// but it is garuntee that if the considerations fail to find a valid target, they will return a score of 0, 
		// thus making this action unselectable, so this is just an extra safety check.
		if (this._readOnlyTargetComponentRegistry == null) {
			SetComplete();
			return;
		}

		this._selfComponentRegistry = ctx.CurrentMutableEntityContext;

		if (!this._readOnlyTargetComponentRegistry.TryGetReadOnlyComponent(out this._readOnlyTargetMovementComponent)) {
			Debug.LogError($"RangeAction Error: Target does not have a MovementComponent on {this.name}");
			SetComplete();
			return;
		}

		if (!this._selfComponentRegistry.TryGetReadOnlyComponent(out this._selfMovementComponent)) {
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

		if (this._readOnlyTargetMovementComponent == null || this._selfMovementComponent == null) {
			SetComplete();
			return;
		}

		Debug.Log($"[RangeAction] name= {this._readOnlyTargetComponentRegistry.RegistryName}, position= {this._readOnlyTargetComponentRegistry.EntityTransform.position}");
		Debug.Log($"[RangeAction] self name= {this._selfComponentRegistry.RegistryName}, position= {this._selfComponentRegistry.EntityTransform.position}");

		// just a crude movement logic to move towards the target until we are within the desired range,
		// then we will move away from the target to maintain some distance, creating a back
		// and forth movement around the target.
		Vector3 targetPosition = this._readOnlyTargetMovementComponent.Position;
		Vector3 selfPosition = this._selfMovementComponent.Position;
		Vector3 directionToTarget = targetPosition - selfPosition;
		//	Debug.Log($"Direction to target: {directionToTarget}, SqrMagnitude: {directionToTarget.sqrMagnitude}, Desired SqrRadius: {this.playerFreeSpaceRadius * this.playerFreeSpaceRadius}");
		const float SMOOTHING_THRESHOLD = 0.01f; // to prevent jittery movement when we are very close to the desired distance, we can consider ourselves at the optimal range and not change direction.
												 // Use a small range (e.g., within 0.1m of the goal) to trigger the 'Stop'
		Debug.Log($"[RangeAction] Checking distance to target. SqrDistance: {directionToTarget.sqrMagnitude}, Desired SqrRadius: {this.playerFreeSpaceRadius * this.playerFreeSpaceRadius}");
		float sqrDist = directionToTarget.sqrMagnitude;
		float targetSqrRadius = this.playerFreeSpaceRadius * this.playerFreeSpaceRadius;

		if (sqrDist <= targetSqrRadius + SMOOTHING_THRESHOLD) {
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

		Vector3 directionToMove = directionToTarget.normalized;
		this._selfMovementComponent.SetMovementIntent(new MovementIntent(directionToMove, MovementIntentType.Move));
		return;
	}
	private void SetComplete() {
		this._selfMovementComponent.SetMovementIntent(new MovementIntent(Vector3.zero, MovementIntentType.Stop));
		MarkCompleted();
	}

	protected override void OnEndOrAbort() {
		if (this._actionTimer == null) return;
		this._actionTimer.OnTimerStop -= SetComplete;
		this._actionTimer.Reset();
		this._readOnlyTargetComponentRegistry = null;
		this._readOnlyTargetMovementComponent = null;
		this._selfMovementComponent = null;
	}
}