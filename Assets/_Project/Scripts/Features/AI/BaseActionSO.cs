using System;
using System.Collections;
using UnityEngine;
using Kope.Core.EntityComponentSystem;
using Unity.VisualScripting;

namespace Kope.AI
{
    public enum ExecutionActionStatus : short
    {
        NotInitialized = 0,
        Running = 10,
        Success = 20,
        Failure = 99
    }

    /// <summary>
    /// Base action type for any AI system.
    /// Defines the minimal interface for execution and completion.
    /// </summary>
    public abstract class BaseActionSO : ScriptableObject
    {
        [SerializeField] protected string actionName = "Base Action";
        [SerializeField] protected bool isInterruptible = true;
        protected ExecutionActionStatus actionStatus = ExecutionActionStatus.NotInitialized;

        public string ActionName => actionName;
        public bool IsInterruptible => isInterruptible;
        public bool IsCompleted => actionStatus != ExecutionActionStatus.Running && actionStatus != ExecutionActionStatus.NotInitialized;

        public event Action OnActionCompleted;

        #region Unity Callbacks
#if UNITY_EDITOR
        protected void OnValidate() => ResetState();
#endif
        void OnEnable() => ResetState();

        protected void ResetState()
        {
            actionStatus = ExecutionActionStatus.NotInitialized;
            OnActionCompleted = null;
        }
        #endregion

        /// <summary>
        /// Initialize the action with the mutable context.
        /// Pass the current entity's context for initialization.
        /// Always override this method when needed.
        /// First set up any state related to the action,
        /// do validations, then call base.Initialize(ctx) to set status to Running.<br/>
        /// Recommended:<br/>
        /// Call this on the planner init call so we can cache any needed components. only once.
        /// for current entity. since all the components are reference types. so no need to do this per action execution.
        /// </summary>
        public virtual void Initialize(EntityComponentRegistry ctx)
        {
            this.actionStatus = ExecutionActionStatus.Running;
        }

        /// <summary>
        /// End or abort the action.
        /// Always override this method when needed.
        /// First clean up any state related to the action,
        /// do validations, then call base.EndOrAbort(ctx) to reset status.
        /// </summary>
        public virtual void EndOrAbort(EntityComponentRegistry ctx)
        {
            actionStatus = ExecutionActionStatus.NotInitialized;
            OnActionCompleted = null;
        }

        /// <summary>
        /// Execute the action with the given context.
        /// Must be implemented by derived classes.
        /// Need full context for execution since actions may need to read other target contexts.
        /// Even though the context is passed as mutable,  
        /// any action can mutate only its own entity context.
        /// But it is recommended to use ReadOnlyEntityContext property and use function
        /// TryGetReadOnlyTargetContext of IReadOnlyContext to get "Read-Only(can be modified due to reference nature but anyway)" access to target contexts.
        /// This is to enforce the read-only contract of target contexts in the execute
        /// function of class to hint that the target contexts should be treated as read-only.
        /// since mutating target contexts directly may lead to inconsistent states.
        /// And may break the assumptions made by AI algorithms and actions.
        /// SO PLEASE BE WARNED DO NOT MUTATE TARGET CONTEXTS DIRECTLY IN EXECUTE METHOD UNLESS IT IS EXTREMELY NECESSARY.
        /// AND LAST RESORT. INSTEAD, MUTATE ONLY THE CURRENT ENTITY'S CONTEXT
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public abstract IEnumerator Execute(Context ctx);

        /// <summary>
        /// Mark the action as completed.
        /// </summary>
        public void MarkCompleted()
        {
            actionStatus = ExecutionActionStatus.Success;
            OnActionCompleted?.Invoke();
        }
    }
}
