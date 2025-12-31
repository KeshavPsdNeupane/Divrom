public class PlayerMove : PlayerBaseState
{
    private readonly PlayerMovementComponent movementComponent;
    private readonly AnimationComponent animationComponent;

    public PlayerMove(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController, string animationBoolName,
        string name = "Move")
        : base(baseStateManager, playerStateController, animationBoolName, name)
    {
        this.movementComponent = this.playerStateController.MovementComponent;
        this.animationComponent = this.playerStateController.AnimationComponent;
    }

    public override void Enter()
    {
        this.animationComponent.anim.Play(this.animationStateHash, 0, 0f);
    }

    public override void Update()
    {
        if (this.movementComponent.Direction.sqrMagnitude < AnimationThreshold.WALKING_THRESHOLD)
        {
            this.stateManager.ChangeState(
                this.playerStateController.PlayerStates.PlayerIdle);
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
