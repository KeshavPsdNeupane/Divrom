using System.Collections.Generic;
using UnityEngine;
using Kope.SaveSystem;
using Kope.EntityComponentSystem;
using Kope.Core.Collections.Extensions;
using Kope.Core.Collections.Hashes;
using Kope.EntityIdentity;

namespace Kope.Core.Identity {
	public abstract class EntitySaveSystemBase<TConfig, TDetail, TPacket> : MonoBehaviour
		where TConfig : EntityConfig {

		[SerializeField] protected EntityInstanceNew entityInstance;
		private readonly Dictionary<System.Type, ISaveable> _saveableComponents = new();
		protected SavableEntityRegistry _savableEntityRegistry;

		public HashedTag UniqueID {
			// really not possible to have a null UniqueID, but just in case, we will return default 
			// if it is null
			get {
				if (this.entityInstance == null || this.entityInstance.EntityDetail == null
				|| this.entityInstance.EntityDetail.UniqueID == null) {
					return default;
				}
				return this.entityInstance.EntityDetail.UniqueID.HashedTag;
			}
		}

		protected virtual void Awake() {
			if (this.entityInstance == null) {
				Debug.LogError($"[{GetType().Name}] EntityInstance reference is not set on" +
				$" {this.GetFullHierarchyPath()}. Please assign the EntityInstance in the inspector.");
				return;
			}

			var registry = this.entityInstance.ComponentsRegistryForSaveSystemOnly;
			registry.Register(this);
			this.entityInstance.SetSavable(true);

			if (!ServiceLocator.SceneServiceLocator.Instance.TryGetService<SavableEntityRegistry>(out var savableRegistry)) {
				Debug.LogError($"[{GetType().Name}] No SavableEntityRegistry found in the scene." +
				" Please ensure that a SavableEntityRegistry is present and properly initialized " +
				$"in the scene for EntitySaveSystem to function correctly.{this.GetFullHierarchyPath()}", this.gameObject);
				return;
			}

			this._savableEntityRegistry = savableRegistry;
			RegisterSaveDataChunk();
			RegisterToGlobalRegistry();
		}

		void OnEnable() => this.entityInstance.OnEntityDiedOrPooledEvent(UnRegisterTheEntity, true);
		void OnDisable() => this.entityInstance.OnEntityDiedOrPooledEvent(UnRegisterTheEntity, false);

		public void RegisterSaveDataChunk() {
			var registry = this.entityInstance.ComponentsRegistryForSaveSystemOnly;
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

		private void UnRegisterTheEntity(EntityDetailBase detail) {
			this._savableEntityRegistry.UnregisterEntity(this.UniqueID);
		}

		protected abstract void RegisterToGlobalRegistry();
		protected abstract TPacket CreateSavePacket(HashedTag uid, TConfig config, Dictionary<string, ISaveData> data);
		protected abstract Dictionary<string, ISaveData> GetPacketData(TPacket packet);

		public TPacket GetEntitySavePacket() {
			var dataChunks = new Dictionary<string, ISaveData>();

			foreach (var kvp in this._saveableComponents) {
				if (!SaveTypeRegistry.TryGetId(kvp.Key, out var saveId)) {
					Debug.LogWarning($"[{GetType().Name}] No SaveId registered for component type '{kvp.Key.FullName}'. Skipping save data.");
					continue;
				}
				dataChunks[saveId] = kvp.Value.GetSaveData();
			}

			return CreateSavePacket(UniqueID, (TConfig)this.entityInstance.Config, dataChunks);
		}

		public void LoadEntitySavePacket(TPacket packet) {
			var registry = this.entityInstance.ComponentsRegistryForSaveSystemOnly;
			var dataMap = GetPacketData(packet);

			foreach (var kvp in dataMap) {
				string saveId = kvp.Key;
				ISaveData dataStruct = kvp.Value;

				if (!SaveTypeRegistry.TryResolve(saveId, out System.Type componentType)) {
					Debug.LogWarning($"[{GetType().Name}] No component type registered for SaveId '{saveId}'. Skipping load.");
					continue;
				}
				if (registry.Components.TryGetValue(componentType, out var component)) {
					if (component is ISaveable targetSaveable) {
						targetSaveable.LoadFromSaveData(dataStruct);
					}
				}
			}
		}
	}
}