using UnityEngine;
using Kope.Core.ServiceLocator;

public class PanelController : UIState {
	[SerializeField] private string panelName = "Menu";
	[SerializeField] private GameObject currentPanel;
	[SerializeField] private PlayerInputActionCollection inputActionMap = PlayerInputActionCollection.UI;
	private InputManager inputManager;
	public string PanelName => this.panelName;
	protected override bool OnInit() {
		try {
			if (this.currentPanel == null) {
				Debug.LogError($"{this.panelName}Controller: {this.panelName} "
				  + "Panel reference is missing!");
				return false;
			}
			if (this.inputActionMap == PlayerInputActionCollection.None) {
				Debug.LogError($"{this.panelName}Controller: Input Action Map is not set!");
				return false;
			}
			this.currentPanel.SetActive(false);
			if (GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager)) {
				this.inputManager = inputManager;
			} else {
				Debug.LogError($"{this.gameObject.name}Controller: InputManager service not found!");
				return false;
			}
			return true;

		} catch (System.Exception ex) {
			Debug.LogError($"{this.panelName}Controller: Initialization failed with exception: {ex}" + this.GetParentGameObjectHeirarchyMessage());
			return false;

		}
	}

#if UNITY_EDITOR
	private void OnValidate() {
		// This ensures the developer sees the error in the inspector immediately
		if (!System.Enum.IsDefined(typeof(PlayerInputActionCollection), this.inputActionMap)) {
			Debug.LogError($"<color=red>CRITICAL:</color> {gameObject.name} has an invalid Enum index for InputMap! " +
						   $"Please reset it in the inspector. {this.GetParentGameObjectHeirarchyMessage()}", this);
		}
	}
#endif

	public override void EnterState() => OpenPanel();
	public override void ExitState() => ClosePanel();

	public void OpenPanel() {
		if (!this.currentPanel.activeSelf) {
			this.currentPanel.SetActive(true);
			this.inputManager.SwitchActionMap(this.inputActionMap);
		}
	}

	public void ClosePanel() {
		if (this.currentPanel.activeSelf) {
			this.currentPanel.SetActive(false);
			this.inputManager.SetDefaultActionMap();
		}
	}
}
