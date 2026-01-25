using System.Collections.Generic;

public sealed class EntityContext : IReadOnlyEntityContext
{

    private readonly Dictionary<string, object> components = new();

    private readonly Dictionary<string, object> data = new();

    private readonly EntityStates states;
    private readonly EntityStateManager stateMachine;

    public EntityStateManager StateMachine => this.stateMachine;
    public EntityStates States => this.states;

    public EntityContext(EntityStateManager stateMachine, EntityStates states)
    {
        this.stateMachine = stateMachine;
        this.states = states;
    }

    public void AddComponent<Tcomponent>(string key, Tcomponent component) => this.components[key] = component;

    public bool TryGetComponent<Tcomponent>(string key, out Tcomponent component)
    {
        if (this.components.TryGetValue(key, out var comp) && comp is Tcomponent typedComp)
        {
            component = typedComp;
            return true;
        }
        component = default;
        return false;
    }

    public void AddData<Tdata>(string key, Tdata value) => this.data[key] = value;
    public bool TryGetData<Tdata>(string key, out Tdata value)
    {
        if (this.data.TryGetValue(key, out var dataValue) && dataValue is Tdata typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

}
