using UnityEngine;
using UnityEngine.InputSystem;
using ServiceLocatorPattern;
using Kope.Core.CompilerServices;
public class PanelController : UIState
{
    [SerializeField] private string panelName = "Menu";
    [SerializeField] private GameObject currentPanel;
    [SerializeField] private PlayerInputActionMap inputActionMap = PlayerInputActionMap.Menu;
    private InputManager inputManager;
    public string PanelName => this.panelName;
    public override void OnInit()
    {
        base.OnInit();
        if (this.currentPanel == null)
        {
            MyLogger.Error($"{this.panelName}Controller: {this.panelName} "
              + "Panel reference is missing!");
            return;
        }
        this.currentPanel.SetActive(false);
        if (GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager))
        {
            this.inputManager = inputManager;
        }
        else
        {
            MyLogger.Error($"{this.gameObject.name}Controller: InputManager service not found!");
        }
    }

    public override void EnterState() => OpenMenu();
    public override void ExitState() => ClosePanel();

    public void ToggleMenuUi(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            this.currentPanel.SetActive(!this.currentPanel.activeSelf);
            this.inputManager.SwitchActionMap(
                this.currentPanel.activeSelf ?
                this.inputActionMap :
                PlayerInputActionMap.Player);

        }
    }

    public void OpenMenu()
    {
        if (!this.currentPanel.activeSelf)
        {
            this.currentPanel.SetActive(true);
            this.inputManager.SwitchActionMap(this.inputActionMap);
        }
    }

    public void ClosePanel()
    {
        if (this.currentPanel.activeSelf)
        {
            this.currentPanel.SetActive(false);
            this.inputManager.SwitchActionMap(PlayerInputActionMap.Player);
        }
    }


}
