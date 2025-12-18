using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

/// <summary>
/// Maps input action map types to their corresponding action map names.
/// Used to dynamically enable/disable action maps without hardcoding.
/// </summary>
public enum PlayerInputActionMap
{
    Player,
    UI,
    Menu,
    Inventory,
}


public enum PlayerInputActionKey
{
    Move,
    Fire,
    ToggleMenu,
    InventoryToggle,
    Pause,
    OpenInventory,
}





/// <summary>
/// Centralized input manager using the generated PlayerInputs class.
/// Dynamically manages action maps without hardcoding individual disable calls.
/// </summary>


[DefaultExecutionOrder(-100)]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance;
    private PlayerInput playerInput;
    private readonly Dictionary<PlayerInputActionMap, InputActionMap> actionMaps = new();

    public PlayerInput PlayerInputs => this.playerInput;

    private void Awake()
    {
        // If another instance exists and it's not this one, destroy this one
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        // This is the instance to keep
        Instance = this;
        InitializeActionMaps();
    }
    public static InputManager GetOrCreateInstance()
    {
        if (Instance == null)
        {
            // Create new one if truly none exists
            GameObject go = new("InputManager");
            InputManager manager = go.AddComponent<InputManager>();
            return manager;
        }
        return Instance;
    }

    private void InitializeActionMaps()
    {
        this.playerInput = new PlayerInput();
        this.playerInput.Enable();

        // Map each InputMapType enum to the corresponding action map from PlayerInputs
        foreach (PlayerInputActionMap type in Enum.GetValues(typeof(PlayerInputActionMap)))
        {
            InputActionMap map = GetActionMapByType(type);
            if (map != null)
            {
                actionMaps[type] = map;
            }
            else
            {
                Debug.LogWarning($"Failed to load action map: {type}");
            }
        }

        DisableAllInputs();
        EnableActionType(PlayerInputActionMap.Player);
    }

    /// <summary>
    /// Returns the InputActionMap for a given InputMapType.
    /// </summary>
    private InputActionMap GetActionMapByType(PlayerInputActionMap type)
    {
        return type switch
        {
            PlayerInputActionMap.Player => this.playerInput.Player,
            PlayerInputActionMap.UI => this.playerInput.UI,
            PlayerInputActionMap.Menu => this.playerInput.Menu,
            PlayerInputActionMap.Inventory => this.playerInput.Inventory,
            _ => null
        };
    }

    /// <summary>
    /// Enables a specific action map type.
    /// </summary>
    public void EnableActionType(PlayerInputActionMap actionType)
    {
        if (actionMaps.TryGetValue(actionType, out var map))
        {
            map.Enable();
        }
    }

    /// <summary>
    /// Disables a specific action map type.
    /// </summary>
    public void DisableActionType(PlayerInputActionMap actionType)
    {
        if (actionMaps.TryGetValue(actionType, out var map))
        {
            map.Disable();
        }
    }

    /// <summary>
    /// Switches to one action map type and disables all others.
    /// </summary>
    public void SwitchActionMap(PlayerInputActionMap actionType)
    {
        DisableAllInputs();
        EnableActionType(actionType);
    }

    /// <summary>
    /// Disables all input action maps using a loop (no hardcoding).
    /// </summary>
    public void DisableAllInputs()
    {
        foreach (var kvp in actionMaps)
        {
            kvp.Value.Disable();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            this.playerInput.Dispose();
        }
    }

    public void SubscribeToInputAction(PlayerInputActionMap actionType, string actionName, Action<InputAction.CallbackContext> callback)
    {
        if (actionMaps.TryGetValue(actionType, out var map))
        {
            var action = map.FindAction(actionName);
            if (action != null)
            {
                action.performed += callback;
                if (actionName == PlayerInputActionKey.Move.ToString())
                {
                    action.canceled += callback;
                }
            }
        }
        else
        {
            print($"Action map not found for type: {actionType}, cannot subscribe to action: {actionName}");
        }
    }
    public void UnsubscribeFromInputAction(PlayerInputActionMap actionType, string actionName, Action<InputAction.CallbackContext> callback)
    {
        if (actionMaps.TryGetValue(actionType, out var map))
        {
            var action = map.FindAction(actionName);
            if (action != null)
            {
                action.performed -= callback;
                if (actionName == PlayerInputActionKey.Move.ToString())
                {
                    action.canceled -= callback;
                }
            }
        }
    }
}
