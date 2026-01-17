
public class PlayerBaseState
{
    protected PlayerStateController playerStateController;
    protected PlayerStateManager stateManager;

    public virtual AnimationState AnimationState { get; protected set; }
    public virtual int AnimationStateHash { get; protected set; }

    public PlayerBaseState(PlayerStateManager StateManager,
        PlayerStateController playerStateController)
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
