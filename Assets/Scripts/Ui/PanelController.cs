using UnityEngine;
using UnityEngine.InputSystem;

public class PanelController : UIState
{
    [SerializeField] private string panelName = "Menu";
    [SerializeField] private GameObject currentPanel;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private InputActionType currentActionType = InputActionType.Menu;

    public string PanelName => this.panelName;
    public override void Init()
    {
        if (this.currentPanel == null)
        {
            Debug.LogError($"{this.panelName}Controller: {this.panelName} "
            + "Panel reference is missing!");
            return;
        }
        if (this.inputManager == null)
        {
            Debug.LogError($"{this.panelName}Controller:" +
            " InputManager reference is missing!");
            return;
        }
        this.currentPanel.SetActive(false);
    }

    public override void EnterState() => OpenMenu();
    public override void ExitState() => ClosePanel();

    public void ToggleMenuUi(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            this.currentPanel.SetActive(!this.currentPanel.activeSelf);
            this.inputManager.SwitchActionMap(this.currentPanel.activeSelf ? currentActionType : InputActionType.Player);
        }
    }

    public void OpenMenu()
    {
        if (!this.currentPanel.activeSelf)
        {
            this.currentPanel.SetActive(true);
            this.inputManager.SwitchActionMap(currentActionType);
        }
    }

    public void ClosePanel()
    {
        if (this.currentPanel.activeSelf)
        {
            this.currentPanel.SetActive(false);
            this.inputManager.SwitchActionMap(InputActionType.Player);
        }
    }


}
