using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the player's state and interactions.
/// Must be placed Under the Root Player GameObject
/// For the HandleAnimationTrigger to work
///</summary>
public class PlayerStateController : InitializableBase
{
    [field: SerializeField] public MovementComponent movementComponent { get; private set; }
    [field: SerializeField] public AnimationComponent animationComponent { get; private set; }
    [field: SerializeField] public AttackComponent attackComponent { get; private set; }

    public PlayerStateManager stateMachine { get; private set; }
    public PlayerStates playerStates { get; private set; }

    public override void Init()
    {
        this.stateMachine = new PlayerStateManager();
        this.playerStates = new PlayerStates(this.stateMachine, this);
        this.stateMachine.Initialize(this.playerStates.playerIdle);
        this.animationComponent.OnAnimationTrigger += HandleAnimationTrigger;
        this.attackComponent.OnAttackPerformed += Attack;

    }

    private void OnDisable()
    {
        this.animationComponent.OnAnimationTrigger -= HandleAnimationTrigger;
        this.attackComponent.OnAttackPerformed -= Attack;
    }

    private void Update() => this.stateMachine.currentState.Update();
    private void FixedUpdate() => this.stateMachine.currentState.PhysicUpdate();


    // this delegate func called when AttackComponent invokes OnAttackPerformed event
    // so we can change state to attack
    // this decouples the input system from the state controller
    // and allows for more flexible attack triggering
    private void Attack()
    {
        this.stateMachine.ChangeState(this.playerStates.playerAttack);
    }

    // this delegate func called when AnimationComponent invokes OnAnimationTrigger event
    // so we can notify the current state about the animation trigger
    // allowing states to react accordingly (e.g., transition states)
    // this decouples the animation system from the state controller
    // Might also attach this to a UnityEvent so i dont have to put 
    // this class on Root Player GameObject
    public void HandleAnimationTrigger()
    {
        this.stateMachine.currentState.OnAnimationTrigger();
    }
}
