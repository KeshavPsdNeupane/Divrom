public interface IReadOnlyEntityContext
{
    EntityStateManager StateMachine { get; }
    EntityStates States { get; }

    bool TryGetComponent<T>(out T component);
}
