using UnityEngine;
using Kope.Core.Init;
/// <summary>
/// Controls the player's state and interactions.
/// Must be placed Under the Root Player GameObject
/// For the HandleAnimationTrigger to work
///</summary>
<<<<<<< HEAD

public class EntityContextManager : InitializableBase
{
    [SerializeField] private MovementComponentBase movementComponent;
    [SerializeField] private AnimationComponentBase animationComponent;
    [SerializeField] private AttackComponentBase attackComponent;
    private EntityStateManager stateMachine;
    private EntityStates entityStates;

    // Public getters for the components
    public MovementComponentBase MovementComponent => movementComponent;
    public AnimationComponentBase AnimationComponent => animationComponent;
    public AttackComponentBase PlayerAttackComponent => attackComponent;

    public EntityStateManager StateMachine => stateMachine;
    public EntityStates EntityStates => entityStates;
=======

public class PlayerStateController : InitializableBase
{
    [SerializeField] private MovementComponentBase movementComponent;
    [SerializeField] private AnimationComponent animationComponent;
    [SerializeField] private AttackComponentBase attackComponent;
    private PlayerStateManager stateMachine;
    private PlayerStates playerStates;

    // Public getters for the components
    public MovementComponentBase MovementComponent => movementComponent;
    public AnimationComponent AnimationComponent => animationComponent;
    public AttackComponentBase PlayerAttackComponent => attackComponent;

    public PlayerStateManager StateMachine => stateMachine;
    public PlayerStates PlayerStates => playerStates;
>>>>>>> master

    public override void Init()
    {
        this.stateMachine = new EntityStateManager();
        this.entityStates = new EntityStates(this.stateMachine, this);
        this.stateMachine.Initialize(this.entityStates.EntityIdle);
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

    private void Update() => this.stateMachine?.CurrentState?.Update();
    private void FixedUpdate() => this.stateMachine?.CurrentState?.PhysicUpdate();


    // this delegate func called when AttackComponent invokes OnAttackPerformed event
    // so we can change state to attack
    // this decouples the input system from the state controller
    // and allows for more flexible attack triggering
    private void Attack()
    {
        this.stateMachine.ChangeState(this.entityStates.EntityAttack);
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
