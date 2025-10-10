using UnityEngine;

public class PlayerStates
{
    public PlayerIdle playerIdle;
    public PlayerMove playermove;
    public PlayerAttack playerAttack;

    public PlayerStates(PlayerStateManager baseStateMachine, PlayerStateController playerStateController)
    {
        this.playerIdle = new PlayerIdle(baseStateMachine, playerStateController, "Idle");
        this.playermove = new PlayerMove(baseStateMachine, playerStateController, "Walk");
        this.playerAttack = new PlayerAttack(baseStateMachine, playerStateController, "BasicAttack");
    }
}
