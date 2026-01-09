using UnityEngine;
using UnityEngine.InputSystem;
using ServiceLocatorPattern;
/// <summary>
/// Player-specific movement component that handles input and movement application.
/// The ApplyMovement Must be called externally, typically from a PlayerController or similar script.
/// For player to move, this Class just provides the movement logic based on input received.
/// Actual Movement application should be managed by the caller.
/// </summary>
public class PlayerMovementComponent : MovementComponentBase
{
    private InputManager inputManager;


    public override void Init()
    {
        base.Init();
        if (GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager))
        {
            this.inputManager = inputManager;
        }
        else
        {
            Logger.Error($"{this.gameObject.name}Controller: InputManager service not found!");
        }
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
    => SetDirection(context.ReadValue<Vector2>());


}
