using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStateController : MonoBehaviour
{
    [field: SerializeField] public MovementComponent movementComponent { get; private set; }
    [field: SerializeField] public AnimationComponent animationComponent { get; private set; }

    public PlayerStateManager stateMachine { get; private set; }
    public PlayerStates playerStates { get; private set; }

    private void Awake()
    {
        this.stateMachine = new PlayerStateManager();
        this.playerStates = new PlayerStates(this.stateMachine, this);
        this.stateMachine.Initialize(this.playerStates.playerIdle);
        this.animationComponent.OnAnimationTrigger += HandleAnimationTrigger;
    }

    private void OnDestroy()
    {
        this.animationComponent.OnAnimationTrigger -= HandleAnimationTrigger;
    }

    private void Update() => this.stateMachine.currentState.Update();
    private void FixedUpdate() => this.stateMachine.currentState.PhysicUpdate();

    public void SetMoveDirection(Vector2 dir) => this.movementComponent.direction = dir;

    public void Move(InputAction.CallbackContext context)
    {
        this.movementComponent.direction = context.ReadValue<Vector2>();
    }

    public void Attack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            this.stateMachine.ChangeState(this.playerStates.playerAttack);
        }
    }

    private void HandleAnimationTrigger()
    {
        this.stateMachine.currentState.OnAnimationTrigger();
    }
}
