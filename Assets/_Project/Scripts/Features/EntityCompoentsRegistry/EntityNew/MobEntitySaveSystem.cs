using System.Collections.Generic;
using UnityEngine;
using Kope.SaveSystem;
using Kope.EntityComponentSystem;
using Kope.Core.Collections.Extensions;
using Kope.Core.Collections.Hashes;

namespace Kope.Core.Identity {
	/// <summary>
	/// Manages the save and load states of a single entity, serving as a centralized system 
	/// for serializing, deserializing, and tracking the entity's overall state.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This system maintains a registry of saveable components and their state providers, 
	/// enabling fast lookups and state management when saving or restoring the entity. It interacts 
	/// directly with the scene-level save system to coordinate individual entity serialization 
	/// with global save files.
	/// </para>
	/// <para>
	/// <b>Lifecycle &amp; Architecture Note:</b><br/>
	/// This class intentionally does not inherit from <c>InitializableBase</c> or <c>ComponentBase</c>. 
	/// Base components are processed and initialized *before* the parent <c>EntityInstance</c> is fully established. 
	/// To bypass this timing conflict, this class registers itself directly with the entity's component registry 
	/// during its own <c>Awake</c> cycle, ensuring the <c>EntityInstance</c> is fully ready when registration occurs.
	/// </para>
	/// </remarks>
	public class MobEntitySaveSystem : MonoBehaviour, IMobEntitySavePacketProvider {
		[SerializeField] private MobInstance entityInstance;
		private readonly Dictionary<System.Type, ISaveable> _saveableComponents = new();

		private SavableEntityRegistry _savableEntityRegistry;

		public HashedTag UniqueID => this.entityInstance.MobEntityDetail.UniqueID.HashedTag;

		protected void Awake() {
			if (this.entityInstance == null) {
				Debug.LogError($"[MobEntitySaveSystem] EntityInstance reference is not set on" +
				$" {this.GetFullHierarchyPath()}. Please assign the EntityInstance in the inspector.");
				return;
			}

			var registry = this.entityInstance.ComponentsRegistryForSaveSystemOnly;
			registry.Register(this);
			this.entityInstance.SetSavable(true);

			if (!ServiceLocator.SceneServiceLocator.Instance.TryGetService<SavableEntityRegistry>(out var savableEntityRegistry)) {
				Debug.LogError($"[MobEntitySaveSystem] No SavableEntityRegistry found in the scene." +
				" Please ensure that a SavableEntityRegistry is present and properly initialized " +
				$"in the scene for EntitySaveSystem to function correctly.{this.GetFullHierarchyPath()}"
				, this.gameObject);
				return;
			}
			RegisterSaveDataChunk();
			this._savableEntityRegistry = savableEntityRegistry;
			this._savableEntityRegistry.RegisterMobEntity(this);
			return;
		}

		// Register to event on Enable and Unregister on Disable to ensure that we properly unregister 
		//  entity from the global save system when the entity is destroyed or pooled,
		//  preventing potential memory leaks or stale references in the save system.
		void OnEnable() => this.entityInstance.OnEntityDiedOrPooledEvent(UnRegisterTheEntity, true);
		void OnDisable() => this.entityInstance.OnEntityDiedOrPooledEvent(UnRegisterTheEntity, false);

		/// <summary>
		/// Scans the component registry and caches everything that implements ISaveable.
		/// Called during initialization by the identity or global system.
		/// </summary>
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


		private void UnRegisterTheEntity(MobEntityDetail detail) {

			this._savableEntityRegistry.UnregisterEntity(this.UniqueID);
		}

		public MobEntitySavePacket GetEntitySavePacket() {
			var dataChunks = new Dictionary<string, ISaveData>();

			foreach (var kvp in this._saveableComponents) {
				if (!SaveTypeRegistry.TryGetId(kvp.Key, out var saveId)) {
					Debug.LogWarning($"[EntitySaveSystem] No SaveId registered for component type '{kvp.Key.FullName}'. Skipping save data.");
					continue;
				}
				dataChunks[saveId] = kvp.Value.GetSaveData();
			}
			var ed = this.entityInstance.MobEntityDetail;
			return new MobEntitySavePacket(ed.UniqueID.HashedTag, ed.MobConfig, dataChunks);
		}

		public void LoadEntitySavePacket(MobEntitySavePacket packet) {
			var registry = this.entityInstance.ComponentsRegistryForSaveSystemOnly;

			foreach (var kvp in packet.Data) {
				string saveId = kvp.Key;
				ISaveData dataStruct = kvp.Value;

				if (!SaveTypeRegistry.TryResolve(saveId, out System.Type componentType)) {
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
	}
}