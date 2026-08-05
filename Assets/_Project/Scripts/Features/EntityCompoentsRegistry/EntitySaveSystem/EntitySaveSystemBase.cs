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

		[SerializeField] protected EntityInstance entityInstance;

		// SaveId -> concrete component instance, rebuilt per entity each time components
		// are (re)registered. This is the ONLY place component ids get resolved to a type/
		// instance - deliberately local to this entity, never global. A saveId can be
		// shared by multiple concrete types via [InheritSaveId], so a global id -> Type
		// lookup would be ambiguous; matching against this entity's own live components
		// is not, since only one concrete type for a given id can exist on it at once.
		private readonly Dictionary<string, ISaveable> _saveIdToComponent = new();

		// Registered into EntityRegistry independently of EntityInstance's own
		// registration - this system only ever touches the save-provider tables and
		// never the live mob/prop entity tables. EntityInstance has no idea this happens.
		protected EntityRegistry _entityRegistry;

		public HashedTag UniqueID {
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

			if (!ServiceLocator.SceneServiceLocator.Instance.TryGetService<EntityRegistry>(out var entityRegistry)) {
				Debug.LogError($"[{GetType().Name}] No EntityRegistry found in the scene." +
				" Please ensure that an EntityRegistry is present and properly initialized " +
				$"in the scene for EntitySaveSystem to function correctly.{this.GetFullHierarchyPath()}", this.gameObject);
				return;
			}

			this._entityRegistry = entityRegistry;
			RegisterSaveDataChunk();
			RegisterToGlobalRegistry();
		}

		void OnEnable() => this.entityInstance.OnEntityDiedOrPooledEvent(UnRegisterTheEntity, true);
		void OnDisable() => this.entityInstance.OnEntityDiedOrPooledEvent(UnRegisterTheEntity, false);

		public void RegisterSaveDataChunk() {
			var registry = this.entityInstance.ComponentsRegistryForSaveSystemOnly;
			if (registry == null) return;

			this._saveIdToComponent.Clear();
			var processedObjects = new HashSet<ISaveable>();

			foreach (var kvp in registry.Components) {
				if (kvp.Value is not ISaveable saveable || !processedObjects.Add(saveable)) continue;

				if (!SaveTypeRegistry.TryGetComponentId(saveable.GetType(), out var saveId)) {
					// Not every ISaveable necessarily needs to be save-tracked, so this is a
					// warning rather than an error - but it means this component's state
					// silently won't persist, which is worth knowing at registration time
					// rather than discovering it after a save/load round-trip loses data.
					Debug.LogWarning($"[{GetType().Name}] Component '{saveable.GetType().FullName}' on " +
						$"{this.GetFullHierarchyPath()} implements ISaveable but has no registered SaveId " +
						"([SaveComponent] or [InheritSaveId]). Its data will not be saved.");
					continue;
				}

				if (!this._saveIdToComponent.TryAdd(saveId, saveable)) {
					// Two saveable components on the SAME entity resolved to the same id -
					// this is always a bug (either a duplicate [SaveComponent] id slipped past
					// the editor validator, or two unrelated InheritSaveId chains collided).
					// Whichever one lost silently would otherwise fail to save/load with no signal.
					Debug.LogError($"[{GetType().Name}] Duplicate SaveId '{saveId}' on {this.GetFullHierarchyPath()}: " +
						$"both '{this._saveIdToComponent[saveId].GetType().FullName}' and " +
						$"'{saveable.GetType().FullName}' resolve to it. Only the first will be saved/loaded.");
				}
			}
		}

		private void UnRegisterTheEntity(EntityDetailBase detail) {
			this._entityRegistry.UnregisterEntity(this.UniqueID);
		}

		protected abstract void RegisterToGlobalRegistry();
		protected abstract TPacket CreateSavePacket(HashedTag uid, TConfig config, Dictionary<string, ISaveData> data);
		protected abstract Dictionary<string, ISaveData> GetPacketData(TPacket packet);

		public TPacket GetEntitySavePacket() {
			var dataChunks = new Dictionary<string, ISaveData>();

			foreach (var kvp in this._saveIdToComponent) {
				dataChunks[kvp.Key] = kvp.Value.GetSaveData();
			}

			return CreateSavePacket(UniqueID, (TConfig)this.entityInstance.Config, dataChunks);
		}

		public void LoadEntitySavePacket(TPacket packet) {
			var dataMap = GetPacketData(packet);

			foreach (var kvp in dataMap) {
				string saveId = kvp.Key;
				ISaveData dataStruct = kvp.Value;

				if (!this._saveIdToComponent.TryGetValue(saveId, out var targetSaveable)) {
					// Expected in two legitimate cases, not just errors: the entity's config
					// changed and no longer has a component for this id, or the save predates
					// a rename/removal. Either way, skipping is correct - don't throw.
					Debug.LogWarning($"[{GetType().Name}] No saveable component on {this.GetFullHierarchyPath()} " +
						$"matches SaveId '{saveId}'. Skipping (component may have been removed, or this data " +
						"is stale from an older save version).");
					continue;
				}

				targetSaveable.LoadFromSaveData(dataStruct);
			}
		}
	}
}