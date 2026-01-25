
public class EntityBaseState
{
    protected EntityStateController playerStateController;
    protected EntityStateManager stateManager;

    public virtual AnimationState AnimationState { get; protected set; }
    public virtual int AnimationStateHash { get; protected set; }

    public EntityBaseState(EntityStateManager StateManager,
        EntityStateController playerStateController)
    {
        this.stateManager = StateManager;
        this.playerStateController = playerStateController;

    }

    public virtual void Enter() { }

    public virtual void Update() { }

    public virtual void PhysicUpdate() { }

    public virtual void Exit() { }

    public virtual void OnAnimationTrigger() { }
}
