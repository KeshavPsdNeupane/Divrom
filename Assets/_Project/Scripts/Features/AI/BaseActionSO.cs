using System;
using UnityEngine;
using Kope.Core.EntityComponentSystem;

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
        public void Initialize(EntityComponentRegistry ctx)
        {
            this.actionStatus = ExecutionActionStatus.Running;
            OnInilialize(ctx);
        }
        protected abstract void OnInilialize(EntityComponentRegistry ctx);
        /// <summary>
        /// End or abort the action.
        /// Always override this method when needed.
        /// First clean up any state related to the action,
        /// do validations, then call base.EndOrAbort(ctx) to reset status.
        /// </summary>
        public void EndOrAbort(EntityComponentRegistry ctx)
        {
            OnEndOrAbort(ctx);
            actionStatus = ExecutionActionStatus.NotInitialized;
            OnActionCompleted = null;
        }
        protected abstract void OnEndOrAbort(EntityComponentRegistry ctx);

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
        public abstract void TickUpdate(Context ctx);

        /// <summary>
        /// Execute the action logic during the Physics (FixedUpdate) cycle.
        /// Should be implemented by derived classes for movement, force application, or any Rigidbody manipulation.
        /// This ensures movement behavior is consistent with the physics engine's timing, preventing jitter.
        /// 
        /// Need full context for execution since actions may need to read other target contexts.
        /// Even though the context is passed as mutable, any action can mutate only its own entity context.
        /// 
        /// It is recommended to use the ReadOnlyEntityContext property and use the function
        /// TryGetReadOnlyTargetContext of IReadOnlyContext to get "Read-Only" access to target contexts.
        /// This enforces the read-only contract of target contexts to prevent inconsistent states
        /// and preserve the assumptions made by AI algorithms and other actions.
        /// 
        /// SO PLEASE BE WARNED: DO NOT MUTATE TARGET CONTEXTS DIRECTLY IN TICKFIXEDUPDATE UNLESS EXTREMELY NECESSARY.
        /// THIS IS A LAST RESORT. INSTEAD, MUTATE ONLY THE CURRENT ENTITY'S CONTEXT.
        /// </summary>
        /// <param name="ctx">The AI context containing current and target entity data.</param>
        public abstract void TickFixedUpdate(Context ctx);



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
