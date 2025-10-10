using UnityEngine;

public class PlayerStateManager
{
    public PlayerBaseState currentState { get; private set; }
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
