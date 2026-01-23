public class EntityStateManager
{
    private EntityBaseState currentState;
    public EntityBaseState CurrentState => this.currentState;
    public void Initialize(EntityBaseState state)
    {
        this.currentState = state;
        this.currentState?.Enter();
    }
    public void ChangeState(EntityBaseState state)
    {
        this.currentState?.Exit();
        this.currentState = state;
        this.currentState?.Enter();
    }

}
