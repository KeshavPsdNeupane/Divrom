using UnityEngine;

public class EntityIdle : EntityBaseState
{
    private readonly MovementComponentBase movementComponent;
    private readonly AnimationComponentBase animationComponent;
    private readonly AnimationState animationState;
    private readonly int animationStateHash;

    public override AnimationState AnimationState => this.animationState;
    public override int AnimationStateHash => this.animationStateHash;

    public EntityIdle(EntityStateManager baseStateManager,
        EntityContextManager playerStateController, AnimationState animationState = AnimationState.Idle)
        : base(baseStateManager, playerStateController)
    {
        this.animationState = animationState;
        this.animationStateHash = Animator.StringToHash(animationState.ToString());
        this.movementComponent = this.playerStateController.MovementComponent;
        this.animationComponent = this.playerStateController.AnimationComponent;
    }

    public override void Enter()
    {
        this.animationComponent.anim.Play(this.animationStateHash, 0, 0f);
    }

    public override void Update()
    {
        if (this.movementComponent.Direction.sqrMagnitude
        >= MovementComponentBase.DIRECTION_THRESHOLD)
            this.stateManager.ChangeState(
                this.playerStateController.EntityStates.EntityMove
                );
    }

    public override void PhysicUpdate()
    {
        this.movementComponent.ApplyMovement();
    }

    public override void Exit() { }
}
