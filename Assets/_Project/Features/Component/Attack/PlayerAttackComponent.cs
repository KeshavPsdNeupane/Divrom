using UnityEngine.InputSystem;
using ServiceLocatorPattern;
public class PlayerAttackComponent : AttackComponentBase
{
    private InputManager inputManager;

    public override void Init()
    {
        base.Init();
        this.inputManager = GlobalServiceLocator.Instance.GetORCreateDefault<InputManager>();

    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Subscribe();
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
        // For testing so no need for logger
        Logger.Log($"Attack performed! Damage: {damage}, Base attack: {attack}");
        RaiseOnAttackPerformedEvent();
    }
}
