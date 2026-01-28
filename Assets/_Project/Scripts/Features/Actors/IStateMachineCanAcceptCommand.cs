

/// <summary>
/// Interface to indicate that a state machine can accept commands.
/// Must be implemented by any state machine that needs to check if it can accept commands.
/// So it can block the AI or player commands when needed.
/// </summary>
public interface IStateCanAcceptCommand
{
    /// <summary>
    /// Returns true if the state machine can accept commands.
    /// There is no default implementation. a child class must implement this.
    /// So that each state machine can define its own logic. And
    /// There wont be no assumptions about the default behavior.
    /// </summary>
    /// <returns></returns>
    bool CanAcceptCommand { get; }
}
