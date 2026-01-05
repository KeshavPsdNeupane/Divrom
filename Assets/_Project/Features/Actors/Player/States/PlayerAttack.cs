
public class PlayerAttack : PlayerBaseState
{
    private readonly PlayerMovementComponent movementComponent;
    private readonly AnimationComponent animationComponent;

    private AnimationState animationState;
    private int animationStateHash;

    public override AnimationState AnimationState => this.animationState;
    public override int AnimationStateHash => this.animationStateHash;

    private bool doAnimationExists = false;

    public PlayerAttack(PlayerStateManager baseStateManager, PlayerStateController playerStateController)
        : base(baseStateManager, playerStateController)
    {
        this.movementComponent = this.playerStateController.MovementComponent;
        this.animationComponent = this.playerStateController.AnimationComponent;
        SetWeaponData(this.playerStateController.PlayerAttackComponent.EquippedWeaponData);
    }

    public override void Enter()
    {
        var weapon = this.playerStateController.PlayerAttackComponent.EquippedWeaponData;

        this.animationComponent.anim.speed = weapon.AttackSpeed;

        if (weapon.HasChanged())
        {
            UpdateAnimationData(weapon);
        }
        if (this.doAnimationExists)
        {
            this.animationComponent.anim.Play(this.animationStateHash, 0, 0f);
        }
        else
        {
            ChangeToDefaultState();
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
    => ChangeToDefaultState();


    public override void Exit() { }

    private void CheckIfAnimationFinished()
    {
        // this threshold defines how close to the end of the animation we consider it finished
        // using this just to get out of unity fking animation flickering,
        // fk unity animation system and its blending bs 
        const float ATTACK_ANIMATION_THRESHOLD = 0.9f;
        if (!this.animationComponent.IsAnimationFinished(this.AnimationStateHash,
        ATTACK_ANIMATION_THRESHOLD)) return;

        ChangeToDefaultState();
    }

    private void SetWeaponData(WeaponData weaponData)
    {
        UpdateAnimationData(weaponData);
    }
    private void UpdateAnimationData(WeaponData weaponData)
    {
        this.animationState = weaponData.PrimaryAttackAnimation;
        this.animationStateHash = weaponData.PrimaryAttackAnimationHash;
        this.doAnimationExists = this.animationComponent.DoesAnimationExist(this.AnimationStateHash);
    }

    private void ChangeToDefaultState()
    {
        this.animationComponent.SetDefaultAnimationSpeed();
        this.stateManager.ChangeState(this.playerStateController.PlayerStates.PlayerIdle);
    }
}
