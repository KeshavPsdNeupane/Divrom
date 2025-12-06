public interface IInitializable
{
    /// <summary>
    /// Called once after dependencies are injected.
    /// </summary>
    void Init();

    /// <summary>
    /// Called when the object is being destroyed, for cleanup.
    /// </summary>
    void Shutdown();
}
