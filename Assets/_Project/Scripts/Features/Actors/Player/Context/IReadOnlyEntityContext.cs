using UnityEngine;

public interface IReadOnlyEntityContext
{
    Transform EntityTransform { get; }
    EntityStateManager StateMachine { get; }
    EntityStates States { get; }

    bool TryGetComponent<T>(out T component);
}
