public class PlayerIdle : PlayerBaseState
{
    private readonly PlayerMovementComponent movementComponent;
    private readonly AnimationComponent animationComponent;

    public PlayerIdle(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController, string animationBoolName,
        string name = "Idle")
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
        if (this.movementComponent.Direction.sqrMagnitude
        >= AnimationThreshold.WALKING_THRESHOLD)
            this.stateManager.ChangeState(
                this.playerStateController.PlayerStates.PlayerMove
                );
    }

    public override void PhysicUpdate()
    {
        this.movementComponent.ApplyMovement();
    }

    public override void Exit() { }
}
