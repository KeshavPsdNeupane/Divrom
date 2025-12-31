using UnityEngine;
using UnityEngine.InputSystem;
/// <summary>
/// Player-specific movement component that handles input and movement application.
/// The ApplyMovement Must be called externally, typically from a PlayerController or similar script.
/// For player to move, this Class just provides the movement logic based on input received.
/// Actual Movement application should be managed by the caller.
/// </summary>
public class PlayerMovementComponent : MovementComponentBase
{
    private InputManager inputManager;
    private Vector2 normalizedDirection;
    private bool directionChanged;

    public override void Init()
    {
        base.Init();
        this.inputManager = InputManager.GetOrCreateInstance();
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        Subscribe();
    }


    protected override void OnDisable()
    {
        base.OnDisable();
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (this.inputManager == null) return;
        this.inputManager.SubscribeToInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.Move.ToString(),
            MoveForInputSystem);
    }

    private void Unsubscribe()
    {
        if (this.inputManager == null) return;
        this.inputManager.UnsubscribeFromInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.Move.ToString(),
            MoveForInputSystem);
    }


    public void MoveForInputSystem(InputAction.CallbackContext context)
    {
        Vector2 newdirection = context.ReadValue<Vector2>();
        SetDirection(newdirection);
        this.directionChanged = true;
    }

    public override void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        if (this.directionChanged)
        {
            this.normalizedDirection = this.direction.normalized;
            this.directionChanged = false;
        }

        this.rb.linearVelocity = defaultMovementSpeed * movementSpeedMult * normalizedDirection;
    }
}
