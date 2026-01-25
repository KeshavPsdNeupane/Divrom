using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ActionSO : ScriptableObject
{
    [SerializeField] private string actionName;
    [SerializeField] private List<ConsiderationSO> considerations;
    [SerializeField] private bool isInterruptible = true;
    private bool isCompleted = false;

    /// <summary>
    /// Event triggered when the action completes.
    /// Subscribe to this for event-driven completion instead of polling IsCompleted.
    /// </summary>
    public event Action OnActionCompleted;
    public string ActionName => actionName;
    public bool IsInterruptible => isInterruptible;
    public bool IsCompleted => isCompleted;

    void OnValidate() => this.isCompleted = false;
    void OnEnable() => this.isCompleted = false;
    void OnDisable() => ResetState();
    void OnDestroy() => ResetState();

    void ResetState()
    {
        this.isCompleted = false;
        this.OnActionCompleted = null;
    }
    /// <summary>
    /// Initializes the action with the given entity context.
    /// Call on the Start of the action execution.
    /// </summary>
    /// <param name="ctx"></param>
    public abstract void Initialize(EntityContext ctx);

    /// <summary>
    /// It either ends or aborts the action.
    /// Call on the End or Abort of the action execution.
    /// Combining both End and Abort into a single method for simplicity.
    /// since both scenarios may require similar cleanup logic.
    /// for default, just resetting isCompleted to false.
    /// </summary>
    /// <param name="ctx"></param>
    public virtual void EndOrAbort(EntityContext ctx)
    {
        this.isCompleted = false;
        OnActionCompleted = null; // Clear event subscribers
    }

    public float Evaluate(EntityContext context)
    {
        float totalScore = 1f;
        foreach (var consideration in considerations)
        {
            float score = consideration.Evaluate(context);
            totalScore *= score;
        }

        return totalScore;
    }
    /// <summary>
    /// Executes the action logic as a coroutine.
    /// Call on every frame during the action execution.
    /// Coroutine should yield return null until the action is completed.
    /// Once the action is finished, it should call MarkCompleted() to indicate completion.
    /// Coroutine is managed by AiExecutor. since it handles the execution flow.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public abstract IEnumerator Execute(EntityContext context);





    /// <summary>
    /// Marks the action as completed.
    /// Call this method within the action's execution logic
    /// when the action has finished its intended behavior.
    /// </summary>
    public void MarkCompleted()
    {
        this.isCompleted = true;
        OnActionCompleted?.Invoke();
    }

}
