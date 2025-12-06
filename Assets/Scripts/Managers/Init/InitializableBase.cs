using UnityEngine;

/// <summary>
/// Convenience base class for MonoBehaviours that participate in InitManager lifecycle.
/// Derive from this so components automatically implement IInitializable.
/// </summary>
public abstract class InitializableBase : MonoBehaviour, IInitializable
{
    /// <summary>
    /// Called once after dependencies are injected. Override or use method-injection.
    /// </summary>
    public virtual void Init() { }

    /// <summary>
    /// Called during shutdown. Override for cleanup.
    /// </summary>
    public virtual void Shutdown() { }
}
