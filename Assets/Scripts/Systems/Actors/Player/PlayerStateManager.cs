using UnityEngine;

public class PlayerStateManager
{
    private PlayerBaseState currentState;
    public PlayerBaseState CurrentState => this.currentState;
    public void Initialize(PlayerBaseState state)
    {
        this.currentState = state;
        this.currentState?.Enter();
    }
    public void ChangeState(PlayerBaseState state)
    {
        this.currentState?.Exit();
        this.currentState = state;
        this.currentState?.Enter();
    }

}
