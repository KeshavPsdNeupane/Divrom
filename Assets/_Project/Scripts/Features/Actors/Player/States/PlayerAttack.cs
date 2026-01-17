public class PlayerAttack : PlayerBaseState
{
    private readonly MovementComponentBase movementComponent;
    private readonly AnimationComponent animationComponent;


    private AnimationState currentAnimationState;
    private int currentAnimationHash;
    private bool animationExists;

    public override AnimationState AnimationState => this.currentAnimationState;
    public override int AnimationStateHash => this.currentAnimationHash;

    private const float ATTACK_ANIMATION_THRESHOLD = 0.9f;

    public PlayerAttack(PlayerStateManager baseStateManager, PlayerStateController playerStateController)
        : base(baseStateManager, playerStateController)
    {
        movementComponent = playerStateController.MovementComponent;
        animationComponent = playerStateController.AnimationComponent;

        UpdateWeaponData(playerStateController.PlayerAttackComponent.EquippedWeaponData);
    }

    public override void Enter()
    {
        // Always grab latest data when we start an attack
        var weapon = playerStateController.PlayerAttackComponent.EquippedWeaponData;
        UpdateWeaponData(weapon);

        if (this.animationExists)
        {
            this.animationComponent.anim.speed = weapon.AttackSpeed;
            this.animationComponent.anim.Play(this.currentAnimationHash, 0, 0f);
        }
        else
        {
            SwitchToIdle();
        }
    }

    public override void Update()
    {
        if (!this.animationExists) return;
        CheckAnimationFinished();
    }


    public override void PhysicUpdate() => this.movementComponent.ApplyMovement(0.5f);
    public override void OnAnimationTrigger() => SwitchToIdle();
    public override void Exit() { }

    private void UpdateWeaponData(WeaponData weaponData)
    {
        this.currentAnimationState = weaponData.PrimaryAttackAnimation;
        this.currentAnimationHash = weaponData.PrimaryAttackAnimationHash;
        this.animationExists = this.animationComponent.DoesAnimationExist(this.currentAnimationHash);
    }

    private void CheckAnimationFinished()
    {
        if (!this.animationComponent.IsAnimationFinished(this.currentAnimationHash, ATTACK_ANIMATION_THRESHOLD)) return;
        SwitchToIdle();
    }

    private void SwitchToIdle()
    {
        this.animationComponent.SetDefaultAnimationSpeed();
        this.stateManager.ChangeState(this.playerStateController.PlayerStates.PlayerIdle);
    }
}
