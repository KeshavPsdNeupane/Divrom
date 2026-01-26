using System.Collections.Generic;
using System;
public sealed class EntityContext : IReadOnlyEntityContext
{

    private readonly Dictionary<Type, object> components = new();
    private readonly EntityStates states;
    private readonly EntityStateManager stateMachine;

    public EntityStateManager StateMachine => this.stateMachine;
    public EntityStates States => this.states;

    public EntityContext(EntityStateManager stateMachine, EntityStates states)
    {
        this.stateMachine = stateMachine;
        this.states = states;
    }

    public void AddComponent<Tcomponent>(Tcomponent component)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component), "Component cannot be null.");
        var type = typeof(Tcomponent);
        if (this.components.ContainsKey(type))
            throw new ArgumentException($"Component of type {type.Name} already exists in the context.");
        this.components[type] = component;
    }

    public bool TryGetComponent<Tcomponent>(out Tcomponent component)
    {
        if (this.components.TryGetValue(typeof(Tcomponent), out var comp) && comp is Tcomponent typedComp)
        {
            component = typedComp;
            return true;
        }
        component = default;
        return false;
    }

}
