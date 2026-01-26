using System;
using System.Collections;
using UnityEngine;

namespace Kope.AI
{
    /// <summary>
    /// Base action type for any AI system.
    /// Defines the minimal interface for execution and completion.
    /// </summary>
    public abstract class BaseActionSO : ScriptableObject
    {
        [SerializeField] protected string actionName = "Base Action";
        [SerializeField] protected bool isInterruptible = true;
        private bool isCompleted = false;

        public string ActionName => actionName;
        public bool IsInterruptible => isInterruptible;
        public bool IsCompleted => isCompleted;

        public event Action OnActionCompleted;

        #region Unity Callbacks
        void OnValidate() => ResetState();
        void OnEnable() => ResetState();
        void OnDisable() => ResetState();
        void OnDestroy() => ResetState();

        protected void ResetState()
        {
            isCompleted = false;
            OnActionCompleted = null;
        }
        #endregion

        /// <summary>
        /// Initialize the action with the mutable context.
        /// </summary>
        public abstract void Initialize(EntityContext ctx);

        /// <summary>
        /// End or abort the action.
        /// </summary>
        public virtual void EndOrAbort(EntityContext ctx)
        {
            isCompleted = false;
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
            isCompleted = true;
            OnActionCompleted?.Invoke();
        }
    }
}
