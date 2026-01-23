
<<<<<<< HEAD
public class EntityStates
{
    private readonly EntityIdle entityIdle;
    private readonly EntityMove entityMove;
    private readonly EntityAttack entityAttack;

    public EntityIdle EntityIdle => this.entityIdle;
    public EntityMove EntityMove => this.entityMove;
    public EntityAttack EntityAttack => this.entityAttack;
    public EntityStates(EntityStateManager baseStateMachine, EntityContextManager playerStateController)
    {
        // the enumeration names must match the animation state names in the Animator Controller
        // to ensure correct state transitions and animations
        this.entityIdle = new EntityIdle(baseStateMachine, playerStateController, AnimationState.Idle);
        this.entityMove = new EntityMove(baseStateMachine, playerStateController, AnimationState.Walk);
        this.entityAttack = new EntityAttack(baseStateMachine, playerStateController);
=======
public class PlayerStates
{
    private readonly PlayerIdle playerIdle;
    private readonly PlayerMove playerMove;
    private readonly PlayerAttack playerAttack;

    public PlayerIdle PlayerIdle => this.playerIdle;
    public PlayerMove PlayerMove => this.playerMove;
    public PlayerAttack PlayerAttack => this.playerAttack;
    public PlayerStates(PlayerStateManager baseStateMachine, PlayerStateController playerStateController)
    {
        // the enumeration names must match the animation state names in the Animator Controller
        // to ensure correct state transitions and animations
        this.playerIdle = new PlayerIdle(baseStateMachine, playerStateController, AnimationState.Idle);
        this.playerMove = new PlayerMove(baseStateMachine, playerStateController, AnimationState.Walk);
        this.playerAttack = new PlayerAttack(baseStateMachine, playerStateController);
>>>>>>> master
    }
}
