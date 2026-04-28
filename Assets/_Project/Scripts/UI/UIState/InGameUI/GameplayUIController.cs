using UnityEngine;
using UnityEngine.InputSystem;
using ServiceLocatorPattern;
using Kope.Core.CompilerServices;
using Kope.Core.Init;
using System.Collections.Generic;

public class GameplayUIController : InitializableBase {

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
			MyLogger.Error($"{this.gameObject.name}Controller: InputManager service not found!");
			return false;
		}

		this.inputManager = inputManager;
		return true;
	}

	void OnEnable() {
		if (this.IsInitialized) Subscribe();
	}

	public void OnDisable() {
		if (this.uiStateManager != null) {
			this.uiStateManager.CurrentUIState?.ExitState();
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
				PlayerInputActionCollection.Menu,
				UIInputActionKey.RemoveTopUIStack,
				EsePressed
			),
			new InputActionSubscriptionLifetime<UIInputActionKey>(
				PlayerInputActionCollection.Player,
				UIInputActionKey.OpenInventory,
				TabPressed
			),
			new InputActionSubscriptionLifetime<UIInputActionKey>(
				PlayerInputActionCollection.Inventory,
				UIInputActionKey.RemoveTopUIStack,
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

	protected override void OnUpdate() {
		base.OnUpdate();

		if (Mouse.current.rightButton.isPressed) {
			Unsubscribe();
			return;
		}

		if (this.uiStateManager == null) return;

		this.uiStateManager.ProcessStateChanges();
		this.uiStateManager.CurrentUIState?.UpdateState();
	}

	public void EsePressed(InputAction.CallbackContext context) {
		if (!context.performed || context.duration > 0.05f) return;

		if (this.uiStateManager.IsEmptyStateStack()) {
			this.uiStateManager.AddState(this.inGameplayMenu, true);
		} else {
			this.uiStateManager.PopState();
		}
	}

	public void TabPressed(InputAction.CallbackContext context) {
		if (!context.performed || context.duration > 0.05f) return;

		if (this.uiStateManager.IsEmptyStateStack()) {
			this.uiStateManager.AddState(this.inventoryMenu, true);
		} else {
			this.uiStateManager.PopState();
		}
	}
}