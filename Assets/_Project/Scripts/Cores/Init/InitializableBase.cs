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

        // this bool just means whether Init() has been called or not yet for the instance
        // it does not guarantee that all dependencies are injected or valid
        // that is up to the derived class to ensure during its Init() implementation
        public bool IsInitialized { get; protected set; } = false;

        /// <summary>
        /// Sets the IsInitialized boolean value.
        /// Default is true, set to false to mark uninitialized.
        /// Use with caution; prefer calling Init()/Shutdown() instead.
        /// </summary>
        /// <param name="value"></param>
        public void SetInitBoolean(bool value = true) => this.IsInitialized = value;

        /// <summary>
        /// Called during initialization. Override for setup.
        /// Always call base.Init() to set IsInitialized = true.
        /// at the top of your override. since other components may check IsInitialized in their Init().
        /// </summary>
        public virtual void Init()
        {
            this.IsInitialized = true;
        }

        /// <summary>
        /// Called during shutdown. Override for teardown.
        /// Always call base.Shutdown() to set IsInitialized = false.
        /// Completely optional to override.
        /// </summary>
        public virtual void Shutdown()
        {
            this.IsInitialized = false;
        }



        /// <summary>
        /// Update method called every frame by UnityLifecycleManager.
        /// If you override, always call base.Update() to get the warning
        /// And also add if (!this.IsInitialized) return; to avoid processing when not initialized.
        /// </summary>
        protected virtual void Update()
        {
            if (this.IsInitialized) return;
            string referenceStack = "";
            FindAllParentStackString(this.transform.parent, ref referenceStack);
            if (referenceStack == "")
                referenceStack = "(root)";
            else
                referenceStack = "(root) -> " + referenceStack + " -> " + this.name;

            Debug.LogWarning($"Component {this.name} is not initialized on stack {referenceStack} but is receiving Update calls. Please place the component on the InitLifecycleManager Init() call order correctly.");


        }
        private void FindAllParentStackString(Transform currentTransform, ref string stackString)
        {
            if (currentTransform == null) return;
            stackString = currentTransform.name + (stackString == "" ? "" : " -> " + stackString);
            FindAllParentStackString(currentTransform.parent, ref stackString);
        }
    }
}