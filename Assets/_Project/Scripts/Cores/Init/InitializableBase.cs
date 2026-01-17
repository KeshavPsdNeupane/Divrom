using UnityEngine;
namespace Kope.Core.Init
{
    /// <summary>
    /// Convenience base class for MonoBehaviours that participate in InitManager lifecycle.
    /// Derive from this so components automatically implement IInitializable.
    /// Make sure your are placing the Init() call in the correct order in InitLifecycleManager.
    /// </summary>
    public abstract class InitializableBase : MonoBehaviour, IInitializable
    {
        public bool IsInitialized { get; protected set; } = false;

        protected void SetInitialized(bool initialized = true)
        => this.IsInitialized = initialized;

        /// <summary>
        /// Called once after dependencies are injected. Override or use method-injection.
        /// </summary>
        public virtual void Init() { }

        /// <summary>
        /// Called during shutdown. Override for cleanup.
        /// </summary>
        public virtual void Shutdown() { }
    }
}