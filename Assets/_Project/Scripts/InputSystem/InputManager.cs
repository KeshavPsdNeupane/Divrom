using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;
using Kope.Core.ServiceLocator;

/*
 * =================================================================================
 * ARCHITECTURAL NOTE: LOGICAL VS. PHYSICAL MAPPING
 * ---------------------------------------------------------------------------------
 * These Enums represent LOGICAL groupings for code clarity. They do not need to 
 * match the Map structure in the .inputactions asset 1:1.
 * * Example: 'OpenMenu' is in 'UIInputActionKey' logically, but might physically 
 * exist inside the 'Player' Map in the Input Asset so it can be pressed during 
 * gameplay. The InputManager handles this abstraction.
 * =================================================================================
 */

/*
 * =================================================================================
 * REBINDING USAGE GUIDE:
 * ---------------------------------------------------------------------------------
 * The 'bindingIndex' is the path index in the Input Action Asset.
 * * 1. Simple Button (Jump/Fire):
 * StartInteractiveRebind(..., PlayerInputActionKey.Fire, 0); 
 * // Index 0: The only binding available.
 *
 * 2. 2D Vector Composite (Move/WASD):
 * StartInteractiveRebind(..., PlayerInputActionKey.Move, 1); // UP
 * StartInteractiveRebind(..., PlayerInputActionKey.Move, 2); // DOWN
 * StartInteractiveRebind(..., PlayerInputActionKey.Move, 3); // LEFT
 * StartInteractiveRebind(..., PlayerInputActionKey.Move, 4); // RIGHT
 * // Note: Index 0 is the "Composite" container itself; sub-bindings start at 1.
 *
 * 3. 1D Axis (Zoom/Lean):
 * StartInteractiveRebind(..., PlayerInputActionKey.Zoom, 1); // NEGATIVE
 * StartInteractiveRebind(..., PlayerInputActionKey.Zoom, 2); // POSITIVE
 * =================================================================================
 */

public enum PlayerInputActionCollection {
	// Always reserve 0 for 'None' to detect unassigned references in the Inspector.
	None = 0,

	/* WARNING: Unity serializes Enums as integers. Changing these values 
     * (e.g., Menu = 21 to Menu = 25) will break any existing references 
     * saved in your Prefabs or Scene objects. */
	Player = 1,
	UI = 11,
}

public enum PlayerInputDevice {
	None = 0,
	KeyboardMouse = 1,
	Gamepad = 11,
	Touch = 21,
	Unknown = 99
}

public enum PlayerInputActionKey {
	None = 0,

	// movement and camera controls
	Move = 1,
	Look = 5,

	// Action buttons. Keep sequential for potential index-based logic.
	Fire = 11,
	Dodge = 15,
	// Kept sequential to allow for potential index-based logic or loops.
	Ability1 = 21,
	Ability2 = 22,
	Ability3 = 23,
	Ability4 = 24,

}

public enum UIInputActionKey {
	None = 0,
	OpenMenu = 11,
	OpenInventory = 21,

	/* RemoveTopUIStack is intended for a 'Back' button or Escape key functionality.
     * It allows UI panels to close without needing to know which specific 
     * panel is currently active. */
	RemoveTopUIStack = 31,

	Navigate = 51,
	// Submit button for ui either Enter or A on a controller, this is the "Confirm" button in many UI contexts
	Submit = 52,
	// this is the "Back" button in many UI contexts, often mapped to Escape or B on a controller
	// this will replace the  RemoveTopUIStack action in the future, but for now we will keep both for compatibility
	// since cancel is more standardized and can be used in more contexts than just UI, we will
	// use it for all new actions
	Cancel = 53,
	Point = 54,
	Click = 55,
	ScrollWheel = 56,
	MiddleClick = 57,
	RightClick = 58,
	TrackedDevicePosition = 59,
	TrackedDeviceOrientation = 60,

	// Unified bumper tab navigation contexts
	TabCycleLeft = 71,
	TabCycleRight = 72
}

/// <summary>
/// A memory-efficient container for managing input subscriptions.
/// Uses generics <typeparamref name="TEnum"/> to prevent boxing of Enum keys.
/// </summary>
public readonly struct InputActionSubscriptionLifetime<TEnum> where TEnum : Enum {
	public readonly PlayerInputActionCollection Map;
	public readonly TEnum Key;
	public readonly Action<InputAction.CallbackContext> Callback;
	public readonly bool IncludeCanceled;

	public InputActionSubscriptionLifetime(
		PlayerInputActionCollection map,
		TEnum key,
		Action<InputAction.CallbackContext> callback,
		bool includeCanceled = false
	) {
		this.Map = map;
		this.Key = key;
		this.Callback = callback;
		this.IncludeCanceled = includeCanceled;
	}
}

