using UnityEngine;

/// <summary>
/// Controls the player's state and interactions.
/// Must be placed Under the Root Player GameObject
/// For the HandleAnimationTrigger to work
///</summary>
public class PlayerStateController : InitializableBase
{
    // Need the [field: SerializeField] syntax to make auto-properties
    // serializable in Unity, since we cant use SerializeField with Geter and Setter
    // So i have to use this syntax to keep the properties encapsulated
    // while still allowing Unity to serialize them.
    [SerializeField] private MovementComponent movementComponent;
    [SerializeField] private AnimationComponent animationComponent;
    [SerializeField] private AttackComponent attackComponent;
    private PlayerStateManager stateMachine;
    private PlayerStates playerStates;


    // Public getters for the components
    public MovementComponent MovementComponent => movementComponent;
    public AnimationComponent AnimationComponent => animationComponent;
    public AttackComponent AttackComponent => attackComponent;

    public PlayerStateManager StateMachine => stateMachine;
    public PlayerStates PlayerStates => playerStates;



    public override void Init()
    {
        this.stateMachine = new PlayerStateManager();
        this.playerStates = new PlayerStates(this.stateMachine, this);
        this.stateMachine.Initialize(this.playerStates.PlayerIdle);
        SetInitialized();
    }

    private void OnEnable()
    {
        this.animationComponent.OnAnimationTrigger += HandleAnimationTrigger;
        this.attackComponent.OnAttackPerformed += Attack;
    }

    private void OnDisable()
    {
        this.animationComponent.OnAnimationTrigger -= HandleAnimationTrigger;
        this.attackComponent.OnAttackPerformed -= Attack;
    }

    private void Update() => this.stateMachine.CurrentState.Update();
    private void FixedUpdate() => this.stateMachine.CurrentState.PhysicUpdate();


    // this delegate func called when AttackComponent invokes OnAttackPerformed event
    // so we can change state to attack
    // this decouples the input system from the state controller
    // and allows for more flexible attack triggering
    private void Attack()
    {
        this.stateMachine.ChangeState(this.playerStates.PlayerAttack);
    }

    // this delegate func called when AnimationComponent invokes OnAnimationTrigger event
    // so we can notify the current state about the animation trigger
    // allowing states to react accordingly (e.g., transition states)
    // this decouples the animation system from the state controller
    // Might also attach this to a UnityEvent so i dont have to put 
    // this class on Root Player GameObject
    public void HandleAnimationTrigger()
    {
        this.stateMachine.CurrentState.OnAnimationTrigger();
    }
}
