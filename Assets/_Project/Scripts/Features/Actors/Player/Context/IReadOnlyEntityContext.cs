public interface IReadOnlyEntityContext
{
    EntityStateManager StateMachine { get; }
    EntityStates States { get; }

    bool TryGetComponent<T>(string key, out T component);
    bool TryGetData<T>(string key, out T value);
}
