using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum InputActionType
{
    Player,
    UI,
    Global
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

        DisableAllExcept(InputActionType.Global);
        EnableActionType(InputActionType.Global);
        EnableActionType(InputActionType.Player);
    }

    public void SwitchActionMap(InputActionType actionType)
    {
        DisableAllExcept(InputActionType.Global);
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

    private void DisableAllExcept(InputActionType exception = InputActionType.Global)
    {
        foreach (var kvp in this.actionMaps)
        {
            if (kvp.Key != exception)
                kvp.Value.Disable();
        }
    }
}
