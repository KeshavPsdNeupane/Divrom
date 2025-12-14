using UnityEngine;

public class PlayerAttack : PlayerBaseState
{
    private readonly MovementComponent movementComponent;
    private readonly AnimationComponent animationComponent;

    public PlayerAttack(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController, string animationBoolName,
        string name = "BasicAttack")
        : base(baseStateManager, playerStateController, animationBoolName, name)
    {
        this.movementComponent = this.playerStateController.MovementComponent;
        this.animationComponent = this.playerStateController.AnimationComponent;
    }

    public override void Enter()
    {
        //this.animationComponent.MoveAnimation(this.movementComponent.lastDirection);
        this.animationComponent.anim.Play(this.animationStateHash, 0, 0f);
    }

    public override void Update()
    => CheckIfAnimationFinished();


    public override void PhysicUpdate()
    => this.movementComponent.ApplyMovement(0.5f);



    public override void OnAnimationTrigger()
    => this.stateManager.ChangeState(this.playerStateController.PlayerStates.PlayerIdle);


    public override void Exit() { }

    private void CheckIfAnimationFinished()
    {
        AnimatorStateInfo stateInfo = this.animationComponent.anim.GetCurrentAnimatorStateInfo(0);

        float thisFloatIsUsedToAvoidUnityAnimatorWeirdFlikerOnAttackAnimationEnd = 0.9f;
        if (stateInfo.IsName(this.stateName) && stateInfo.normalizedTime
        >= thisFloatIsUsedToAvoidUnityAnimatorWeirdFlikerOnAttackAnimationEnd)
        {
            this.stateManager.ChangeState(this.playerStateController.PlayerStates.PlayerIdle);
        }
    }
}
