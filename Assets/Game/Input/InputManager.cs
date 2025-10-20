using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum InputActionType
{
    Player,
    UI,
    Menu,
    Inventory,
}

public class InputManager : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;

    private Dictionary<InputActionType, InputActionMap> actionMaps = new Dictionary<InputActionType, InputActionMap>();

    private void Awake()
    {
        if (this.playerInput == null)
        {
            Debug.LogError("PlayerInput reference not assigned in InputManager!");
            return;
        }

        foreach (InputActionType type in System.Enum.GetValues(typeof(InputActionType)))
        {
            var map = this.playerInput.actions.FindActionMap(type.ToString(), true);
            if (map != null)
                this.actionMaps[type] = map;
        }

        DisableAllExcept(InputActionType.Player);
    }

    public void SwitchActionMap(InputActionType actionType)
    {
        DisableAll();
        EnableActionType(actionType);
    }

    public void EnableActionType(InputActionType actionType)
    {
        GetActionMap(actionType)?.Enable();
    }

    public void DisableActionType(InputActionType actionType)
    {
        GetActionMap(actionType)?.Disable();
    }

    private InputActionMap GetActionMap(InputActionType actionType)
    {
        this.actionMaps.TryGetValue(actionType, out var map);
        return map;
    }


    private void DisableAll()
    {
        foreach (var kvp in this.actionMaps)
        {
            kvp.Value.Disable();
        }
    }
    private void DisableAllExcept(InputActionType exception)
    {
        foreach (var kvp in this.actionMaps)
        {
            if (kvp.Key != exception)
                kvp.Value.Disable();
        }
    }
}
