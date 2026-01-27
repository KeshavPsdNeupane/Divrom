using System.Collections.Generic;
using System;
using UnityEngine;
public sealed class EntityContext : IReadOnlyEntityContext
{
    private readonly Transform entityTransform;
    private readonly Dictionary<Type, object> components = new();
    private readonly EntityStates states;
    private readonly EntityStateManager stateMachine;

    private readonly List<string> excludedTypes = new()
    {
        "Kope.Core.Init.InitializableBase",
        "UnityEngine.MonoBehaviour",
        "UnityEngine.Behaviour",
        "UnityEngine.Component",
        "UnityEngine.ScriptableObject"
    };
    // Need to add the  NavMeshAgent2D later when i have implemented it using my quaternary Heap for A* pathfinding
    // since there is no NavMeshAgent2D in Unity by default

    // Need to make Sensors/Perception system later to add the runtime context of the entity like visible enemies, items etc.
    public Transform EntityTransform => this.entityTransform;
    public EntityStateManager StateMachine => this.stateMachine;
    public EntityStates States => this.states;

    public EntityContext(Transform entityTransform, EntityStateManager stateMachine, EntityStates states, List<string> excludedTypes = null)
    {
        this.entityTransform = entityTransform;
        this.stateMachine = stateMachine;
        this.states = states;
        if (excludedTypes != null)
        {
            this.excludedTypes.AddRange(excludedTypes);
        }
    }

    /// <summary>
    /// Registers a component in the EntityContext for later retrieval.
    /// Components are registered under their concrete type, all base types (excluding framework types),
    /// and implemented interfaces, allowing lookups by any of these types via TryGetComponent.
    /// Example: EnemyMovementComponent can be retrieved as MovementComponentBase.
    /// </summary>
    /// <typeparam name="Tcomponent">The type of the component being added.</typeparam>
    /// <param name="component">The component instance to register.</param>
    public void AddComponent<Tcomponent>(Tcomponent component)
    {
        if (component == null)
        {
            Debug.LogError("Cannot add a null component to the EntityContext.");
            return;
        }

        void Register(Type type)
        {
            if (this.components.ContainsKey(type)) return;
            this.components[type] = component;
        }

        bool ShouldStop(Type type)
        {
            return this.excludedTypes.Contains(type.FullName);
        }

        var concreteType = component.GetType();
        Register(concreteType);

        // Register all base types (stop at very base framework types) so TryGetComponent can find by base class
        var baseType = concreteType.BaseType;
        while (baseType != null && baseType != typeof(object) && !ShouldStop(baseType))
        {
            Register(baseType);
            baseType = baseType.BaseType;
        }

        // Register implemented interfaces for interface-based lookups
        foreach (var iface in concreteType.GetInterfaces())
        {
            if (!ShouldStop(iface))
            {
                Register(iface);
            }
        }
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
