using System.Collections.Generic;
using Kope.Core.LifeTimeManagement;
using Kope.Core.ServiceLocator;
using UnityEngine;

using System.Text;

namespace Kope.Core.EntityComponentRegistry {
	/// <summary>
	/// This class takes only InitializableBaseNew as component in inpector but we can,
	/// bypass the InitializableBaseNew requirement and register any component we want 
	/// in the ComponentRegistry during runtime just like the SaveSystem does with the EntitySaveSystem component.
	/// Since for that Save system we dont want to force the user to add an InitializableBaseNew component just for saving/loading purposes, we can just register the EntitySaveSystem directly in the registry during runtime and it will work just fine.
	/// <br/>
	/// Stores a collection of components associated with an entity and initializes them.
	/// Components registered here do **not** need to be initialized elsewhere; 
	/// providing this class to the InitManager handles their initialization automatically.
	/// Using this class is optional — components can still be initialized individually in 
	/// InitManager if desired. 
	/// However, using EntityComponentStore is recommended for better organization and management of entity components.
	/// Centralizes initialization logic and avoids duplicate registration/Init calls.
	/// <br/>
	/// <inheritdoc cref="InitializableBase"/>
	/// </summary>
	public class EntityComponentsRegistry : InitializableBase, IInitializableContainer {
		[SerializeField] private Transform entityTransform;
		[SerializeField] private string registryName = "DefaultRegistryName";
		[SerializeField, Tooltip("Indicates whether this EntityComponentStore contains state/AI/sensor components." +
		"So that the EntityComponentRegistry can optimize its registrations accordingly. and other systems can query this info easily.")]
		private bool hasBehavioralComponents = false;
		[SerializeField] private EntityComponentRegistryConfig config;
		/// <summary>
		/// The list of components stored in this EntityComponentStore.
		/// </summary>
		[SerializeField, Tooltip("Order matters! \n\nIf you can't avoid circular dependencies (#skillIssue), refactor your life choices.")]
		private List<InitializableBase> components = new();
		private ComponentRegistry componentRegistry;



		/// <summary>
		/// Runtime registry of this EntityComponentStore.
		/// </summary>
		public ComponentRegistry ComponentRegistry => componentRegistry;

		public IEnumerable<InitializableBase> GetNestedComponents() {
			return this.components;
		}

		protected override bool OnInit() {

			var dimension = GlobalServiceLocator.Dimension;
			this.componentRegistry = new ComponentRegistry(
				dimension,
				this.registryName,
				this.entityTransform,
				this.hasBehavioralComponents,
				this.config.ExcludedTypeSet
			);

			// First register all components
			foreach (var c in components) {
				if (c != null) {
					if (this == c) {
						Debug.LogError($"EntityComponentStore {this.name} cannot register itself as a component. " +
						"Remove it from the components list.");
						continue;
					}
					componentRegistry.Register(c);
				}
			}
			return true;
		}





		/// <summary>
		/// Formats a detailed, pre-allocated error message for failed component fetch operations.
		/// </summary>
		/// <typeparam name="TCaller">The Unity Object type executing the fetch request.</typeparam>
		/// <typeparam name="TComponent">The type of the component or interface being requested.</typeparam>
		/// <param name="caller">The active instance attempting the fetch, used to extract context names.</param>
		/// <param name="hierarchyPath">The scene hierarchy path tracking the target entity's location.</param>
		/// <param name="isReadOnly">Determines if additional contextual failure reasons for read-only lookups should be appended.</param>
		/// <returns>A fully structured, scannable error string.</returns>
		[HideInCallstack]
		public string FormattedMessage<TCaller, TComponent>(TCaller caller, string hierarchyPath, bool isReadOnly = true)
				where TCaller : UnityEngine.Object
				where TComponent : class {
			string target = typeof(TComponent).Name;

			// Pre-allocate space to avoid internal string allocations on the heap
			StringBuilder sb = new(256);
			sb.Append('[').Append(caller.GetType().Name).Append(']')
			  .Append(" failed to fetch '").Append(target).Append("' from '").Append(this.name).Append("'.\n")
			  .Append("Reason: '").Append(target).Append("' (or any child class / interface implementer) is not registered, or '")
			  .Append(this.name).Append("' is uninitialized.\n");

			if (isReadOnly) {
				sb.Append("Or you tried to fetch a component from a target entity which doesn't have it registered, or doesn't have it at all.\n");
			}
			sb.Append("Path: ").Append(hierarchyPath);
			return sb.ToString();
		}

		/// <summary>
		/// Attempts to safely retrieve a mutatable component reference from the system registry.
		/// </summary>
		/// <remarks>
		/// Uses <see cref="HideInCallstackAttribute"/> to strip this helper frame from the Unity Console output,
		/// ensuring that double-clicking the error log directly navigates the user to the original gameplay 
		/// script line that initiated the failed fetch.
		/// </remarks>
		/// <typeparam name="TCaller">The Unity Object type executing the fetch request.</typeparam>
		/// <typeparam name="TComponent">The type of the mutatable component being requested.</typeparam>
		/// <param name="caller">The source object initiating the call; passed to the logger context to enable instant Inspector selection highlighting upon console selection.</param>
		/// <param name="hierarchyPath">The scene hierarchy path tracking the target entity's location.</param>
		/// <param name="component">The resulting out reference of the requested component, or null if lookups fail.</param>
		/// <returns>True if the component was found and registered successfully; otherwise, false.</returns>
		[HideInCallstack]
		public bool TryFetchMutable<TCaller, TComponent>(TCaller caller, string hierarchyPath, out TComponent component)
							where TCaller : UnityEngine.Object
							where TComponent : class {
			if (!this.componentRegistry.TryGetMutable(out component)) {
				// Passing 'caller' as the second parameter enables frame context picking inside the Inspector
				Debug.LogError(FormattedMessage<TCaller, TComponent>(caller, hierarchyPath, false), caller);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Attempts to safely retrieve a read-only component reference from the system registry.
		/// </summary>
		/// <remarks>
		/// Uses <see cref="HideInCallstackAttribute"/> to strip this helper frame from the Unity Console output,
		/// ensuring that double-clicking the error log directly navigates the user to the original gameplay 
		/// script line that initiated the failed fetch.
		/// </remarks>
		/// <typeparam name="TCaller">The Unity Object type executing the fetch request.</typeparam>
		/// <typeparam name="TComponent">The type of the read-only component or interface being requested.</typeparam>
		/// <param name="caller">The source object initiating the call; passed to the logger context to enable instant Inspector selection highlighting upon console selection.</param>
		/// <param name="hierarchyPath">The scene hierarchy path tracking the target entity's location.</param>
		/// <param name="component">The resulting out reference of the requested component, or null if lookups fail.</param>
		/// <param name="logTheError">Controls whether a failure emits an automated descriptive message directly to the console.</param>
		/// <returns>True if the component was found and registered successfully; otherwise, false.</returns>
		[HideInCallstack]
		public bool TryFetchReadOnly<TCaller, TComponent>(TCaller caller, string hierarchyPath, out TComponent component, bool logTheError = true)
			where TCaller : UnityEngine.Object
			where TComponent : class {
			if (!this.componentRegistry.TryGetReadOnly(out component)) {
				if (logTheError) {
					// Passing 'caller' as the second parameter enables frame context picking inside the Inspector
					Debug.LogError(FormattedMessage<TCaller, TComponent>(caller, hierarchyPath, true), caller);
				}
				return false;
			}
			return true;
		}
	}
}