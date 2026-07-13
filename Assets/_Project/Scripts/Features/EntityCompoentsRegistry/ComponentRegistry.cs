using System.Collections.Generic;
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Kope.Core.EntityComponentRegistry {
	/// <summary>
	/// This class takes any class(either it is mono, c# class or interface or the InitializableBase itself) 
	/// as a component. but the caller must inforce the type safety by only registering components
	/// of the expected types and checking for those types when retrieving.
	/// Stores the context of a single entity. <br/>
	/// <inheritdoc cref="IReadOnlyComponentRegistry"/>
	/// </summary>
	[Serializable]
	public class ComponentRegistry : IReadOnlyComponentRegistry {
		private readonly string registryName;
		/// <summary>
		/// Indicate whether this EntityContext contains Behavioral components like state machines, AI, sensors etc.
		/// this is used to differentiate between entities that have state machines and those that do not.
		/// so we dont have to check for null state machine references everywhere.
		/// and we can reuse this context for entities without state machines.
		/// like static objects, pickups, decorations etc.
		/// </summary>
		private readonly AxisMode dimension = AxisMode.TwoD;
		private readonly bool hasBehavioralComponents = false;
		private readonly Transform entityTransform;
		private readonly Dictionary<System.Type, object> components = new();

		private readonly HashSet<System.Type> excludedTypes = new()
	{
		typeof(MonoBehaviour),
		typeof(Behaviour),
		typeof(Component),
		typeof(ScriptableObject)
	};

		//<inheritdoc/>
		public Transform EntityTransform => this.entityTransform;
		public string RegistryName => this.registryName;
		public AxisMode Dimension => this.dimension;

		public Dictionary<System.Type, object> Components => this.components;

		/// <summary>
		/// Indicates whether this EntityContext contains a state machine context.
		/// this is used to differentiate between entities that have state machines and those that do not.
		/// so we dont have to check for null state machine references everywhere.
		/// and we can reuse this context for entities without state machines.
		/// like static objects, pickups, decorations etc.
		/// </summary>
		public bool HasBehavioralComponents => hasBehavioralComponents;

		/// <summary>
		/// Initializes a new instance of the EntityContext class without state machine.
		/// Provide types in excludedTypes to prevent those types from being registered.
		/// Since reflection is used to register base types and interfaces, excluding framework types like MonoBehaviour and Component
		/// prevents unnecessary registrations and potential conflicts.
		/// </summary>
		/// <param name="entityTransform"></param>
		/// <param name="excludedTypes"></param>
		public ComponentRegistry(AxisMode dimension, string registryName, Transform entityTransform, bool hasBehavioralComponents, HashSet<System.Type> excludedTypes = null) {
			this.dimension = dimension;
			this.registryName = registryName;
			this.entityTransform = entityTransform;
			this.hasBehavioralComponents = hasBehavioralComponents;
			if (excludedTypes != null) {
				this.excludedTypes.UnionWith(excludedTypes);
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
		public void Register<Tcomponent>(Tcomponent component) {
			if (component == null) {
				Debug.LogError("Cannot add a null component to the EntityContext.");
				return;
			}

			void Register(System.Type type) {
				if (this.components.ContainsKey(type)) return;
				this.components[type] = component;
			}

			bool ShouldStop(System.Type type) {
				return this.excludedTypes.Contains(type);
			}

			var concreteType = component.GetType();
			Register(concreteType);

			// Register all base types up to but not including System.Object, excluding 
			// prohibited types like MonoBehaviour, Component, and ScriptableObject
			// and the types provided on the constructor's excludedTypes parameter.
			var baseType = concreteType.BaseType;
			while (baseType != null && baseType != typeof(object) && !ShouldStop(baseType)) {
				Register(baseType);
				baseType = baseType.BaseType;
			}

			// Register all implemented interfaces, excluding prohibited types and those 
			// provided on the constructor's excludedTypes parameter.
			foreach (var iface in concreteType.GetInterfaces()) {
				if (!ShouldStop(iface)) {
					Register(iface);
				}
			}

		}

		public bool TryGetReadOnly<Tcomponent>([MaybeNullWhen(false)] out Tcomponent component) {
			var type = typeof(Tcomponent);
			// Try to get the component by type
			if (components.TryGetValue(type, out var comp) && comp is Tcomponent typedComp) {
				component = typedComp;
				return true;
			}
			component = default;
			return false;
		}
		/// <summary>
		/// Convenience method for TryGetComponent when you want to get a mutable reference to the component.
		/// Since the components are stored as objects, TryGetComponent already returns a reference to 
		/// the component instance, so this method simply calls TryGetComponent and allows the caller to get a mutable
		/// But TryGetReadOnly is a contract that defines that out component must be used as ReadOnly, 
		/// so this method is just a semantic convenience to indicate that the caller intends to mutate the component,
		///  and it can be used to differentiate between read-only and mutable access in the codebase.
		/// Use this method only on the Same Entity's components, since getting a mutable reference to a component of another
		///  entity can lead to unintended side effects and break the encapsulation of the Entity's internal state. 
		/// Always prefer using TryGetReadOnly for cross-entity access to ensure that you are treating
		/// the component as read-only and respecting the boundaries of each Entity's context.
		/// </summary>
		/// <typeparam name="Tcomponent"></typeparam>
		/// <param name="component"></param>
		/// <returns></returns>
		public bool TryGetMutable<Tcomponent>([MaybeNullWhen(false)] out Tcomponent component) where Tcomponent : class {
			return TryGetReadOnly(out component);
		}

	}
}