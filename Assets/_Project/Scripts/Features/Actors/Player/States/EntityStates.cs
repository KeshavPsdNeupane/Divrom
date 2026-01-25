
public class EntityStates
{
    private readonly EntityIdle entityIdle;
    private readonly EntityMove entityMove;
    private readonly EntityAttack entityAttack;

    public EntityIdle EntityIdle => this.entityIdle;
    public EntityMove EntityMove => this.entityMove;
    public EntityAttack EntityAttack => this.entityAttack;
    public EntityStates(EntityStateManager baseStateMachine, EntityStateController playerStateController)
    {
        // the enumeration names must match the animation state names in the Animator Controller
        // to ensure correct state transitions and animations
        this.entityIdle = new EntityIdle(baseStateMachine, playerStateController, AnimationState.Idle);
        this.entityMove = new EntityMove(baseStateMachine, playerStateController, AnimationState.Walk);
        this.entityAttack = new EntityAttack(baseStateMachine, playerStateController);
    }
}
