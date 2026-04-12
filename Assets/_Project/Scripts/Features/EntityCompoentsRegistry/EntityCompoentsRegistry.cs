using System.Collections.Generic;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Core.EntityComponentRegistry {
	/// <summary>
	/// This class takes only InitializableBase as component in inpector but we can,
	/// bypass the InitializableBase requirement and register any component we want 
	/// in the ComponentRegistry during runtime just like the SaveSystem does with the EntitySaveSystem component.
	/// Since for that Save system we dont want to force the user to add an InitializableBase component just for saving/loading purposes, we can just register the EntitySaveSystem directly in the registry during runtime and it will work just fine.
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
	public class EntityComponentsRegistry : InitializableBase {

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

		protected override bool OnInit() {
			try {
				this.componentRegistry = new ComponentRegistry(
				this.registryName,
				this.entityTransform,
				this.hasBehavioralComponents,
				this.config.ExcludedTypeSet
			);

				// First register all components
				foreach (var c in components) {
					if (c != null) componentRegistry.Register(c);
				}
				// Then init all components, this will ensure that dependencies are resolved during Init
				// since all components are already registered. and no runtime race conditions occur.
				// but still order of components in the list matters anyway 
				foreach (var c in components) {
					if (c != null) c.Init();
				}
				return true;
			} catch (System.Exception ex) {
				Debug.LogError($"Exception during EntityComponentsRegistry initialization: {ex.Message}\n{ex.StackTrace}" + GetParentGameObjectHeirarchyMessage());
				return false;
			}
		}
	}

}