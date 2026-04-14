using System;
using System.Collections.Generic;
using UnityEngine;
using Kope.SaveSystem;
using ServiceLocatorPattern;
using Kope.Core.Init;
using Kope.Core.EntityComponentRegistry;
using Kope.EntityComponentSystem;

namespace Kope.Core.Identity {
	[RequireComponent(typeof(EntityInstance))]
	public class EntitySaveSystem : InitializableBase, IEntitySavePacketProvider {
		[SerializeField] private EntityInstance identity;
		private readonly Dictionary<Type, ISaveable> _saveableComponents = new();


		private SavableEntityRegistry _savableEntityRegistry;

		public HashedTag UniqueID => identity.EntityDetail.UniqueID.HashedTag;

		protected void Awake() {
			if (identity == null) this.identity = GetComponent<EntityInstance>();

			var registry = this.identity.ComponentsRegistryForSaveSystemOnly;
			registry.Register(this);

			if (!SceneServiceLocator.Instance.TryGetService<SavableEntityRegistry>(out var savableEntityRegistry)) {
				Debug.LogError($"[EntitySaveSystem] No SavableEntityRegistry found in the scene. Please ensure that a SavableEntityRegistry is present and properly initialized in the scene for EntitySaveSystem to function correctly.{GetParentGameObjectHeirarchyMessage()}", this.gameObject);
				return;
			}
			RegisterSaveDataChunk();
			this._savableEntityRegistry = savableEntityRegistry;

			// registering on Awake is still debatable, since we need to solve the mental mapping,
			// of which type of entity can be registered to the save system, and when should they register,
			// do we have disabled entity that can be registered to the save system? if so, 
			// then we need to make sure they register on Awake, since they will never have chance 
			// to register if they are disabled at the start of the game.
			// so this 1 line of code need more thought and refinement, but for now we will just 
			// register the entity on Awake to make sure all the entities are registered to the save system 
			// before the player can interact with them, and we can refine this logic in the future if needed.
			this._savableEntityRegistry.RegisterEntity(this);

			return;
		}

		// Register to event on Enable and Unregister on Disable to ensure that we properly unregister 
		//  entity from the global save system when the entity is destroyed or pooled,
		//  preventing potential memory leaks or stale references in the save system.
		void OnEnable() => this.identity.OnEntityDiedOrPooled += UnRegisterTheEntity;
		void OnDisable() => this.identity.OnEntityDiedOrPooled -= UnRegisterTheEntity;

		/// <summary>
		/// Scans the component registry and caches everything that implements ISaveable.
		/// Called during initialization by the identity or global system.
		/// </summary>
		public void RegisterSaveDataChunk() {
			//			Debug.Log($"[EntitySaveSystem] Registering save data chunk for entity with ID {this.UniqueID}. Scanning components for ISaveable implementations.");
			var registry = this.identity.ComponentsRegistryForSaveSystemOnly;
			if (registry == null) return;

			this._saveableComponents.Clear();
			var processedObjects = new HashSet<ISaveable>();

			foreach (var kvp in registry.Components) {
				if (kvp.Value is ISaveable saveable && !processedObjects.Contains(saveable)) {
					this._saveableComponents[saveable.GetType()] = saveable;
					processedObjects.Add(saveable);
				}
			}
		}


		private void UnRegisterTheEntity(EntityDetail detail) {

			this._savableEntityRegistry.UnregisterEntity(this.UniqueID);
		}

		public EntitySavePacket GetEntitySavePacket() {
			var dataChunks = new Dictionary<string, ISaveData>();

			foreach (var kvp in this._saveableComponents) {
				if (!SaveTypeRegistry.TryGetId(kvp.Key, out var saveId)) {
					Debug.LogWarning($"[EntitySaveSystem] No SaveId registered for component type '{kvp.Key.FullName}'. Skipping save data.");
					continue;
				}

				dataChunks[saveId] = kvp.Value.GetSaveData();
			}
			var ed = this.identity.EntityDetail;
			return new EntitySavePacket(
				ed.CommonEntityHashedTag,
				ed.EntityIdentityCategoryEnum,
				ed.UniqueID.HashedTag,
				dataChunks
			);
		}

		public void LoadEntitySavePacket(EntitySavePacket packet) {
			var registry = this.identity.ComponentsRegistryForSaveSystemOnly;

			foreach (var kvp in packet.Data) {
				string saveId = kvp.Key;
				ISaveData dataStruct = kvp.Value;

				if (!SaveTypeRegistry.TryResolve(saveId, out Type componentType)) {
					Debug.LogWarning($"[EntitySaveSystem] No component type registered for SaveId '{saveId}'. Skipping load.");
					continue;
				}

				if (registry.Components.TryGetValue(componentType, out var component)) {
					if (component is ISaveable targetSaveable) {
						targetSaveable.LoadFromSaveData(dataStruct);
					}
				}
			}
		}

		public bool ValidateIdentity(string callerInfo = null) => this.identity.ValidateIdentity(callerInfo);
	}
}