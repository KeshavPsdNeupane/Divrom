using Kope.Component.Movement;
using Kope.Component.Animation;
public class EntityAttack : EntityBaseState {
	private readonly MovementComponentBase movementComponent;
	private readonly AnimationComponentBase animationComponent;


	private AnimationState currentAnimationState;
	private int currentAnimationHash;
	private bool animationExists;

	public override AnimationState AnimationState => this.currentAnimationState;
	public override int AnimationStateHash => this.currentAnimationHash;

	public override bool CanAcceptCommand => true;

	private const float ATTACK_ANIMATION_THRESHOLD = 0.9f;

	public EntityAttack(EntityStateManager baseStateManager, EntityStateController playerStateController)
		: base(baseStateManager, playerStateController) {
		movementComponent = playerStateController.MovementComponent;
		animationComponent = playerStateController.AnimationComponent;

		UpdateWeaponData(playerStateController.PlayerAttackComponent.EquippedWeaponData);
	}

	public override void Enter() {
		// Always grab latest data when we start an attack
		var weapon = playerStateController.PlayerAttackComponent.EquippedWeaponData;
		UpdateWeaponData(weapon);

		if (this.animationExists) {
			this.animationComponent.anim.speed = weapon.AttackSpeed;
			this.animationComponent.anim.Play(this.currentAnimationHash, 0, 0f);
		} else {
			SwitchToIdle();
		}
	}

	public override void TickUpdate() {
		if (!this.animationExists) return;
		CheckAnimationFinished();
	}


	public override void TickPhysicUpdate() {
		// Apply reduced movement speed during attack, can be tweaked or made weapon-specific later if desired.
		movementComponent.ApplyPhysics(0.5f);
	}
	public override void OnAnimationTrigger() => SwitchToIdle();
	public override void Exit() { }

	private void UpdateWeaponData(WeaponData weaponData) {
		this.currentAnimationState = weaponData.PrimaryAttackAnimation;
		this.currentAnimationHash = weaponData.PrimaryAttackAnimationHash;
		this.animationExists = this.animationComponent.DoesAnimationExist(this.currentAnimationHash);
	}

	private void CheckAnimationFinished() {
		if (!this.animationComponent.IsAnimationFinished(this.currentAnimationHash, ATTACK_ANIMATION_THRESHOLD)) return;
		SwitchToIdle();
	}

	private void SwitchToIdle() {
		this.animationComponent.SetDefaultAnimationSpeed();
		this.stateManager.ChangeState(this.playerStateController.EntityStates.EntityIdle);
	}
}
