using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttackComponent : AttackComponentBase
{
    private InputManager inputManager;

    public override void Init()
    {
        base.Init();
        // Fetch InputManager lazily - will find existing or create one
        // This works even if InputManager hasn't Awake'd yet because
        // GetOrCreateInstance uses FindFirstObjectByType which finds already-created instances
        if (this.inputManager == null)
        {
            this.inputManager = InputManager.GetOrCreateInstance();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // Ensure InputManager is set (in case OnEnable runs before Init)
        if (this.inputManager == null)
        {
            this.inputManager = InputManager.GetOrCreateInstance();
        }

        Subscribe();
    }
    void Start()
    {
        OnEnable();
    }
    private void Subscribe()
    {
        if (inputManager != null)
        {
            inputManager.SubscribeToInputAction(
                PlayerInputActionMap.Player,
                PlayerInputActionKey.Fire.ToString(),
                AttackForInputSystem
            );
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (inputManager != null)
        {
            inputManager.UnsubscribeFromInputAction(
                PlayerInputActionMap.Player,
                PlayerInputActionKey.Fire.ToString(),
                AttackForInputSystem
            );
        }
    }

    private void AttackForInputSystem(InputAction.CallbackContext context)
    {
        if (context.performed) PerformAttack();
    }

    public override void PerformAttack()
    {
        float damage = CalculateDamage();
        print($"Attack performed! Damage: {damage}, Base attack: {attack}");
        RaiseOnAttackPerformedEvent();
    }
}
