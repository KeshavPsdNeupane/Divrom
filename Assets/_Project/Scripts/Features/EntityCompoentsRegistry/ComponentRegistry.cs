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
		private readonly bool hasBehavioralComponents = false;
		private readonly Transform entityTransform;
		private readonly Dictionary<Type, object> components = new();

		private readonly HashSet<Type> excludedTypes = new()
	{
		typeof(MonoBehaviour),
		typeof(Behaviour),
		typeof(Component),
		typeof(ScriptableObject)
	};

		//<inheritdoc/>
		public Transform EntityTransform => this.entityTransform;
		public string RegistryName => this.registryName;

		public Dictionary<Type, object> Components => this.components;

		/// <summary>
		/// Indicates whether this EntityContext contains a state machine context.
		/// this is used to differentiate between entities that have state machines and those that do not.
		/// so we dont have to check for null state machine references everywhere.
		/// and we can reuse this context for entities without state machines.
		/// like static objects, pickups, decorations etc.
		/// </summary>
		public bool HasBehavioralComponents => hasBehavioralComponents;

		/// <summary>
		/// Initializes a new instance of the EntityContext class.
		/// Provide string type names in excludedTypes to prevent those types from being registered.
		/// Since reflection is used to register base types and interfaces, excluding framework types like MonoBehaviour and Component
		/// prevents unnecessary registrations and potential conflicts.
		/// Using string as type names avoids direct dependencies on those types in this class.
		/// which is useful if those types are in different assemblies or packages.
		/// </summary>
		/// <param name="entityTransform"></param>
		/// <param name="hasBehavioralComponents"></param>
		/// <param name="excludedTypes"></param>
		[Obsolete("This constructor is deprecated. Use the constructor that accepts a " +
		"HashSet<Type> for excludedTypes instead for better performance and type safety." +
		"Provide the hype using the the config asset and the inspector field for excluded types instead of hardcoding them in code.")]
		public ComponentRegistry(string registryName, Transform entityTransform, bool hasBehavioralComponents, List<string> excludedTypes = null) {
			this.registryName = registryName;
			this.entityTransform = entityTransform;
			this.hasBehavioralComponents = hasBehavioralComponents;
			if (excludedTypes != null) {
				foreach (var typeName in excludedTypes) {
					Type type = Type.GetType(typeName);
					if (type != null) {
						this.excludedTypes.Add(type);
					} else {
						Debug.LogWarning($"Excluded type '{typeName}' could not be found and will be ignored.");
					}
				}
			}
		}
		/// <summary>
		/// Initializes a new instance of the EntityContext class without state machine.
		/// Provide types in excludedTypes to prevent those types from being registered.
		/// Since reflection is used to register base types and interfaces, excluding framework types like MonoBehaviour and Component
		/// prevents unnecessary registrations and potential conflicts.
		/// </summary>
		/// <param name="entityTransform"></param>
		/// <param name="excludedTypes"></param>
		public ComponentRegistry(string registryName, Transform entityTransform, bool hasBehavioralComponents, HashSet<Type> excludedTypes = null) {
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

			void Register(Type type) {
				if (this.components.ContainsKey(type)) return;
				this.components[type] = component;
			}

			bool ShouldStop(Type type) {
				return this.excludedTypes.Contains(type);
			}

			var concreteType = component.GetType();
			Register(concreteType);

			// Register all base types (stop at very base framework types) so TryGetComponent can find by base class
			var baseType = concreteType.BaseType;
			while (baseType != null && baseType != typeof(object) && !ShouldStop(baseType)) {
				Register(baseType);
				baseType = baseType.BaseType;
			}

			// Register implemented interfaces for interface-based lookups
			foreach (var iface in concreteType.GetInterfaces()) {
				if (!ShouldStop(iface)) {
					Register(iface);
				}
			}

		}

		public bool TryGetReadOnlyComponent<Tcomponent>([MaybeNullWhen(false)] out Tcomponent component, bool logInternalWarning = true) {
			var type = typeof(Tcomponent);
			// Try to get the component by type
			if (components.TryGetValue(type, out var comp) && comp is Tcomponent typedComp) {
				component = typedComp;
				return true;
			}

			if (logInternalWarning) {
				Debug.LogWarning(
					$"[ECS Warning] Component of type {type.Name} requested but not yet registered. Error Found in {entityTransform.name}.\n" +
					"Possible reasons:\n" +
					"1. Circular dependency(#skillIssue) or incorrect Init order.\n" +
					"2. Component does not exist on this entity.\n" +
					"3. Accessing a component on a target entity which doesn't have the component.\n" +
					"4. The component was excluded from registration intentionally."
				);
			}

			component = default;
			return false;
		}
		/// <summary>
		/// Convenience method for TryGetComponent when you want to get a mutable reference to the component.
		/// Since the components are stored as objects, TryGetComponent already returns a reference to 
		/// the component instance, so this method simply calls TryGetComponent and allows the caller to get a mutable
		/// But TryGetComponent is a contract that defines that out component must be used as ReadOnly, 
		/// so this method is just a semantic convenience to indicate that the caller intends to mutate the component,
		///  and it can be used to differentiate between read-only and mutable access in the codebase.
		/// Use this method only on the Same Entity's components, since getting a mutable reference to a component of another
		///  entity can lead to unintended side effects and break the encapsulation of the Entity's internal state. 
		/// Always prefer using TryGetComponent for cross-entity access to ensure that you are treating
		/// the component as read-only and respecting the boundaries of each Entity's context.
		/// </summary>
		/// <typeparam name="Tcomponent"></typeparam>
		/// <param name="component"></param>
		/// <returns></returns>
		public bool TryGetMutatableComponent<Tcomponent>([MaybeNullWhen(false)] out Tcomponent component, bool logWarning = true) where Tcomponent : class {
			return TryGetReadOnlyComponent(out component, logWarning);
		}

	}
}