using UnityEngine;

public class PlayerAttack : PlayerBaseState
{
    private MovementComponent movementComponent;
    private AnimationComponent animationComponent;

    public PlayerAttack(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController, string animationBoolName,
        string name = "PlayerAttack")
        : base(baseStateManager, playerStateController, animationBoolName, name)
    {
        this.movementComponent = this.playerStateController.movementComponent;
        this.animationComponent = this.playerStateController.animationComponent;
    }

    public override void Enter()
    {
        this.animationComponent.MoveAnimation(this.movementComponent.lastDirection);
        this.animationComponent.anim.SetBool(this.animationBoolHash, true);
    }

    public override void PhysicUpdate()
    {
        this.movementComponent.ApplyMovement(0.5f);
    }
    public override void OnAnimationTrigger()
    {
        this.stateManager.ChangeState(this.playerStateController.playerStates.playerIdle);
    }
    public override void Exit()
    {
        this.animationComponent.anim.SetBool(this.animationBoolHash, false);
    }
}
