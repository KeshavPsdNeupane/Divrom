using UnityEngine.InputSystem;
using ServiceLocatorPattern;
using UnityEngine;
public class PlayerAttackComponent : AttackComponentBase
{
    private InputManager inputManager;
    [SerializeField] WeaponSO equippedWeaponDataSO;
    public WeaponData EquippedWeaponData => this.equippedWeaponDataSO.CurrentWeaponData;
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

    public override void PerformAttack()
    {
        float damage = CalculateDamage();
        // For testing so no need for logger
        Logger.Log($"Attack performed! Damage: {damage}, Base attack: {attack}");
        RaiseOnAttackPerformedEvent();
    }
}
