using UnityEngine;
using Kope.Core.Init;
using Kope.Core.EntityComponentSystem;
using System;

/// <summary>
/// Store the Unique ID for an Entity and its EntityComponentStore reference. This is the main component that 
/// defines an Entity in the ECS architecture.
/// Every Entity must have an EntityManager component at its root, which holds a reference to its UniqueID and
///  EntityComponentStore. The EntityManager is responsible for initializing the EntityComponentStore and ensuring
/// that the Entity is properly registered in the prefab EntityComponentRegistry. It also provides a 
/// common entity name for easier identification and optimization in systems that require hashed tags.
/// <br/>
/// The EntityManager should be placed at the root of the Entity's GameObject hierarchy, and the UniqueID component 
/// should be placed on the same GameObject as the EntityManager to ensure proper identification and registration.
///  The EntityComponentStore can be on the same GameObject or a child GameObject, but it must be properly referenced
///  in the EntityManager for initialization and registration to work correctly.
/// <br/>
/// </summary>
[RequireComponent(typeof(UniqueID))]
public class EntityManager : InitializableBase, IEntityDiedOrPooled {
	[SerializeField, Tooltip("The name of this EntityComponentStore, used in Context grouping and other systems that require a hashed tag for identification and optimization purposes." +
		 "For eg, All the goblin based entity should have 'Goblin' as their common entity name, so that the system can easily query all goblin entities by their common hashed tag, without needing to check each individual component's hashed tag." +
		 "This common entity name is not required to be unique across different EntityComponentStore, but it is recommended to be unique for better identification and optimization. ")]
	private string commonEntityName;
	[SerializeField] private UniqueID uniqueID;
	[SerializeField] private EntityComponentsRegistry entityComponentRegistry;
	[SerializeField] private EntityCommonNameConfig config;
	private HashedTag commonEntityHashedTag;
	public string CommonEntityName => commonEntityName;

	/// <summary>
	/// This event is invoked when the Entity dies or is pooled, and it passes the UniqueID and CommonEntityHashedTag of 
	/// the Entity to any subscribed listeners.
	/// Systems that need to perform cleanup or other operations when an Entity dies or is pooled can subscribe to this 
	/// event and use the provided UniqueID and CommonEntityHashedTag to identify which Entity has died or been pooled, and perform the necessary actions accordingly. 
	/// This allows for a decoupled way for systems to react to Entity death or pooling events without needing direct 
	/// references to the EntityManager or
	/// We can sub to this event by either this field or the entityDetail's EntityDiedOrPooledHandler field, 
	/// since they both reference the same method in the EntityManager. The entityDetail is just a convenient container
	/// for passing around the Entity's details, including the UniqueID, CommonEntityHashedTag, and EntityComponentRegistry,
	/// which can be useful for systems that need to access this information when reacting to Entity death or pooling events.
	/// </summary>
	public event Action<EntityDetail> OnEntityDiedOrPooled;

	/// <summary>
	/// This EntityDetail instance is created and populated during the OnInit method of the EntityManager, 
	/// after validating that all necessary references are properly assigned and initialized.
	///  It serves as a convenient container for essential details about the Entity, such as its UniqueID,
	///  CommonEntityHashedTag, and EntityComponentRegistry, which can be easily accessed and passed around when needed,
	///  especially when notifying systems about the Entity's death or pooling events through the OnEntityDiedOrPooled event.
	///  By using this EntityDetail class, we can ensure that all relevant information about the Entity is organized and readily
	///  available for various operations within the ECS framework.
	/// </summary>
	private EntityDetail entityDetail;
	public EntityDetail EntityDetail => this.entityDetail;


	protected override bool OnInit() {
		if (!Validate()) return false;
		// After validation, we can be sure that the EntityComponentStore and its ComponentRegistry are properly initialized and ready to use.

		this.entityComponentRegistry.ComponentRegistry.Register(this.entityComponentRegistry);
		this.entityDetail = new EntityDetail(
			this.uniqueID, this.commonEntityHashedTag,
			 this.entityComponentRegistry.ComponentRegistry, this);
		return true;
	}

	public void NotifyEntityDiedOrPooled() {
		OnEntityDiedOrPooled?.Invoke(this.entityDetail);
	}

	void OnValidate() {
		EditorOnlyValidate();
	}

	private bool EditorOnlyValidate() {
		string parentStackTraceMessage = GetParentGameObjectHeirarchyMessage();
		if (uniqueID == null) {
			Debug.LogError($"EntityManager on GameObject '{this.gameObject.name}' is missing a UniqueID reference. Please assign one for proper identification and optimization.{parentStackTraceMessage}", this.gameObject);
			return false;
		}
		if (entityComponentRegistry == null) {
			Debug.LogError($"EntityManager on GameObject '{this.gameObject.name}' is missing an EntityComponentStore reference. Please assign one for proper functionality.{parentStackTraceMessage}", this.gameObject);
			return false;
		}

		if (string.IsNullOrEmpty(this.commonEntityName)) {
			Debug.LogError($"EntityComponentStore on GameObject '{this.gameObject.name}' is missing a common entity name. Please assign one for proper identification and optimization.{parentStackTraceMessage}", this.gameObject);
			return false;
		}
		if (this.config == null) {
			Debug.LogError($"EntityComponentStore '{this.commonEntityName}' is missing its config reference. Cannot initialize component registry.{parentStackTraceMessage}", this.gameObject);
			return false;
		}
		this.commonEntityHashedTag = new HashedTag(this.commonEntityName);
		if (!this.config.InternalContains(this.commonEntityHashedTag)) {
			Debug.LogError($"EntityComponentStore '{this.commonEntityName}' has a common entity name that is not registered in the EntityCommonNameConfig. Please add it to the config for proper initialization and optimization.{parentStackTraceMessage}", this.gameObject);
			return false;
		}
		return true;
	}

	private bool Validate() {
		bool isValid = EditorOnlyValidate();
		if (!isValid) return false;
		var registry = this.entityComponentRegistry.ComponentRegistry;
		if (registry == null) {
			Debug.LogError($"[EntityManager] there is an issue , 'the component registry is not initialized yet', for {this.gameObject.name}" +
			"Please check the InitManager and make sure the EntityComponentRegistry is placed on list" +
			$"And the EntityManager is placed after the EntityComponentRegistry in the execution order, and that the EntityComponentRegistry is properly initialized in its OnInit method.{GetParentGameObjectHeirarchyMessage()}", this.gameObject);
			return false;
		}
		return true;

	}
}