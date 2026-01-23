<<<<<<< HEAD
public class EntityStateManager
{
    private EntityBaseState currentState;
    public EntityBaseState CurrentState => this.currentState;
    public void Initialize(EntityBaseState state)
=======
public class PlayerStateManager
{
    private PlayerBaseState currentState;
    public PlayerBaseState CurrentState => this.currentState;
    public void Initialize(PlayerBaseState state)
>>>>>>> master
    {
        this.currentState = state;
        this.currentState?.Enter();
    }
<<<<<<< HEAD
    public void ChangeState(EntityBaseState state)
=======
    public void ChangeState(PlayerBaseState state)
>>>>>>> master
    {
        this.currentState?.Exit();
        this.currentState = state;
        this.currentState?.Enter();
    }

}
