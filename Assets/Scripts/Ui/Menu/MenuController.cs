using UnityEngine;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private InputManager inputManager;

    private void Awake()
    {
        this.menuPanel.SetActive(false);
    }

    public void ToggleMenuUi(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            this.menuPanel.SetActive(!this.menuPanel.activeSelf);
            this.inputManager.SwitchActionMap(this.menuPanel.activeSelf ? InputActionType.Menu : InputActionType.Player);
        }
    }
}
