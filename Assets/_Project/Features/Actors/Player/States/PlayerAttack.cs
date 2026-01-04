using UnityEngine;

public class PlayerAttack : PlayerBaseState
{
    private readonly PlayerMovementComponent movementComponent;
    private readonly AnimationComponent animationComponent;
    private AnimationState animationState;
    private WeaponType equippedWeapon;
    private int animationStateHash;
    public override int AnimationStateHash => this.animationStateHash;
    public override AnimationState AnimationState => this.animationState;
    private bool doAnimationExists = false;

    public PlayerAttack(PlayerStateManager baseStateManager,
        PlayerStateController playerStateController)
        : base(baseStateManager, playerStateController)
    {

        this.movementComponent = this.playerStateController.MovementComponent;
        this.animationComponent = this.playerStateController.AnimationComponent;

        UpdateAnimationForWeapon(this.playerStateController.EquippedWeapon);
    }

    public override void Enter()
    {
        if (this.equippedWeapon != this.playerStateController.EquippedWeapon)
        {
            UpdateAnimationForWeapon(this.playerStateController.EquippedWeapon);
        }

        if (this.doAnimationExists)
        {
            this.animationComponent.anim.Play(this.animationStateHash, 0, 0f);
        }
        else
        {
            Logger.Error($"Animation {this.animationState} not found in Animator for PlayerAttack state.");
            this.stateManager.ChangeState(this.playerStateController.PlayerStates.PlayerIdle);
            return;
        }
    }

    public override void Update()
    {
        if (!this.doAnimationExists) return;
        CheckIfAnimationFinished();
    }


    public override void PhysicUpdate()
    => this.movementComponent.ApplyMovement(0.5f);



    public override void OnAnimationTrigger()
    => this.stateManager.ChangeState(this.playerStateController.PlayerStates.PlayerIdle);


    public override void Exit() { }

    private void CheckIfAnimationFinished()
    {
        // this threshold defines how close to the end of the animation we consider it finished
        // using this just to get out of unity fking animation flickering,
        // fk unity animation system and its blending bs 
        const float ATTACK_ANIMATION_THRESHOLD = 0.9f;
        if (!this.animationComponent.IsAnimationFinished(this.animationStateHash,
        ATTACK_ANIMATION_THRESHOLD)) return;

        this.stateManager.ChangeState(this.playerStateController.PlayerStates.PlayerIdle);
    }

    private void UpdateAnimationForWeapon(WeaponType weapon)
    {
        this.equippedWeapon = weapon;
        this.animationState = WeaponAnimationMapper.GetAnimationType(weapon);
        this.animationStateHash = Animator.StringToHash(this.animationState.ToString());
        this.doAnimationExists = this.animationComponent.CheckIfAnimationExists(this.animationStateHash);
    }
}
