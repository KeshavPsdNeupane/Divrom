

/// <summary>
/// Base class for all entity states.
/// Doesn't implement CanAcceptCommand to force derived states to implement it.
/// Child of this class must implement CanAcceptCommand.
/// </summary>
public abstract class EntityBaseState : IStateCanAcceptCommand {
	protected EntityStateController playerStateController;
	protected EntityStateManager stateManager;

	public virtual AnimationState AnimationState { get; protected set; }
	public virtual int AnimationStateHash { get; protected set; }
	public abstract bool CanAcceptCommand { get; }

	public EntityBaseState(EntityStateManager StateManager,
		EntityStateController playerStateController) {
		this.stateManager = StateManager;
		this.playerStateController = playerStateController;

	}

	public virtual void Enter() { }

	public virtual void TickUpdate() { }

	public virtual void TickPhysicUpdate() { }

	public virtual void Exit() { }

	public virtual void OnAnimationTrigger() { }
}
