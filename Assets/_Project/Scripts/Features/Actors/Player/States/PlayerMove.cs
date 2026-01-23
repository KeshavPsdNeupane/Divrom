using UnityEngine;

<<<<<<< HEAD
public class EntityMove : EntityBaseState
{
    private readonly MovementComponentBase movementComponent;
    private readonly AnimationComponentBase animationComponent;
=======
public class PlayerMove : PlayerBaseState
{
    private readonly MovementComponentBase movementComponent;
    private readonly AnimationComponent animationComponent;
>>>>>>> master
    private readonly AnimationState animationState;
    private readonly int animationStateHash;

    public override AnimationState AnimationState => this.animationState;
    public override int AnimationStateHash => this.animationStateHash;
<<<<<<< HEAD
    public EntityMove(EntityStateManager baseStateManager,
        EntityContextManager playerStateController, AnimationState animationState = AnimationState.Walk)
=======
    public PlayerMove(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController, AnimationState animationState = AnimationState.Walk)
>>>>>>> master
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
        if (this.movementComponent.Direction.sqrMagnitude < MovementComponentBase.DIRECTION_THRESHOLD)
        {
            this.stateManager.ChangeState(
<<<<<<< HEAD
                this.playerStateController.EntityStates.EntityIdle);
=======
                this.playerStateController.PlayerStates.PlayerIdle);
>>>>>>> master
            return;
        }
        this.animationComponent.MoveAnimation(this.movementComponent.Direction);
    }

    public override void PhysicUpdate()
    {
        this.movementComponent.ApplyMovement();
    }

    public override void Exit() { }
}
