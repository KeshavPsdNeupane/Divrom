using UnityEngine;
using Kope.Core.Init;
using Kope.Component.Movement;
using Kope.Component.Attack;
using Kope.Component.Animation;

public class EntityStateController : ComponentBase
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

	public bool CanStateMachineAcceptCommand => this.stateMachine == null || this.stateMachine.CurrentState is not IStateCanAcceptCommand state || state.CanStateAcceptExternalCommand;

	protected override bool OnInit()
	{
		try
		{
			this.stateMachine = new EntityStateManager();
			this.entityStates = new EntityStates(this.stateMachine, this);
			this.stateMachine.Initialize(this.entityStates.EntityIdle);
			return true;
		}
		catch (System.Exception ex)
		{
			Debug.LogError($"EntityStateController: Initialization failed with exception: {ex}" + this.HieararchyPath);
			return false;

		}
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

	protected override void OnUpdate()
	{
		base.OnUpdate();

		this.stateMachine?.CurrentState?.TickUpdate();
	}


	protected override void OnFixedUpdate() => this.stateMachine?.CurrentState?.TickPhysicUpdate();


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
