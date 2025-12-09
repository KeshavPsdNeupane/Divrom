using UnityEngine;

public class PlayerIdle : PlayerBaseState
{
    private readonly MovementComponent movementComponent;
    private readonly AnimationComponent animationComponent;

    public PlayerIdle(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController, string animationBoolName,
        string name = "PlayerIdle")
        : base(baseStateManager, playerStateController, animationBoolName, name)
    {
        this.movementComponent = this.playerStateController.MovementComponent;
        this.animationComponent = this.playerStateController.AnimationComponent;
    }

    public override void Enter()
    {
        this.animationComponent.anim.SetBool(this.animationBoolHash, true);
    }

    public override void Update()
    {
        if (this.movementComponent.direction.sqrMagnitude
        >= AnimationThreshold.WALKING_THRESHOLD)
            this.stateManager.ChangeState(
                this.playerStateController.PlayerStates.PlayerMove
                );
    }

    public override void PhysicUpdate()
    {
        this.movementComponent.ApplyMovement();
    }
    public override void Exit()
    {
        this.animationComponent.anim.SetBool(this.animationBoolHash, false);
    }
}