public class InputManager : GlobalServiceBase {
	private CustomPlayerInputs playerInput;
	private readonly Dictionary<PlayerInputActionCollection, InputActionMap> actionMaps = new();

	private float lastDeviceSwitchTime;
	// Minimum seconds required between layout swaps
	private const float DEVICE_SWITCH_COOLDOWN = 0.2f;

	// --- AUTOMATED DEVICE DETECTION MODULE ---
	private PlayerInputDevice currentDevice = PlayerInputDevice.None;

	/// <summary> Fires dynamically the exact frame the player touches a different hardware category. </summary>
	//	public event Action<PlayerInputDevice> OnDeviceSwitched;

	/// <summary> Exposes the currently active player device classification tracking record. </summary>
	public PlayerInputDevice CurrentDevice => this.currentDevice;

	// Property to access the raw generated C# class if needed for direct polling
	public CustomPlayerInputs PlayerInputs => this.playerInput;

	protected override bool OnInitializeService() {
		InitializeActionMaps();

		// Hook into the global engine input processing pipeline to listen for device mutations
		//InputSystem.onActionChange += OnGlobalActionChange;

		return true;
	}

	private void InitializeActionMaps() {
		this.playerInput = new CustomPlayerInputs();

		// Dynamically link our Enum keys to the physical Action Maps in the Asset
		foreach (PlayerInputActionCollection type in Enum.GetValues(typeof(PlayerInputActionCollection))) {
			if (type == PlayerInputActionCollection.None) continue;

			InputActionMap map = GetActionMapByType(type);
			if (map != null) {
				actionMaps[type] = map;
			} else {
				Debug.LogWarning($"[InputManager] Failed to find physical ActionMap in Asset for Enum: {type}");
			}
		}

		DisableAllInputs();
		EnableActionType(PlayerInputActionCollection.Player);
	}

	// /// <summary>
	// /// Processes changes across all active action mappings to evaluate what physical control hardware generated the input event.
	// /// </summary>
	// // --- Add these tracking fields to the top of your InputManager class ---
	// private void OnGlobalActionChange(object obj, InputActionChange change) {
	// 	if (change != InputActionChange.ActionPerformed) return;

	// 	var action = obj as InputAction;
	// 	if (action == null || action.activeControl == null) return;

	// 	// --- GUARDRAIL 1: TIME COOLDOWN ---
	// 	// If we just switched layouts a split second ago, ignore fast jitter bounce
	// 	if (Time.unscaledTime - this.lastDeviceSwitchTime < DEVICE_SWITCH_COOLDOWN) return;

	// 	InputDevice device = action.activeControl.device;

	// 	// --- GUARDRAIL 2: ANALOG DRIFT RESOLUTION ---
	// 	// If the incoming action is an axis/pointer movement, verify it crossed a deliberate threshold
	// 	if (action.activeControl.valueType == typeof(Vector2)) {
	// 		Vector2 value = action.ReadValue<Vector2>();

	// 		if (device is Mouse) {
	// 			// For mice, look at the delta (movement speed) rather than absolute screen positioning.
	// 			// If the mouse is just sitting there vibrating a sub-pixel, ignore it.
	// 			Vector2 mouseDelta = Mouse.current.delta.ReadValue();
	// 			if (mouseDelta.sqrMagnitude < 0.5f) return;
	// 		} else if (device is Gamepad) {
	// 			// Standard hardware thumbstick deadzone filtering
	// 			if (value.sqrMagnitude < 0.05f) return;
	// 		}
	// 	}

	// 	// --- ROUTE & UPDATE LAYOUT ---
	// 	PlayerInputDevice detectedDevice = PlayerInputDevice.Unknown;

	// 	if (device is Keyboard || device is Mouse) {
	// 		detectedDevice = PlayerInputDevice.KeyboardMouse;
	// 	} else if (device is Gamepad) {
	// 		detectedDevice = PlayerInputDevice.Gamepad;
	// 	} else if (device is Touchscreen) {
	// 		detectedDevice = PlayerInputDevice.Touch;
	// 	}

	// 	if (detectedDevice != PlayerInputDevice.Unknown && detectedDevice != this.currentDevice) {
	// 		this.currentDevice = detectedDevice;
	// 		this.lastDeviceSwitchTime = Time.unscaledTime; // Anchor timestamp

	// 		Debug.Log($"[Kope.Input] Hardware context shifted smoothly to: {this.currentDevice}");
	// 		OnDeviceSwitched?.Invoke(this.currentDevice);
	// 	}
	// }
	private InputActionMap GetActionMapByType(PlayerInputActionCollection type) {
		return type switch {
			PlayerInputActionCollection.Player => this.playerInput.Player,
			PlayerInputActionCollection.UI => this.playerInput.UI,
			_ => null
		};
	}

