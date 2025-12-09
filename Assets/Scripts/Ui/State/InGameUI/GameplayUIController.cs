using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GameplayUIController : InitializableBase
{
    [SerializeField] private UIState inGameplayMenu;
    [SerializeField] private UIState inventoryMenu;
    private UIStateManager uiStateManager;

    public UIStateManager UIStateManager => this.uiStateManager;

    // Must be called In Init lifecycle
    // to setup the UIStateManager and default states
    public override void Init()
    {
        this.uiStateManager = new();
        this.uiStateManager.Init();
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
