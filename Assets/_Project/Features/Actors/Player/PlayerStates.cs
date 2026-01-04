
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
    }
}
