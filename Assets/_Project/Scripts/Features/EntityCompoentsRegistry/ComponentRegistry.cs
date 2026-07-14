using System.Collections.Generic;
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using System.Collections.ObjectModel;

namespace Kope.Core.EntityComponentRegistry {
	/// <summary>
	/// This class takes any class (either a MonoBehaviour, pure C# class, interface, 
	/// or custom component bases) and registers it as a component. 
	/// The caller must enforce type safety by registering components of expected types 
	/// and checking for those types when retrieving. <br/>
	/// Stores the context of a single entity. <br/>
	/// <inheritdoc cref="IReadOnlyComponentRegistry"/>
	/// </summary>
	[Serializable]
	public class ComponentRegistry : IReadOnlyComponentRegistry {
		private readonly string registryName;

		/// <summary>
		/// Indicates whether this EntityContext contains behavioral components like state machines, AI, sensors, etc.
		/// Used to differentiate between entities that have state machines and those that do not, 
		/// avoiding null-checks on state machine references and allowing reuse of contexts for static/non-behavioral entities.
		/// </summary>
		private readonly bool hasBehavioralComponents = false;
		private readonly AxisMode dimension = AxisMode.TwoD;
		private readonly Transform entityTransform;
		private readonly Dictionary<System.Type, object> components = new();

		/// <summary>
		/// Global hard limits where reflection must completely halt while climbing the base-type inheritance chain.
		/// Keeping these framework base classes out of the registry prevents clutter and unexpected lookup collisions.
		/// </summary>
		private static readonly HashSet<System.Type> fullStopType = new() {
			typeof(MonoBehaviour),
			typeof(Component),
			typeof(Behaviour),
			typeof(ScriptableObject),
			typeof(UnityEngine.Object)
		};

		/// <summary>
		/// Returns true if the type is a core framework type where inheritance-climbing must stop.
		/// </summary>
		private static bool ShouldStop(System.Type type) {
			return fullStopType.Contains(type);
		}

		/// <summary>
		/// Dynamic, instance-specific list of types to exclude from registration.
		/// Pass these through the constructor to prevent registration under specific interfaces or base classes.
		/// </summary>
		private readonly HashSet<System.Type> _excludeType = new();

		// <inheritdoc/>
		public Transform EntityTransform => this.entityTransform;
		public string RegistryName => this.registryName;
		public AxisMode Dimension => this.dimension;
		public Dictionary<System.Type, object> Components => this.components;

		/// <summary>
		/// Indicates whether this EntityContext contains a state machine context.
		/// </summary>
		public bool HasBehavioralComponents => hasBehavioralComponents;

		/// <summary>
		/// Initializes a new instance of the ComponentRegistry.
		/// Provide types in <paramref name="excludedTypes"/> to prevent specific base types or interfaces from registering.
		/// </summary>
		public ComponentRegistry(
			AxisMode dimension,
			string registryName,
			Transform entityTransform,
			bool hasBehavioralComponents,
			HashSet<System.Type> excludedTypes = null
		) {
			this.dimension = dimension;
			this.registryName = registryName;
			this.entityTransform = entityTransform;
			this.hasBehavioralComponents = hasBehavioralComponents;

			if (excludedTypes != null) {
				this._excludeType.UnionWith(excludedTypes);
			}
		}

		/// <summary>
		/// Registers a component in the registry for later retrieval.
		/// <para>
		/// The component instance is registered under its concrete type, its parent classes (up until 
		/// <see cref="System.Object"/> or any <see cref="fullStopType"/> framework bases), and all implemented interfaces.
		/// </para>
		/// </summary>
		/// <typeparam name="Tcomponent">The type of the component being added.</typeparam>
		/// <param name="component">The component instance to register.</param>
		public void Register<Tcomponent>(Tcomponent component) {
			if (component == null) {
				Debug.LogError("[ComponentRegistry] Cannot add a null component to the registry.");
				return;
			}

			void RegisterType(System.Type type) {
				if (this.components.ContainsKey(type)) return;
				this.components[type] = component;
			}

			bool ShouldExclude(System.Type type) {
				return this._excludeType.Contains(type);
			}

			var concreteType = component.GetType();

			// 1. Register the concrete type itself (as long as it isn't explicitly excluded)
			if (!ShouldExclude(concreteType)) {
				RegisterType(concreteType);
			}

			// 2. Register all valid base classes up the inheritance chain
			var baseType = concreteType.BaseType;
			while (baseType != null && baseType != typeof(object)) {
				// Completely stop climbing if we hit a framework-level stop type (MonoBehaviour, Component, etc.)
				if (ShouldStop(baseType)) {
					break;
				}

				// If not explicitly excluded by the registry configuration, register this base class
				if (!ShouldExclude(baseType)) {
					RegisterType(baseType);
				}

				baseType = baseType.BaseType;
			}

			// 3. Register all implemented interfaces, ignoring any in the exclusion list
			foreach (var iface in concreteType.GetInterfaces()) {
				if (!ShouldExclude(iface)) {
					RegisterType(iface);
				}
			}
		}

		/// <summary>
		/// Attempts to retrieve a component by its registered type. 
		/// Recommended for cross-entity requests to preserve clean boundaries.
		/// </summary>
		public bool TryGetReadOnly<Tcomponent>([MaybeNullWhen(false)] out Tcomponent component) {
			var type = typeof(Tcomponent);
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