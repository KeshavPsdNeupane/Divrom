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


		public string FormattedMessage<TCaller, TComponent>(TCaller caller, string hierarchyPath, bool isReadOnly = true)
		where TCaller : UnityEngine.Object
		where TComponent : class {
			string target = typeof(TComponent).Name;
			// Pre-allocate space to avoid internal re-allocations
			StringBuilder sb = new(256);
			sb.Append($"[{caller.GetType().Name}]")
			  .Append(" failed to fetch '").Append(target).Append("' from '").Append(this.name).Append("'.\n")
			  .Append("Reason: '").Append(target).Append("' (or any child class / interface implementer) is not registered, or '")
			  .Append(this.name).Append("' is uninitialized.\n");

			if (isReadOnly) {
				sb.Append("Or you tried to fetch a component from a target entity which doesn't have it registered, or doesn't have it at all.\n");
			}
			sb.Append("Path: ").Append(hierarchyPath);
			return sb.ToString();
		}

		public bool TryFetchMutable<TCaller, TComponent>(TCaller caller, string hierarchyPath, out TComponent component)
			where TCaller : UnityEngine.Object
			where TComponent : class {
			if (!this.componentRegistry.TryGetMutable(out component)) {
				Debug.LogError(FormattedMessage<TCaller, TComponent>(caller, hierarchyPath, false), caller);
				return false;
			}
			return true;
		}

		public bool TryFetchReadOnly<TCaller, TComponent>(TCaller caller, string hierarchyPath, out TComponent component, bool logTheError = true)
			where TCaller : UnityEngine.Object
			where TComponent : class {
			if (!this.componentRegistry.TryGetReadOnly(out component)) {
				if (logTheError) {
					Debug.LogError(FormattedMessage<TCaller, TComponent>(caller, hierarchyPath, true), caller);
				}
				return false;
			}
			return true;
		}
	}
}