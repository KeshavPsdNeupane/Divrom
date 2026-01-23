
<<<<<<< HEAD
public class EntityBaseState
{
    protected EntityContextManager playerStateController;
    protected EntityStateManager stateManager;
=======
public class PlayerBaseState
{
    protected PlayerStateController playerStateController;
    protected PlayerStateManager stateManager;
>>>>>>> master

    public virtual AnimationState AnimationState { get; protected set; }
    public virtual int AnimationStateHash { get; protected set; }

<<<<<<< HEAD
    public EntityBaseState(EntityStateManager StateManager,
        EntityContextManager playerStateController)
=======
    public PlayerBaseState(PlayerStateManager StateManager,
        PlayerStateController playerStateController)
>>>>>>> master
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
