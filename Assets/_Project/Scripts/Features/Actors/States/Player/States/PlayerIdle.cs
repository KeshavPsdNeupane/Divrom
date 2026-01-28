using UnityEngine;
using Kope.Component.Movement;
public class EntityIdle : EntityBaseState
{
    private readonly MovementComponentBase movementComponent;
    private readonly AnimationComponentBase animationComponent;
    private readonly AnimationState animationState;
    private readonly int animationStateHash;

    public override AnimationState AnimationState => this.animationState;
    public override int AnimationStateHash => this.animationStateHash;
    public override bool CanAcceptCommand => true;

    public EntityIdle(EntityStateManager baseStateManager,
        EntityStateController playerStateController, AnimationState animationState = AnimationState.Idle)
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
        >= MovementComponentBase.MOVEMENT_EPSILON)
            this.stateManager.ChangeState(
                this.playerStateController.EntityStates.EntityMove
                );
    }

    public override void PhysicUpdate()
    {
        // this.movementComponent.ApplyPhysics();
    }

    public override void Exit() { }
}
