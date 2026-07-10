using UnityEngine;
using UnityEngine.InputSystem;
using Kope.Core.ServiceLocator;
using Kope.Core.LifeTimeManagement;
using System.Collections.Generic;

public class GameplayUIController : InitializableBase, IUpdatable {

	[SerializeField] private UIState inGameplayMenu;
	[SerializeField] private UIState inventoryMenu;

	private UIStateManager uiStateManager;
	private InputManager inputManager;

	public UIStateManager UIStateManager => this.uiStateManager;

	private readonly List<InputActionSubscriptionLifetime<UIInputActionKey>> _inputSubscriptions = new();
	private bool _isSubscribed;

	protected override bool OnInit() {
		this.uiStateManager = new();
		this.uiStateManager.Init();

		if (!GlobalServiceLocator.Instance.TryGetService(out InputManager inputManager)) {
			Debug.LogError($"{this.gameObject.name}Controller: InputManager service not found!");
			return false;
		}

		this.inputManager = inputManager;
		return true;
	}

	void OnEnable() {
		if (this.IsInitialized) Subscribe();
	}

	public void OnDisable() {
		if (this.uiStateManager != null && this.uiStateManager.CurrentUIState != null) {
			this.uiStateManager.CurrentUIState.ExitState();
			this.uiStateManager = null;
		}
		Unsubscribe();
	}

	private void Subscribe() {
		if (this.inputManager == null || this._isSubscribed) return;

		this._inputSubscriptions.Clear();

		this._inputSubscriptions.AddRange(new[] {
			new InputActionSubscriptionLifetime<UIInputActionKey>(
				PlayerInputActionCollection.Player,
				UIInputActionKey.OpenMenu,
				EsePressed
			),
			new InputActionSubscriptionLifetime<UIInputActionKey>(
				PlayerInputActionCollection.UI,
				UIInputActionKey.Cancel,
				EsePressed
			),
			new InputActionSubscriptionLifetime<UIInputActionKey>(
				PlayerInputActionCollection.Player,
				UIInputActionKey.OpenInventory,
				TabPressed
			),
			new InputActionSubscriptionLifetime<UIInputActionKey>(
				PlayerInputActionCollection.UI,
				UIInputActionKey.Cancel,
				TabPressed
			)
		});

		this.inputManager.SubscribeBulk(this._inputSubscriptions);
		this._isSubscribed = true;
	}

	private void Unsubscribe() {
		if (this.inputManager == null || !this._isSubscribed) return;

		this.inputManager.UnSubscribeBulk(this._inputSubscriptions);
		this._inputSubscriptions.Clear();

		this._isSubscribed = false;
	}

	public void OnUpdate() {
		if (this.uiStateManager == null) return;

		this.uiStateManager.ProcessStateChanges();
		this.uiStateManager.UpdateState();
	}

	public void EsePressed(InputAction.CallbackContext context) {
		if (!context.performed || context.duration > 0.05f) return;
		// Work: Toggle in-game menu
		// if no ui is open, open the in-game menu, otherwise close any current ui (including the in-game menu)
		if (this.uiStateManager.IsEmptyStateStack()) {
			this.uiStateManager.AddState(this.inGameplayMenu, true);
		} else {
			this.uiStateManager.PopState();
		}
	}

	public void TabPressed(InputAction.CallbackContext context) {
		if (!context.performed || context.duration > 0.05f) return;
		// Work: Open inventory menu
		// to pop the inventory player just press the Cancel button (Esc or A in controller) 
		if (this.uiStateManager.IsEmptyStateStack()) {
			this.uiStateManager.AddState(this.inventoryMenu, true);
		}
	}
}