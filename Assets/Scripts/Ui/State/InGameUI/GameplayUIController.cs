using UnityEngine;
using UnityEngine.InputSystem;

public class GameplayUIController : InitializableBase
{
    [SerializeField] private UIState inGameplayMenu;
    [SerializeField] private UIState inventoryMenu;
    private UIStateManager uiStateManager;
    private InputManager inputManager;

    public UIStateManager UIStateManager => this.uiStateManager;

    // Must be called In Init lifecycle
    // to setup the UIStateManager and default states
    public override void Init()
    {
        this.uiStateManager = new();
        this.uiStateManager.Init();
        // InputManager will be fetched in OnEnable when needed
        this.inputManager = InputManager.GetOrCreateInstance();
        SetInitialized();
    }

    void OnEnable()
    {
        Subscribe();
    }
    public void OnDisable()
    {
        if (this.uiStateManager != null)
        {
            if (this.uiStateManager.CurrentUIState != null)
            {
                this.uiStateManager.CurrentUIState.ExitState();
            }
            this.uiStateManager = null;
        }
        Unsubscribe();
    }
    private void Subscribe()
    {
        if (this.inputManager == null) return;
        this.inputManager.SubscribeToInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.ToggleMenu.ToString(),
            EsePressed);

        this.inputManager.SubscribeToInputAction(
            PlayerInputActionMap.Menu,
            PlayerInputActionKey.ToggleMenu.ToString(),
            EsePressed);

        this.inputManager.SubscribeToInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.InventoryToggle.ToString(),
            TabPressed);
        this.inputManager.SubscribeToInputAction(
            PlayerInputActionMap.Inventory,
            PlayerInputActionKey.InventoryToggle.ToString(),
            TabPressed);

    }

    private void Unsubscribe()
    {
        if (this.inputManager == null) return;
        this.inputManager.UnsubscribeFromInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.ToggleMenu.ToString(),
            EsePressed
        );
        this.inputManager.UnsubscribeFromInputAction(
            PlayerInputActionMap.Menu,
            PlayerInputActionKey.ToggleMenu.ToString(),
            EsePressed
        );


        this.inputManager.UnsubscribeFromInputAction(
            PlayerInputActionMap.Player,
            PlayerInputActionKey.InventoryToggle.ToString(),
            TabPressed
        );
        this.inputManager.UnsubscribeFromInputAction(
            PlayerInputActionMap.Inventory,
            PlayerInputActionKey.InventoryToggle.ToString(),
            TabPressed
        );
    }



    private void Update()
    {
        if (this.uiStateManager == null) return;

        this.uiStateManager.ProcessStateChanges();
        if (this.uiStateManager.CurrentUIState != null)
        {
            this.uiStateManager.CurrentUIState.UpdateState();
        }
    }


    public void EsePressed(InputAction.CallbackContext context)
    {

        if (context.performed)
        {
            if (this.uiStateManager.IsEmptyStateStack())
            {
                this.uiStateManager.AddState(this.inGameplayMenu, true);
            }
            else // here else since ese should pop any current state
            {
                this.uiStateManager.PopState();
            }
        }
    }

    public void TabPressed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            if (this.uiStateManager.IsEmptyStateStack())
            {
                this.uiStateManager.AddState(this.inventoryMenu, true);
            }
            // here we only pop if the current state is the inventory menu
            else if (this.uiStateManager.CurrentUIState == this.inventoryMenu)
            {
                this.uiStateManager.PopState();
            }
        }
    }
}
