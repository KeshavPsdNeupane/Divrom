using System;
using System.Collections;
using UnityEngine;

namespace Kope.AI
{

    public enum ExecutionActionStatus
    {
        NotInitialized,
        Success,
        Failure,
        Running
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
        void OnValidate() => ResetState();
        void OnEnable() => ResetState();
        void OnDisable() => ResetState();
        void OnDestroy() => ResetState();

        protected void ResetState()
        {
            actionStatus = ExecutionActionStatus.NotInitialized;
            OnActionCompleted = null;
        }
        #endregion

        /// <summary>
        /// Initialize the action with the mutable context.
        /// </summary>
        public virtual void Initialize(EntityContext ctx)
        {
            this.actionStatus = ExecutionActionStatus.Running;
        }

        /// <summary>
        /// End or abort the action.
        /// ALways overright this method when needed.
        /// First clean up any state related to the action,
        /// do validations, then call base.EndOrAbort(ctx) to reset status.
        /// </summary>
        public virtual void EndOrAbort(EntityContext ctx)
        {
            actionStatus = ExecutionActionStatus.NotInitialized;
            OnActionCompleted = null;
        }

        /// <summary>
        /// Execute action logic.
        /// </summary>
        public abstract IEnumerator Execute(EntityContext ctx);

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
