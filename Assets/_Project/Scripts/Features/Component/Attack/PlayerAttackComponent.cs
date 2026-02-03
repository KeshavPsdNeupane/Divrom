using UnityEngine.InputSystem;
using ServiceLocatorPattern;
using Kope.Core.CompilerServices;
public class PlayerAttackComponent : AttackComponentBase
{
    private InputManager inputManager;

    public override void OnInit()
    {
        base.OnInit();
        if (GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager))
        {
            this.inputManager = inputManager;
        }
        else
        {
            MyLogger.Error($"{this.gameObject.name}Controller: InputManager service not found!");
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Subscribe();
    }

    private void Subscribe()
    {
        if (inputManager == null) return;

        inputManager.SubscribeToInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.Fire.ToString(),
            AttackForInputSystem
       );

    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (inputManager == null) return;

        inputManager.UnsubscribeFromInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.Fire.ToString(),
            AttackForInputSystem
        );

    }

    private void AttackForInputSystem(InputAction.CallbackContext context)
    {
        if (context.performed) PerformAttack();
    }

    protected override void PerformAttackInternal()
    {

        float damage = CalculateDamage();
        // For testing so no need for logger
        MyLogger.Log($"Attack performed! Damage: {damage}, Base attack: {attack}");
    }
}
