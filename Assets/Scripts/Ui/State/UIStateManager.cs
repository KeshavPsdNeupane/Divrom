using System.Collections.Generic;
using UnityEngine.InputSystem;


public class UIStateManager
{
    private Stack<UIState> currentStates;
    private UIState nextState;
    private bool isAdding = false;
    private bool isReplacing = false;
    public UIState CurrentUIState =>
    (this.currentStates != null && this.currentStates.Count > 0)
    ? this.currentStates.Peek() : null;

    public void Init() => this.currentStates ??= new Stack<UIState>();

    public int Size => this.currentStates.Count;

    public bool IsEmptyStateStack()
    => this.currentStates == null || this.currentStates.Count == 0;


    public void AddState(UIState newState, bool isReplace = false)
    {
        this.nextState = newState;
        this.isAdding = true;
        this.isReplacing = isReplace;
    }



    public void ProcessStateChanges()
    {
        if (!this.isAdding) return;

        if (!IsEmptyStateStack())
        {
            if (this.isReplacing)
            {
                UIState oldState = this.currentStates.Pop();
                oldState.ExitState();
            }
            else
            {
                UIState oldState = this.currentStates.Peek();
                oldState.ExitState();
            }
        }
        this.currentStates.Push(this.nextState);
        this.nextState.EnterState();
        this.isReplacing = false;
        this.isAdding = false;
        this.nextState = null;
    }

    public void PopStateInputSystem(InputAction.CallbackContext context)
    {
        if (context.performed) PopState();
    }


    public void PopState()
    {
        if (IsEmptyStateStack()) return;

        UIState top = this.currentStates.Pop();
        top.ExitState();

        if (!IsEmptyStateStack())
        {
            UIState newTop = this.currentStates.Peek();
            newTop.EnterState();
        }
    }

    public void ClearStates()
    {
        if (this.currentStates == null) return;
        while (this.currentStates.Count > 0)
        {
            var s = this.currentStates.Pop();
            s.ExitState();
        }
    }

    public void OnDestroy()
    {
        ClearStates();
        this.currentStates = null;
    }

}
