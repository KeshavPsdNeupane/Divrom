using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementComponent : MovementComponentBase
{
    private InputManager inputManager;
    [SerializeField] private CharacterStatsSystem characterStats;
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
        SetDirection(context.ReadValue<Vector2>());
        this.directionChanged = true;
    }

    public override void ApplyMovement(float movementSpeedMult = 1.0f)
    {
        if (this.directionChanged)
        {
            this.normalizedDirection = this.direction.normalized;
            this.directionChanged = false;
        }

        this.rb.linearVelocity = movementSpeed * movementSpeedMult * normalizedDirection;
    }
}
