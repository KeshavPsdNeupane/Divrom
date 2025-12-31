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
        // write the string as the exact name of the animation state blendspace in the Animator
        // to avoid typos use consts or enums in future refactorings
        this.playerIdle = new PlayerIdle(baseStateMachine, playerStateController, "Idle");
        this.playerMove = new PlayerMove(baseStateMachine, playerStateController, "Walk");
        this.playerAttack = new PlayerAttack(baseStateMachine, playerStateController, "BasicAttack");
    }
}