	#region Map Management

	public void EnableActionType(PlayerInputActionCollection actionType) {
		if (actionMaps.TryGetValue(actionType, out var map)) map.Enable();
	}

	public void DisableActionType(PlayerInputActionCollection actionType) {
		if (actionMaps.TryGetValue(actionType, out var map)) map.Disable();
	}

	/// <summary>
	/// Disables all maps and enables the target map.
	/// Calling DisableAllInputs first prevents "Input Bleed" (keys held across maps).
	/// </summary>
	public void SwitchActionMap(PlayerInputActionCollection actionType) {
		DisableAllInputs();
		EnableActionType(actionType);
	}

	public void SetDefaultActionMap() => SwitchActionMap(PlayerInputActionCollection.Player);

	public void DisableAllInputs() {
		foreach (var kvp in actionMaps) kvp.Value.Disable();
	}

	#endregion

	#region Subscription System

	/// <summary>
	/// Subscribes a callback to a specific action. 
	/// RATIONALE: We use .ToString() inside because subscriptions are "Cold Paths" 
	/// occurring at Init. This avoids complex generic dictionary caching.
	/// </summary>
	public void Subscribe<TEnum>(InputActionSubscriptionLifetime<TEnum> inputAction) where TEnum : Enum {
		if (actionMaps.TryGetValue(inputAction.Map, out var actionMap)) {
			string actionName = inputAction.Key.ToString();
			var action = actionMap.FindAction(actionName);

			if (action != null) {
				action.performed += inputAction.Callback;
				if (inputAction.IncludeCanceled) action.canceled += inputAction.Callback;
			} else {
				Debug.LogWarning($"[InputManager] Action '{actionName}' not found in Map '{inputAction.Map}'. " +
								 "Ensure Enum name matches Action name in Input Asset exactly.");
			}
		}
	}

	public void SubscribeBulk<TEnum>(IEnumerable<InputActionSubscriptionLifetime<TEnum>> inputActions) where TEnum : Enum {
		foreach (var action in inputActions) Subscribe(action);
	}

	public void UnSubscribe<TEnum>(InputActionSubscriptionLifetime<TEnum> inputAction) where TEnum : Enum {
		if (actionMaps.TryGetValue(inputAction.Map, out var actionMap)) {
			string actionName = inputAction.Key.ToString();
			var action = actionMap.FindAction(actionName);

			if (action != null) {
				action.performed -= inputAction.Callback;
				if (inputAction.IncludeCanceled) action.canceled -= inputAction.Callback;
			}
		}
	}

	public void UnSubscribeBulk<TEnum>(IEnumerable<InputActionSubscriptionLifetime<TEnum>> inputActions) where TEnum : Enum {
		foreach (var action in inputActions) UnSubscribe(action);
	}

	#endregion

	#region Rebinding

	/// <summary>
	/// Initiates interactive rebind. 
	/// Index 0: Buttons. 
	/// Index 1-4: Composite parts (WASD/D-Pad).
	/// </summary>
	public void StartInteractiveRebind(
		PlayerInputActionCollection mapType,
		Enum key,
		int bindingIndex,
		PlayerInputDevice deviceLimit,
		Action onComplete = null) {

		if (!actionMaps.TryGetValue(mapType, out var map)) return;
		var action = map.FindAction(key.ToString());

		if (action == null) return;

		// Disable all input during the listening phase to prevent unintended actions
		playerInput.Disable();

		var rebindOp = action.PerformInteractiveRebinding(bindingIndex)
			.WithControlsExcluding("<Mouse>/position")
			.WithControlsExcluding("<Mouse>/delta");

		string group = GetGroupFromDevice(deviceLimit);
		if (!string.IsNullOrEmpty(group)) rebindOp.WithBindingGroup(group);

		rebindOp.OnMatchWaitForAnother(0.1f)
			.OnComplete(operation => {
				operation.Dispose();
				playerInput.Enable();
				onComplete?.Invoke();
			})
			.OnCancel(operation => {
				operation.Dispose();
				playerInput.Enable();
			});

		rebindOp.Start();
	}

	private string GetGroupFromDevice(PlayerInputDevice device) {
		return device switch {
			PlayerInputDevice.KeyboardMouse => "Keyboard&Mouse",
			PlayerInputDevice.Gamepad => "Gamepad",
			PlayerInputDevice.Touch => "Touch",
			_ => string.Empty
		};
	}

	#endregion

	private void OnDestroy() {
		// Clean up global engine hooks cleanly to avoid lingering allocation leaks
		//InputSystem.onActionChange -= OnGlobalActionChange;

		if (this.playerInput != null) {
			this.playerInput.Disable();
			this.playerInput.Dispose();
			this.playerInput = null;
		}
	}
}