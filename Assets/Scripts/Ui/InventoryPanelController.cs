using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryPanelController : MonoBehaviour
{
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private InputManager inputManager;
    private void Awake()
    {
        this.inventoryPanel.SetActive(false);
    }

    public void ToggleInventoryUI(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            this.inventoryPanel.SetActive(!this.inventoryPanel.activeSelf);
            this.inputManager.SwitchActionMap(this.inventoryPanel.activeSelf ? InputActionType.Inventory : InputActionType.Player);
        }
    }
}
