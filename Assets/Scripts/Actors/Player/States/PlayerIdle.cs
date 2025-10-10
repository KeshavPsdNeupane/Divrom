using UnityEngine;

public class PlayerIdle : PlayerBaseState
{
    private MovementComponent movementComponent;
    private AnimationComponent animationComponent;

    public PlayerIdle(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController, string animationBoolName,
        string name = "PlayerIdle")
        : base(baseStateManager, playerStateController,animationBoolName, name)
    {
        this.movementComponent = this.playerStateController.movementComponent;
        this.animationComponent = this.playerStateController.animationComponent;
    }

    public override void Enter()
    {
        this.animationComponent.anim.SetBool(this.animationBoolHash, true);
    }

    public override void Update()
    {
        if (this.movementComponent.direction.sqrMagnitude >= PlayerAnimationThreshold.WALKING_THRESHOLD)
            this.stateManager.ChangeState(this.playerStateController.playerStates.playermove);
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
