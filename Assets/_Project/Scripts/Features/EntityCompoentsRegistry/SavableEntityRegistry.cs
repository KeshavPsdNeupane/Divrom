using System.Collections.Generic;
using Kope.Core.Identity;
using Kope.Core.Types.Hashes;
using Kope.SaveSystem;
using Kope.Core.ServiceLocator;
using UnityEngine;

namespace Kope.EntityComponentSystem {
	/// <summary>
	/// This class is responsible for managing the save data of all entities registered in the scene, 
	/// providing a centralized registry for saving and loading entity states.
	/// It maintains a dictionary of registered entities and their corresponding save data providers,
	/// allowing for efficient retrieval and management of entity save data during the save and load processes.
	/// The EntityRegistrySaveDataManager interacts with the scene save system to ensure that 
	/// all registered entities are properly saved and loaded,
	/// </summary>
	public class SavableEntityRegistry : SceneServiceBase, ISceneSaveProvider {

		// default entity we can seperate this whole entity to multiple seperate registry,
		// if we want to support more complex structure and more optimized save data callback,
		// for example, we can have a seperate registry for player related entities, and another seperate
		//  registry for environment related entities, and so on,
		[SerializeField] private SceneDataProviderTypeEnum providerType = SceneDataProviderTypeEnum.EntityRegistry;

		private readonly Dictionary<HashedTag, IEntitySavePacketProvider> _saveAbleEntityes = new();
		private SceneSaveSystem _sceneSaveSystem;

		public SceneDataProviderTypeEnum ProviderType => this.providerType;


		protected override bool OnInitializeService() {
			Awake();
			if (!SceneServiceLocator.Instance.TryGetService(out this._sceneSaveSystem)) {
				Debug.LogError("SceneSaveSystem is not registered in SceneServiceLocator. Please check your SceneBootStrap!");
				return false;
			}
			this._sceneSaveSystem.RegisterProvider(this);
			return true;
		}


		public void RegisterEntity(EntitySaveSystem entity) {
			if (entity == null) return;
			var id = entity.UniqueID;
			if (this._saveAbleEntityes.ContainsKey(id)) return;
			this._saveAbleEntityes.Add(id, entity);
		}
		public void UnregisterEntity(HashedTag entityId) {
			// if the  highlevel entity registry doesnot contains a entity instance,
			// the sub set savable entity of entity registry will also not contain the entity.
			// we can can just skip if the highlevel entity registry doesnot contain the entity instance,
			// since it means the entity is not registered at all, and we don't need to do anything to unregister it.
			if (this._saveAbleEntityes.ContainsKey(entityId)) {
				this._saveAbleEntityes.Remove(entityId);
			}
		}

		/// <summary>
		/// This Update method is purely for debugging purposes, to track the number of registered entities in the scene in real-time.
		/// It logs the current count of registered entities whenever there is a change in the count, allowing developers to monitor the registry's state and ensure that entities are being registered and unregistered correctly throughout the
		/// </summary>
		private int tempCounter = 0;


		void Update() {
			if (this._saveAbleEntityes.Count != tempCounter) {
				//				Debug.Log($"[EntityRegistrySaveDataManager] Current registered savable entities count: {this._saveAbleEntityes.Count}");
				tempCounter = this._saveAbleEntityes.Count;
			}
			foreach (var kvp in this._saveAbleEntityes) {
				var entityId = kvp.Key;
				var provider = kvp.Value;
				if (provider == null) {
					Debug.LogWarning($"[EntityRegistrySaveDataManager] Found a null save packet provider for entity ID {entityId}. This may indicate an issue with the entity's components or registration.");
				}
			}
		}

		public SceneSaveDataContainer OnSave() {
			var entitySavePackets = new Dictionary<HashedTag, EntitySavePacket>();
			foreach (var kvp in this._saveAbleEntityes) {
				var provider = kvp.Value;
				var packet = provider.GetEntitySavePacket();
				entitySavePackets[kvp.Key] = packet;
			}
			return new SceneSaveDataContainer(this.ProviderType, entitySavePackets);
			// we will use the entity save packets to save the state of each entity in the scene, and we will use the unique ID of each entity as the key to save the data in the save system.
			// we can also add some additional data to the save packet if needed, such as the position and rotation of the entity, and other relevant data that we want to save for each entity.
		}

		public void OnLoad(SceneSaveDataContainer data) {
			foreach (var kvp in data.EntitySavePackets) {
				var entityId = kvp.Key;
				var packet = kvp.Value;
				if (this._saveAbleEntityes.TryGetValue(entityId, out var provider)) {
					provider.LoadEntitySavePacket(packet);
				} else {
					Debug.LogWarning($"No save packet provider found for entity ID {entityId}. Skipping loading for this entity.");
				}
			}
			// we will use the unique ID of each entity to find the corresponding entity in the scene, and then we will use the save packet to load the state of the entity in the scene.
		}
	}

}
